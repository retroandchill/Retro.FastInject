using Retro.FastInject.Annotations;
using Retro.SourceGeneratorUtilities.Utilities.Attributes;
namespace Retro.FastInject.Model.Attributes;

/// <summary>
/// Represents an overview for factory-related configurations in the dependency injection system.
/// </summary>
/// <remarks>
/// This class is a record which is designed to hold factory-specific configurations, including the
/// scope of the service and an optional unique key for identifying specific configurations. It is
/// annotated with the <c>FactoryAttribute</c> to indicate its purpose in association with factory methods.
/// </remarks>
/// <param name="Scope">
/// Specifies the lifecycle scope of the service within the dependency injection container.
/// </param>
[AttributeInfoType<FactoryAttribute>]
public record FactoryOverview(ServiceScope Scope) {

  /// <summary>
  /// Gets or initializes the configuration key associated with this factory overview.
  /// </summary>
  /// <remarks>
  /// This property represents an optional identifier used to distinguish or configure services
  /// instantiated by a factory within the dependency injection system. The key can be utilized
  /// to facilitate custom logic or resolve ambiguities when multiple services share the same scope.
  /// </remarks>
  public string? Key { get; init; }
  
}