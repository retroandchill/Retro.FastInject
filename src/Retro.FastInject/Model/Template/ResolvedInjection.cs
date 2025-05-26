using Microsoft.CodeAnalysis;
using Retro.FastInject.Generation;
using Retro.FastInject.Model.Manifest;
using Retro.FastInject.Utils;
namespace Retro.FastInject.Model.Template;

/// <summary>
/// Represents a resolved injection for a service, containing the service name, type,
/// and an optional index when applicable.
/// </summary>
public record ResolvedInjection {
  /// <summary>
  /// Gets the name of the service associated with the resolved injection.
  /// This property identifies the specific service within the dependency injection context.
  /// It is typically derived from the type or a provided key during registration.
  /// </summary>
  public required string ServiceName { get; init; }

  /// <summary>
  /// Gets the type of the service associated with the resolved injection.
  /// This property specifies the fully qualified type name of the service
  /// and is typically derived from the service registration details.
  /// </summary>
  public required string ServiceType { get; init; }

  /// <summary>
  /// Gets the optional index associated with the resolved injection.
  /// This property indicates the ordinal position of the service within a collection
  /// of services of the same type, when applicable. It is null if no index is assigned.
  /// </summary>
  public required int? Index { get; init; }

  /// <summary>
  /// Determines whether the resolved injection represents a collection of services.
  /// This property is set based on the type, identifying if the service type
  /// is a generic collection.
  /// </summary>
  public bool IsCollection { get; init; }

  /// <summary>
  /// Indicates whether dynamic resolution is enabled for this injection.
  /// When set to true, the service resolution will leverage dynamic runtime mechanisms
  /// instead of precompiled static logic, allowing greater flexibility for complex scenarios.
  /// </summary>
  public bool UseDynamic { get; init; }

  /// <summary>
  /// Creates a ResolvedInjection instance from the specified ServiceRegistration and configuration.
  /// </summary>
  /// <param name="registration">The service registration containing details about the type, name, and index of the service.</param>
  /// <param name="useDynamic">Indicates whether dynamic resolution should be used for this instance.</param>
  /// <returns>An instance of ResolvedInjection with properties populated based on the provided service registration and configuration.</returns>
  public static ResolvedInjection FromRegistration(ServiceRegistration registration, bool useDynamic) {
    return new ResolvedInjection {
        ServiceName = registration.Type.GetSanitizedTypeName(),
        ServiceType = registration.Type.ToDisplayString(),
        Index = registration.IndexForType > 0 ? registration.IndexForType : null,
        IsCollection = registration.Type is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsGenericCollectionType(),
        UseDynamic = useDynamic
    };
  }
}