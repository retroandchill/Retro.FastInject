using System.Linq;
using Microsoft.CodeAnalysis;
using Retro.FastInject.Model.Detection;
using Retro.FastInject.Model.Manifest;

namespace Retro.FastInject.Generation;

/// <summary>
/// Responsible for generating a service manifest that organizes and maps services, their implementations,
/// lifetimes, base types, and indirect relationships within a dependency injection framework.
/// </summary>
public static class ServiceManifestGenerator {
  /// <summary>
  /// Generates a manifest that maps services to their implementations, lifetimes, base types, and indirect relationships.
  /// </summary>
  /// <param name="services">A collection of service declarations representing the services and their associated metadata.</param>
  /// <returns>A <see cref="ServiceManifest"/> object containing comprehensive mappings of services, implementations, and related dependencies.</returns>
  public static ServiceManifest GenerateManifest(this in ServiceDeclarationCollection services) {
    // Get the dependencies dictionary
    var dependencies = services.GetDependencies();

    var manifest = new ServiceManifest {
        AllowDynamicResolution = services.AllowDynamicServices
    };

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
    }

    return manifest;
  }
}