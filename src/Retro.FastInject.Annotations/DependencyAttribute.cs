using System;
namespace Retro.FastInject.Annotations;

/// <summary>
/// An attribute used to define a dependency registration for a type in a dependency injection container.
/// </summary>
/// <remarks>
/// This attribute is applied to classes, structs, or interfaces to specify the service type and the lifecycle scope.
/// It can be used multiple times on the same type to register it for different services or configurations.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = true)]
public class DependencyAttribute(Type type, ServiceScope scope) : Attribute {

  /// <summary>
  /// Gets the type that represents the service or implementation being registered in a dependency injection container.
  /// </summary>
  /// <remarks>
  /// This property defines the type associated with the dependency registration.
  /// It specifies the type that the dependency injection container will use to resolve or inject the service.
  /// </remarks>
  /// <value>
  /// The <see cref="System.Type"/> of the service or implementation being registered.
  /// </value>
  public Type Type { get; } = type;

  /// <summary>
  /// Gets the lifecycle scope of the dependency registration in a dependency injection container.
  /// </summary>
  /// <remarks>
  /// This property specifies the scope in which the service instance will be resolved within
  /// the dependency injection container. It defines whether the service is created as a singleton,
  /// per scope, or transient based on the <see cref="Retro.FastInject.Annotations.ServiceScope"/> enumeration.
  /// </remarks>
  /// <value>
  /// The <see cref="Retro.FastInject.Annotations.ServiceScope"/> specifying the scope of the service.
  /// </value>
  public ServiceScope Scope { get; } = scope;

  /// <summary>
  /// Gets or sets the unique string key associated with a registered service in the dependency injection container.
  /// </summary>
  /// <remarks>
  /// This property is used to distinguish different registrations of the same service type within the container.
  /// It allows resolving or injecting a specific instance of a service when multiple registrations exist, each associated with a unique key.
  /// </remarks>
  /// <value>
  /// A <see cref="System.String"/> representing the key for identifying a particular registration in the container.
  /// </value>
  public string? Key { get; init; }

}