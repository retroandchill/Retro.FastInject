using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Retro.FastInject.Annotations;
using Retro.FastInject.Utils;
namespace Retro.FastInject.ServiceHierarchy;

/// <summary>
/// Records a service resolution for a constructor parameter.
/// </summary>
public class ParameterResolution
{
    /// <summary>
    /// The parameter symbol
    /// </summary>
    public IParameterSymbol Parameter { get; set; } = null!;
    
    /// <summary>
    /// The type being injected
    /// </summary>
    public ITypeSymbol ParameterType { get; set; } = null!;
    
    /// <summary>
    /// Key used for resolution, null for non-keyed services
    /// </summary>
    public string? Key { get; set; }
    
    /// <summary>
    /// The service registration that was selected for this parameter
    /// </summary>
    public ServiceRegistration? SelectedService { get; set; }
    
    /// <summary>
    /// Whether the parameter was resolved through an indirect service
    /// </summary>
    public bool IsIndirectResolution { get; set; }
    
    /// <summary>
    /// The indirect implementation type if IsIndirectResolution is true
    /// </summary>
    public ITypeSymbol? IndirectImplementationType { get; set; }
}

/// <summary>
/// Records all service resolutions for a constructor.
/// </summary>
public class ConstructorResolution
{
    /// <summary>
    /// The constructor that was resolved
    /// </summary>
    public IMethodSymbol Constructor { get; set; } = null!;
    
    /// <summary>
    /// The type the constructor belongs to
    /// </summary>
    public ITypeSymbol Type { get; set; } = null!;
    
    /// <summary>
    /// All parameter resolutions for this constructor
    /// </summary>
    public List<ParameterResolution> Parameters { get; } = [];
}

public class ServiceManifest {
  

  private readonly Dictionary<ITypeSymbol, List<ServiceRegistration>> _services;
  private readonly Dictionary<ITypeSymbol, ITypeSymbol> _indirectServices;
  private readonly HashSet<ITypeSymbol> _baseTypes;
  private readonly Dictionary<ITypeSymbol, ConstructorResolution> _constructorResolutions;

  public ServiceManifest() {
    _services = new Dictionary<ITypeSymbol, List<ServiceRegistration>>(TypeSymbolEqualityComparer.Instance);
    _indirectServices = new Dictionary<ITypeSymbol, ITypeSymbol>(TypeSymbolEqualityComparer.Instance);
    _baseTypes = new HashSet<ITypeSymbol>(TypeSymbolEqualityComparer.Instance);
    _constructorResolutions = new Dictionary<ITypeSymbol, ConstructorResolution>(TypeSymbolEqualityComparer.Instance);
  }
  
  /// <summary>
  /// Gets the constructor resolution information for a given type.
  /// </summary>
  /// <param name="type">The type to get constructor resolution for.</param>
  /// <returns>Constructor resolution information or null if not available.</returns>
  public ConstructorResolution? GetConstructorResolution(ITypeSymbol type)
  {
      return _constructorResolutions.TryGetValue(type, out var resolution) ? resolution : null;
  }
  
  /// <summary>
  /// Gets all constructor resolutions that have been recorded.
  /// </summary>
  public IEnumerable<ConstructorResolution> GetAllConstructorResolutions()
  {
      return _constructorResolutions.Values;
  }
  
  /// <summary>
  /// Checks if all dependencies in the constructor can be resolved and records the resolution.
  /// </summary>
  /// <param name="type">The type to check constructors for</param>
  /// <exception cref="InvalidOperationException">Thrown when dependencies cannot be resolved or multiple public constructors exist</exception>
  public void CheckConstructorDependencies(ITypeSymbol type) {
    if (type is not INamedTypeSymbol namedTypeSymbol) return;
    
    var publicConstructors = namedTypeSymbol.Constructors
      .Where(c => c.DeclaredAccessibility == Accessibility.Public)
      .ToArray();
    
    switch (publicConstructors.Length) {
      case 0:
        return;
      case > 1:
        throw new InvalidOperationException(
            $"Type '{type.ToDisplayString()}' has multiple public constructors. Only one public constructor is allowed for dependency injection.");
    }

    var constructor = publicConstructors[0];
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
      
      if (!canResolve) {
        // Add the missing dependency to the list with detailed information
        string dependency = $"{paramType.ToDisplayString()}";
        if (keyName != null) {
          dependency += $" with key '{keyName}'";
        }
        missingDependencies.Add(dependency);
      }
    }
    
    // Store the constructor resolution
    _constructorResolutions[type] = constructorResolution;
    
    if (missingDependencies.Count > 0) {
      throw new InvalidOperationException(
        $"Cannot resolve the following dependencies for type '{type.ToDisplayString()}':\n" +
        $"- {string.Join("\n- ", missingDependencies)}");
    }
  }

  public void AddService(ITypeSymbol serviceType, ServiceScope lifetime, ITypeSymbol? implementationType = null, string? key = null) {
    if (!_services.TryGetValue(serviceType, out var registrations)) {
      registrations = [];
      _services[serviceType] = registrations;
    }

    registrations.Add(new ServiceRegistration {
        Type = serviceType,
        Key = key,
        Lifetime = lifetime,
        ImplementationType = implementationType is null || implementationType.Equals(serviceType, SymbolEqualityComparer.Default) ? null : implementationType,
        IndexForType = registrations.Count
    });
  }

  public void AddIndirectService(ITypeSymbol serviceType, ITypeSymbol implementationType) {
    _indirectServices[serviceType] = implementationType;
  }

  public bool TryGetIndirectService(ITypeSymbol serviceType, out ITypeSymbol? implementationType) {
    return _indirectServices.TryGetValue(serviceType, out implementationType);
  }

  public void AddBaseType(ITypeSymbol baseType) {
    _baseTypes.Add(baseType);
  }

  public IEnumerable<ITypeSymbol> GetTypesWithMultipleResolutions() {
    return _services.Where(kvp => kvp.Value.Count > 1).Select(kvp => kvp.Key);
  }

  public IEnumerable<ServiceRegistration> GetKeyedServices() {
    return _services.Values
        .SelectMany(list => list)
        .Where(reg => reg.Key != null);
  }

  public IEnumerable<ServiceRegistration> GetUnnamedServices() {
    return _services.Values
        .SelectMany(list => list)
        .Where(reg => reg.Key == null);
  }

  public IEnumerable<ServiceRegistration> GetServicesByLifetime(ServiceScope lifetime) {
    return _services.Values
        .SelectMany(list => list)
        .Where(reg => reg.Lifetime == lifetime);
  }

  public IEnumerable<ITypeSymbol> GetBaseTypes() {
    return _baseTypes;
  }
}