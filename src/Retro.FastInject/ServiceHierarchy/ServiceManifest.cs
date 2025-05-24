using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
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
  private readonly Dictionary<ITypeSymbol, List<ServiceRegistration>> _services =
      new(TypeSymbolEqualityComparer.Instance);

  private readonly Dictionary<ITypeSymbol, ConstructorResolution> _constructorResolutions =
      new(TypeSymbolEqualityComparer.Instance);

  /// <summary>
  /// Gets all constructor resolutions that have been recorded.
  /// </summary>
  public IEnumerable<ConstructorResolution> GetAllConstructorResolutions() {
    return _constructorResolutions.Values;
  }

  /// <summary>
  /// Checks if all dependencies in the constructor of the specified service registration can be resolved and records the resolution.
  /// </summary>
  /// <param name="declaration">The service registration representing the type and its associated symbol to check.</param>
  /// <param name="compilation">The Roslyn compilation providing semantic analysis for the type and its dependencies.</param>
  /// <exception cref="InvalidOperationException">
  /// Thrown when dependencies cannot be resolved, or when the type has multiple public constructors, or if the type is not a named type.
  /// </exception>
  public void CheckConstructorDependencies(ServiceRegistration declaration, Compilation compilation) {
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
    var constructorResolution = new ConstructorResolution {
        Constructor = constructor,
        Type = type
    };

    foreach (var parameter in constructor.Parameters) {
      ResolveParameterDependencies(parameter, constructorResolution, compilation, missingDependencies);
    }

    // Store the constructor resolution
    _constructorResolutions[type] = constructorResolution;

    if (missingDependencies.Count > 0) {
      throw new InvalidOperationException(
          $"Cannot resolve the following dependencies for type '{type.ToDisplayString()}':\n" +
          $"- {string.Join("\n- ", missingDependencies)}");
    }
  }

  private void ResolveParameterDependencies(IParameterSymbol parameter, ConstructorResolution constructorResolution,
                                            Compilation compilation,
                                            List<string> missingDependencies) {
    var (isNullable, paramType) = parameter.Type.CheckIfNullable();
    
    // Create parameter resolution
    var parameterResolution = new ParameterResolution {
        Parameter = parameter,
        ParameterType = paramType
    };

    // Check for FromKeyedServices attribute
    var fromKeyedServicesAttr = parameter.GetAttributes()
        .FirstOrDefault(a => a.IsOfAttributeType<FromKeyedServicesAttribute>());

    string? keyName = null;
    if (fromKeyedServicesAttr is { ConstructorArguments.Length: > 0 }) {
      keyName = fromKeyedServicesAttr.ConstructorArguments[0].Value?.ToString();
    }

    parameterResolution.Key = keyName;

    var canResolve = CanResolve(keyName, paramType, parameterResolution, compilation, out var selectedService);

    parameterResolution.SelectedService = selectedService;
    parameterResolution.DefaultValue = parameter.GetDefaultValueString();
    constructorResolution.Parameters.Add(parameterResolution);

    if (canResolve || isNullable || parameterResolution.DefaultValue is not null) return;
      
    // Add the missing dependency to the list with detailed information
    var dependency = new StringBuilder(paramType.ToDisplayString());
    if (keyName != null) {
      dependency.Append($" with key '{keyName}'");
    }

    // Add more specific error information
    if (parameterResolution.HasNoDeclaration) {
      dependency.Append(" (No service declaration found)");
    } else if (parameterResolution is { HasMultipleRegistrations: true, MultipleServices.Count: > 0 }) {
      dependency.Append($" (Multiple registrations found: {parameterResolution.MultipleServices.Count})");
      // Optionally add details about the multiple registrations
      foreach (var service in parameterResolution.MultipleServices) {
        var implType = service.ImplementationType?.ToDisplayString() ?? service.Type.ToDisplayString();
        dependency.Append($"\n  -- {implType}" + (service.Key != null ? $" with key '{service.Key}'" : ""));
      }
    }
  
    missingDependencies.Add(dependency.ToString());
  }

  private bool CanResolve(string? keyName, ITypeSymbol paramType, ParameterResolution parameterResolution,
                          Compilation compilation,
                          out ServiceRegistration? selectedService) {
    // Check if the dependency can be resolved
    var canResolve = false;
    selectedService = null;

    if (_services.TryGetValue(paramType, out var registrations)) {
      try {
        selectedService = registrations.SingleOrDefault(r => keyName is null || r.Key == keyName);
      } catch (InvalidOperationException) {
        parameterResolution.HasMultipleRegistrations = true;
        parameterResolution.MultipleServices = registrations.ToList();
        selectedService = null;
      }
      
      if (selectedService is not null) {
        selectedService = ResolveConcreteType(selectedService);
      }
  
      canResolve = selectedService != null;
    } else {
      // Check if the type is a collection type
      if (keyName is null && paramType is INamedTypeSymbol { IsGenericType: true } namedType 
                          && CanResolveGenericType(namedType, compilation, parameterResolution.Parameter,
                                                   ref selectedService)) {
        return true;
      }
      parameterResolution.HasNoDeclaration = true;
    }

    return canResolve;
  }

  private bool CanResolveGenericType(INamedTypeSymbol namedType, Compilation compilation, 
                                     IParameterSymbol targetParameter,
                                     ref ServiceRegistration? selectedService) {
    var genericTypeName = namedType.ConstructedFrom.ToDisplayString();
    if (genericTypeName is not ("System.Collections.Generic.IEnumerable<T>" or
        "System.Collections.Generic.IReadOnlyCollection<T>" or
        "System.Collections.Generic.IReadOnlyList<T>" or
        "System.Collections.Immutable.ImmutableArray<T>")) return false;
    
    var elementType = namedType.TypeArguments[0];
    if (!_services.TryGetValue(elementType, out var elementServices)) {
      elementServices = [];
    }
    
    var requireNonEmptyAttribute = targetParameter.GetAttribute<RequireNonEmptyAttribute>();
    if (requireNonEmptyAttribute is not null && elementServices.Count == 0) {
      return false;
    }
      
    var immutableArrayType = typeof(ImmutableArray<>).GetInstantiatedGeneric(compilation, elementType);
    AddService(immutableArrayType, ServiceScope.Transient);
    var readOnlyListType = typeof(IReadOnlyList<>).GetInstantiatedGeneric(compilation, elementType);
    AddService(readOnlyListType, ServiceScope.Transient, immutableArrayType);
    var readOnlyCollectionType = typeof(IReadOnlyCollection<>).GetInstantiatedGeneric(compilation, elementType);
    AddService(readOnlyCollectionType, ServiceScope.Transient, immutableArrayType);
    var enumerableType = typeof(IEnumerable<>).GetInstantiatedGeneric(compilation, elementType);
    AddService(enumerableType, ServiceScope.Transient, immutableArrayType,
        collectedServices: elementServices
            .Select(ResolveConcreteType)
            .ToList());
    selectedService = _services[immutableArrayType][0];
    return true;
  }

  private ServiceRegistration ResolveConcreteType(ServiceRegistration declaration) {
    if (declaration.ImplementationType is null || !_services.TryGetValue(declaration.ImplementationType, 
                                                                         out var possibleImpls)) {
      return declaration;
    }
    
    try {
      var implService = possibleImpls
          .SingleOrDefault(x => declaration.Key is null || declaration.Key == x.Key);
      if (implService is not null) {
        return implService;
      }
    } catch (InvalidOperationException) {
      // In this case swallow the exception and keep the abstract service
      // as the selected service.
    }
    
    return declaration;
  }

  private static IMethodSymbol? GetValidConstructor(INamedTypeSymbol type) {
    var publicConstructors = type.Constructors
        .Where(c => c.DeclaredAccessibility == Accessibility.Public)
        .ToArray();

    var explicitConstructors = publicConstructors
        .Where(x => !x.IsImplicitlyDeclared)
        .ToArray();

    if (explicitConstructors.Length > 0) {
      return explicitConstructors.Length switch {
          > 1 => throw new InvalidOperationException(
              $"Type '{type.ToDisplayString()}' has multiple public constructors. Only one public constructor is allowed for dependency injection."),
          _ => explicitConstructors[0]
      };
    }

    // Fallback to the first public constructor if there are no explicit constructors
    return publicConstructors.FirstOrDefault();
  }

  private static IMethodSymbol ValidateFactoryMethod(IMethodSymbol methodSymbol) {
    if (methodSymbol.ReturnsVoid || methodSymbol.ReturnType is not INamedTypeSymbol) {
      throw new InvalidOperationException(
          $"Factory method '{methodSymbol.ToDisplayString()}' must return a named non-void type.");
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
  /// <param name="collectedServices">The list of services that this is a collection of</param>
  public void AddService(ITypeSymbol serviceType, ServiceScope lifetime, ITypeSymbol? implementationType = null,
                         ISymbol? associatedSymbol = null, string? key = null, List<ServiceRegistration>? collectedServices = null) {
    if (!_services.TryGetValue(serviceType, out var registrations)) {
      registrations = [];
      _services[serviceType] = registrations;
    }

    registrations.Add(new ServiceRegistration {
        Type = serviceType,
        Key = key,
        Lifetime = lifetime,
        ImplementationType =
            implementationType is null || implementationType.Equals(serviceType, SymbolEqualityComparer.Default)
                ? null
                : implementationType,
        IndexForType = registrations.Count,
        AssociatedSymbol = associatedSymbol,
        CollectedServices = collectedServices,
        IsDisposable = serviceType.AllInterfaces.Any(i => i.IsOfType<IDisposable>()),
        IsAsyncDisposable = serviceType.AllInterfaces.Any(i => i.ToDisplayString() == "System.IAsyncDisposable")
    });
  }

  /// <summary>
  /// Retrieves all service registrations.
  /// </summary>
  /// <returns>
  /// An enumerable collection of service registrations.
  /// </returns>
  public IEnumerable<ServiceRegistration> GetAllServices() {
    return _services.Values
        .SelectMany(list => list);
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