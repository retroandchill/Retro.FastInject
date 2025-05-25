using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Retro.FastInject.Annotations;
using Retro.FastInject.Comparers;
using Retro.FastInject.Generation;
using Retro.FastInject.Utils;

namespace Retro.FastInject.Model.Manifest;

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
  /// Validates the entire dependency graph for circular dependencies.
  /// This should be called after all constructor dependencies have been resolved.
  /// </summary>
  /// <exception cref="InvalidOperationException">Thrown when a circular dependency is detected.</exception>
  public void ValidateDependencyGraph() {
    var visited = new HashSet<ITypeSymbol>(TypeSymbolEqualityComparer.Instance);
    var path = new Stack<ITypeSymbol>();
    var onPath = new HashSet<ITypeSymbol>(TypeSymbolEqualityComparer.Instance);
    
    foreach (var serviceType in GetAllServices().Select(x => x.Type)) {
      if (visited.Contains(serviceType)) continue;

      if (DetectCycle(serviceType, visited, path, onPath, out var cycle)) {
        throw new InvalidOperationException(
            $"Detected circular dependency: {string.Join(" → ", cycle.Select(t => t.ToDisplayString()))}");
      }
    }
  }
  
  private bool DetectCycle(ITypeSymbol type, HashSet<ITypeSymbol> visited, Stack<ITypeSymbol> path, 
                          HashSet<ITypeSymbol> onPath, [NotNullWhen(true)] out List<ITypeSymbol>? cycle) {
    cycle = null;
    
    
    if (onPath.Contains(type)) {
      return ExtractCycleFromPath(type, path, out cycle);
    }
    
    if (!visited.Add(type)) {
      return false; // Already visited and no cycle found
    }

    onPath.Add(type);
    path.Push(type);
    
    // If we have a constructor resolution for this type, check its dependencies
    if (_constructorResolutions.TryGetValue(type, out var resolution) && CheckServiceCycle(visited, path, onPath, ref cycle, resolution)) return true;

    // Done with this node
    path.Pop();
    onPath.Remove(type);
    return false;
  }
  private bool CheckServiceCycle(HashSet<ITypeSymbol> visited, 
                                 Stack<ITypeSymbol> path, 
                                 HashSet<ITypeSymbol> onPath, 
                                 [NotNullWhen(true)] ref List<ITypeSymbol>? cycle, 
                                 ConstructorResolution resolution) {
    foreach (var serviceRegistration in resolution.Parameters
                 .Select(param => new {
                     param,
                     isNullable = param.Parameter.Type.NullableAnnotation == NullableAnnotation.Annotated
                 })
                 .Where(t => !t.isNullable && t.param.DefaultValue == null)
                 .Select(t => t.param.SelectedService)) {
      // Check the selected service type if available
      if (serviceRegistration is null) continue;

      var serviceType = serviceRegistration.ResolvedType;
      if (DetectCycle(serviceType, visited, path, onPath, out cycle)) {
        return true;
      }
    }
    return false;
  }
  private static bool ExtractCycleFromPath(ITypeSymbol type, Stack<ITypeSymbol> path, out List<ITypeSymbol> cycle) {
    // We found a cycle
    cycle = [];
    var cycleStarted = false;
      
    // Extract the cycle from the path
    foreach (var node in path.Reverse()) {
      if (TypeSymbolEqualityComparer.Instance.Equals(node, type)) {
        cycleStarted = true;
      }
        
      if (cycleStarted) {
        cycle.Add(node);
      }
    }
      
    cycle.Add(type); // Complete the cycle
    return true;
  }

  public void AddConstructorResolution(ConstructorResolution resolution) {
    _constructorResolutions[resolution.Type] = resolution;
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
  public ServiceRegistration AddService(ITypeSymbol serviceType, ServiceScope lifetime, ITypeSymbol? implementationType = null,
                                        ISymbol? associatedSymbol = null, string? key = null, List<ServiceRegistration>? collectedServices = null) {
    if (!_services.TryGetValue(serviceType, out var registrations)) {
      registrations = [];
      _services[serviceType] = registrations;
    }

    var registration = new ServiceRegistration {
        Type = serviceType,
        Key = key,
        Lifetime = lifetime,
        ImplementationType =
            implementationType is null || implementationType.Equals(serviceType, SymbolEqualityComparer.Default)
                ? null
                : implementationType,
        IndexForType = registrations
            .Count(x => x.ImplementationType is not INamedTypeSymbol { IsGenericType: true } namedType 
                        || !namedType.TypeArguments.Any(y => y is ITypeParameterSymbol)),
        AssociatedSymbol = associatedSymbol,
        CollectedServices = collectedServices,
        IsDisposable = serviceType.AllInterfaces.Any(i => i.IsOfType<IDisposable>()),
        IsAsyncDisposable = serviceType.AllInterfaces.Any(i => i.ToDisplayString() == "System.IAsyncDisposable")
    };
    registrations.Add(registration);

    return registration;
  }

  /// <summary>
  /// Retrieves all service registrations.
  /// </summary>
  /// <returns>
  /// An enumerable collection of service registrations.
  /// </returns>
  public IEnumerable<ServiceRegistration> GetAllServices() {
    return _services.Values
        .SelectMany(list => list.Where(x => x.ResolvedType is not INamedTypeSymbol { IsGenericType: true } generic ||
                                            generic.TypeArguments.All(y => y is not ITypeParameterSymbol)));
  }

  /// <summary>
  /// Retrieves all service registrations that match the specified service lifetime.
  /// </summary>
  /// <param name="lifetime">The desired service lifetime for filtering the service registrations.</param>
  /// <returns>An enumerable collection of service registrations matching the specified lifetime.</returns>
  public IEnumerable<ServiceRegistration> GetServicesByLifetime(ServiceScope lifetime) {
    return GetAllServices().Where(reg => reg.Lifetime == lifetime);
  }

  public bool TryGetServices(ITypeSymbol serviceType, [NotNullWhen(true)] out List<ServiceRegistration> services) {
    return _services.TryGetValue(serviceType, out services);
  }
}