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
  
  public bool IsCollection { get; init; }
  
  public bool UseDynamic { get; init; }

  /// <summary>
  /// Creates a <see cref="ResolvedInjection"/> instance from a given <see cref="ServiceRegistration"/>.
  /// </summary>
  /// <param name="registration">The service registration containing information about the service type, name, and index.</param>
  /// <returns>An instance of <see cref="ResolvedInjection"/> populated with information from the provided service registration.</returns>
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