using Microsoft.Extensions.DependencyInjection;
using Retro.SourceGeneratorUtilities.Utilities.Attributes;
namespace Retro.FastInject.Model.Attributes;

/// <summary>
/// Represents a model attribute that encapsulates a keyed service.
/// </summary>
/// <remarks>
/// This record is designed to handle dependency injection scenarios where services are associated with specific keys.
/// It provides functionality to initialize and retrieve the key associated with the service.
/// </remarks>
[AttributeInfoType<FromKeyedServicesAttribute>]
public record FromKeyedServicesOverview {

  /// <summary>
  /// Represents the key associated with a keyed service in dependency injection.
  /// </summary>
  /// <remarks>
  /// Used to identify and resolve specific services that are registered with a key.
  /// This property is typically initialized with the key value provided during the associated service's registration.
  /// </remarks>
  public string? Key { get; init; }

  /// <summary>
  /// Represents an overview of a keyed service used in dependency injection.
  /// </summary>
  /// <remarks>
  /// This record is utilized in scenarios where a service is bound to a specific key.
  /// It abstracts the initialization process by storing the key as a string representation.
  /// </remarks>
  public FromKeyedServicesOverview(object? key) {
    Key = key?.ToString();
  }
  
}