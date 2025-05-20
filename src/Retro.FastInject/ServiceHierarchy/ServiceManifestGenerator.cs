using System.Linq;
using Microsoft.CodeAnalysis;

namespace Retro.FastInject.ServiceHierarchy;

/// <summary>
/// Responsible for generating a service manifest that organizes and maps services, their implementations,
/// lifetimes, base types, and indirect relationships within a dependency injection framework.
/// </summary>
public static class ServiceManifestGenerator {
  /// <summary>
  /// Generates a manifest that maps services to their implementations, lifetimes, base types, and indirect relationships.
  /// </summary>
  /// <param name="classSymbol">An instance of <see cref="ITypeSymbol"/> representing the class type whose services and dependencies will be analyzed.</param>
  /// <returns>A <see cref="ServiceManifest"/> object containing detailed mappings of services, implementations, and dependencies.</returns>
  public static ServiceManifest GenerateManifest(this ITypeSymbol classSymbol) {
    // Get all services using GetInjectedServices
    var services = classSymbol.GetInjectedServices().ToList();

    // Get the dependencies dictionary
    var dependencies = services.GetDependencies();

    var manifest = new ServiceManifest();

    // Process all services and their dependencies
    foreach (var serviceKvp in dependencies) {
      var serviceType = serviceKvp.Key;
      var implementations = serviceKvp.Value;

      // Skip special injectable types (like IServiceProvider)
      if (serviceType.IsSpecialInjectableType()) {
        continue;
      }

      // Add each implementation
      foreach (var implementation in implementations) {
        manifest.AddService(
            serviceType: serviceType,
            implementationType: implementation.Type,
            lifetime: implementation.Lifetime,
            key: implementation.Key,
            associatedSymbol: implementation.AssociatedSymbol
        );
      }

      // If this is an indirect service (interface -> implementation),
      // add the relationship
      var distinctTypes = implementations.Select(x => x.Type)
          .Distinct(TypeSymbolEqualityComparer.Instance)
          .ToList();
      if (distinctTypes.Count == 1 && !serviceType.Equals(distinctTypes[0], SymbolEqualityComparer.Default)) {
        manifest.AddIndirectService(serviceType, distinctTypes[0]);
      }
    }

    return manifest;
  }
}