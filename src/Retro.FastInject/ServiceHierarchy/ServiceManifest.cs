using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Retro.FastInject.Annotations;
using Retro.FastInject.Utils;
namespace Retro.FastInject.ServiceHierarchy;

/// <summary>
/// Represents a manifest for managing service registrations, dependencies, and resolutions.
/// Provides functionalities to track and retrieve services based on different parameters such as
/// lifetime, associated keys, and constructor dependencies.
/// </summary>
public class ServiceManifest {
  
  private readonly Dictionary<ITypeSymbol, List<ServiceRegistration>> _services = new(TypeSymbolEqualityComparer.Instance);
  private readonly Dictionary<ITypeSymbol, ITypeSymbol> _indirectServices = new(TypeSymbolEqualityComparer.Instance);
  private readonly Dictionary<ITypeSymbol, ConstructorResolution> _constructorResolutions = new(TypeSymbolEqualityComparer.Instance);

  /// <summary>
  /// Gets all constructor resolutions that have been recorded.
  /// </summary>
  public IEnumerable<ConstructorResolution> GetAllConstructorResolutions()
  {
      return _constructorResolutions.Values;
  }

  /// <summary>
  /// Checks if all dependencies in the constructor of the specified service registration can be resolved and records the resolution.
  /// </summary>
  /// <param name="declaration">The service registration representing the type and its associated symbol to check.</param>
  /// <exception cref="InvalidOperationException">
  /// Thrown when dependencies cannot be resolved, or when the type has multiple public constructors, or if the type is not a named type.
  /// </exception>
  public void CheckConstructorDependencies(ServiceRegistration declaration) {
    var type = declaration.Type;
    if (type is not INamedTypeSymbol namedTypeSymbol) {
      throw new InvalidOperationException($"Type '{type.ToDisplayString()}' is not a named type.");
    }

    var constructor = declaration.AssociatedSymbol switch {
        IMethodSymbol method => ValidateFactoryMethod(method),
        null => GetValidConstructor(namedTypeSymbol),
        _ => null
    };
    if (constructor is null) {
      return;
    }
    
    var missingDependencies = new List<string>();
    
    // Create constructor resolution record
    var constructorResolution = new ConstructorResolution
    {
        Constructor = constructor,
        Type = type
    };
    
    foreach (var parameter in constructor.Parameters) {
      var paramType = parameter.Type;
      
      // Create parameter resolution
      var parameterResolution = new ParameterResolution
      {
          Parameter = parameter,
          ParameterType = paramType
      };
      
      // Check for FromKeyedServices attribute
      var fromKeyedServicesAttr = parameter.GetAttributes()
        .FirstOrDefault(a => a.IsOfAttributeType<FromKeyedServicesAttribute>());
      
      string? keyName = null;
      if (fromKeyedServicesAttr is {
              ConstructorArguments.Length: > 0
          }) {
        keyName = fromKeyedServicesAttr.ConstructorArguments[0].Value?.ToString();
      }
      
      parameterResolution.Key = keyName;
      
      // Check if the dependency can be resolved
      var canResolve = false;
      ServiceRegistration? selectedService = null;
      
      if (keyName != null) {
        // For keyed service, look for service with matching key
        if (_services.TryGetValue(paramType, out var registrations)) {
          selectedService = registrations.FirstOrDefault(r => r.Key == keyName);
          canResolve = selectedService != null;
        }
      } 
      else {
        // For regular service, only look for non-keyed registrations
        if (_services.TryGetValue(paramType, out var registrations)) {
          selectedService = registrations.FirstOrDefault(r => r.Key == null);
          canResolve = selectedService != null;
        }
        
        // If we can't resolve directly, check indirect services
        if (!canResolve && _indirectServices.TryGetValue(paramType, out var implementationType)) {
          parameterResolution.IsIndirectResolution = true;
          parameterResolution.IndirectImplementationType = implementationType;
          
          if (_services.TryGetValue(implementationType, out var implRegistrations)) {
            selectedService = implRegistrations.FirstOrDefault(r => r.Key == null);
            canResolve = selectedService != null;
          }
        }
      }
      
      parameterResolution.SelectedService = selectedService;
      constructorResolution.Parameters.Add(parameterResolution);

      if (canResolve) continue;
      // Add the missing dependency to the list with detailed information
      var dependency = $"{paramType.ToDisplayString()}";
      if (keyName != null) {
        dependency += $" with key '{keyName}'";
      }
      missingDependencies.Add(dependency);
    }
    
    // Store the constructor resolution
    _constructorResolutions[type] = constructorResolution;
    
    if (missingDependencies.Count > 0) {
      throw new InvalidOperationException(
        $"Cannot resolve the following dependencies for type '{type.ToDisplayString()}':\n" +
        $"- {string.Join("\n- ", missingDependencies)}");
    }
  }

  private static IMethodSymbol? GetValidConstructor(INamedTypeSymbol type) {
    var publicConstructors = type.Constructors
        .Where(c => c.DeclaredAccessibility == Accessibility.Public)
        .ToArray();

    return publicConstructors.Length switch {
        0 => null,
        > 1 => throw new InvalidOperationException(
            $"Type '{type.ToDisplayString()}' has multiple public constructors. Only one public constructor is allowed for dependency injection."),
        _ => publicConstructors[0]
    };
  }

  private static IMethodSymbol ValidateFactoryMethod(IMethodSymbol methodSymbol) {
    if (methodSymbol.ReturnsVoid || methodSymbol.ReturnType is not INamedTypeSymbol) {
      throw new InvalidOperationException($"Factory method '{methodSymbol.ToDisplayString()}' must return a named non-void type.");
    }
    
    return methodSymbol;
  }

  /// <summary>
  /// Adds a service to the service manifest.
  /// </summary>
  /// <param name="serviceType">The type of the service to be added.</param>
  /// <param name="lifetime">The lifetime scope of the service.</param>
  /// <param name="implementationType">The implementation type of the service. Defaults to null if the implementation type is the same as the service type.</param>
  /// <param name="associatedSymbol">An optional symbol associated with the service.</param>
  /// <param name="key">An optional key to differentiate services of the same type.</param>
  public void AddService(ITypeSymbol serviceType, ServiceScope lifetime, ITypeSymbol? implementationType = null, ISymbol? associatedSymbol = null, string? key = null) {
    if (!_services.TryGetValue(serviceType, out var registrations)) {
      registrations = [];
      _services[serviceType] = registrations;
    }

    registrations.Add(new ServiceRegistration {
        Type = serviceType,
        Key = key,
        Lifetime = lifetime,
        ImplementationType = implementationType is null || implementationType.Equals(serviceType, SymbolEqualityComparer.Default) ? null : implementationType,
        IndexForType = registrations.Count,
        AssociatedSymbol = associatedSymbol,
        IsDisposable = serviceType.AllInterfaces.Any(i => i.IsOfType<IDisposable>()),
        IsAsyncDisposable = serviceType.AllInterfaces.Any(i => i.ToDisplayString() == "System.IAsyncDisposable")
    });
  }

  /// <summary>
  /// Adds an indirect service relationship between a service type and its implementation type.
  /// </summary>
  /// <param name="serviceType">The service type being registered.</param>
  /// <param name="implementationType">The implementation type associated with the service type.</param>
  public void AddIndirectService(ITypeSymbol serviceType, ITypeSymbol implementationType) {
    _indirectServices[serviceType] = implementationType;
  }

  /// <summary>
  /// Attempts to retrieve the implementation type for an indirect service registration.
  /// </summary>
  /// <param name="serviceType">The type of the service for which to retrieve the implementation type.</param>
  /// <param name="implementationType">When this method returns, contains the implementation type associated with the service type, if found; otherwise, null.</param>
  /// <returns>True if the implementation type was found; otherwise, false.</returns>
  public bool TryGetIndirectService(ITypeSymbol serviceType, out ITypeSymbol? implementationType) {
    return _indirectServices.TryGetValue(serviceType, out implementationType);
  }

  /// <summary>
  /// Retrieves all service registrations that have an associated key.
  /// </summary>
  /// <returns>
  /// An enumerable collection of service registrations where a key is specified.
  /// </returns>
  public IEnumerable<ServiceRegistration> GetKeyedServices() {
    return _services.Values
        .SelectMany(list => list)
        .Where(reg => reg.Key != null);
  }

  /// <summary>
  /// Retrieves all service registrations that do not have a specified key.
  /// </summary>
  /// <returns>
  /// An enumerable collection of unnamed service registrations.
  /// </returns>
  public IEnumerable<ServiceRegistration> GetUnnamedServices() {
    return _services.Values
        .SelectMany(list => list)
        .Where(reg => reg.Key == null);
  }

  /// <summary>
  /// Retrieves all service registrations that match the specified service lifetime.
  /// </summary>
  /// <param name="lifetime">The desired service lifetime for filtering the service registrations.</param>
  /// <returns>An enumerable collection of service registrations matching the specified lifetime.</returns>
  public IEnumerable<ServiceRegistration> GetServicesByLifetime(ServiceScope lifetime) {
    return _services.Values
        .SelectMany(list => list)
        .Where(reg => reg.Lifetime == lifetime);
  }
}