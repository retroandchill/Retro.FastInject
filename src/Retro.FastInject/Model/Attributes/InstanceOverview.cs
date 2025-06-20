using Retro.FastInject.Annotations;
using Retro.SourceGeneratorUtilities.Utilities.Attributes;
namespace Retro.FastInject.Model.Attributes;

/// <summary>
/// Represents an overview of an instance and serves as a model for mapping specific
/// instance information. Used in conjunction with the <c>InstanceAttribute</c>
/// to describe instance dependencies managed in a dependency injection framework.
/// </summary>
/// <remarks>
/// The <c>InstanceOverview</c> class provides essential details about an instance,
/// primarily focusing on its identifying <c>Key</c>. This is useful for systems that
/// operate with uniquely identified dependency instances.
/// </remarks>
[AttributeInfoType<InstanceAttribute>]
public record InstanceOverview {

  /// <summary>
  /// Gets the unique identifier or key associated with the instance.
  /// </summary>
  /// <remarks>
  /// The <c>Key</c> property provides a unique identifier used to distinguish
  /// this instance within the dependency injection framework. It is particularly
  /// useful in scenarios where multiple instances of the same type need to be
  /// differentiated.
  /// </remarks>
  public string? Key { get; init; }
  
}