using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Retro.FastInject.Annotations;
using Retro.FastInject.Comparers;
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
  /// Indicates whether the system should allow dynamic resolution of services.
  /// </summary>
  /// <remarks>
  /// When set to <c>true</c>, dynamic resolution enables the system to resolve services
  /// at runtime, including cases where specific attributes (e.g., <c>AllowDynamicAttribute</c>)
  /// are used on parameters. This provides flexibility for resolving dependencies that
  /// are not registered explicitly but might be handled dynamically.
  /// By default, dynamic resolution is disabled, requiring all dependencies to be
  /// explicitly registered and resolved at compilation time.
  /// </remarks>
  public bool AllowDynamicResolution { get; init; }

  /// <summary>
  /// Gets all constructor resolutions that have been recorded.
  /// </summary>
  public IEnumerable<ConstructorResolution> GetAllConstructorResolutions() {
    return _constructorResolutions.Values;
  }

  /// <summary>
  /// Adds a constructor resolution entry for a service type.
  /// </summary>
  /// <param name="resolution">The constructor resolution to be added, containing the type, constructor, and parameters.</param>
  public void AddConstructorResolution(ConstructorResolution resolution) {
    _constructorResolutions[resolution.Type] = resolution;
  }

  /// <summary>
  /// Attempts to retrieve a constructor resolution for the specified type.
  /// </summary>
  /// <param name="type">The type symbol for which to retrieve the constructor resolution.</param>
  /// <param name="resolution">
  /// When this method returns, contains the constructor resolution associated with the specified type
  /// if the resolution exists; otherwise, null.
  /// </param>
  /// <returns>
  /// True if the constructor resolution for the specified type exists, otherwise false.
  /// </returns>
  public bool TryGetConstructorResolution(ITypeSymbol type, [NotNullWhen(true)] out ConstructorResolution? resolution) {
    return _constructorResolutions.TryGetValue(type, out resolution);
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

  /// <summary>
  /// Attempts to retrieve a list of service registrations for a given service type.
  /// </summary>
  /// <param name="serviceType">The type of the service to retrieve registrations for.</param>
  /// <param name="services">Outputs the list of service registrations if found; otherwise, null.</param>
  /// <returns>True if services were found for the given service type; otherwise, false.</returns>
  public bool TryGetServices(ITypeSymbol serviceType, [NotNullWhen(true)] out List<ServiceRegistration> services) {
    return _services.TryGetValue(serviceType, out services);
  }
}