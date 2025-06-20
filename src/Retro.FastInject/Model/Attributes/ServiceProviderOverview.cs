using Retro.FastInject.Annotations;
using Retro.SourceGeneratorUtilities.Utilities.Attributes;
namespace Retro.FastInject.Model.Attributes;

/// <summary>
/// Represents an overview of the service provider that defines settings
/// related to dependency injection and registrations within an application.
/// </summary>
/// <remarks>
/// This class provides configuration metadata, such as whether dynamic registrations
/// are allowed, for a service provider implementation in the context of the
/// Retro.FastInject framework.
/// </remarks>
[AttributeInfoType<ServiceProviderAttribute>]
public record ServiceProviderOverview {

  /// <summary>
  /// Determines whether the service provider allows dynamic registrations
  /// during runtime.
  /// </summary>
  /// <remarks>
  /// This property controls the ability of the service provider to accept
  /// and handle registrations dynamically, which can influence the application's
  /// flexibility and runtime behavior.
  /// </remarks>
  public bool AllowDynamicRegistrations { get; init; }
  
}