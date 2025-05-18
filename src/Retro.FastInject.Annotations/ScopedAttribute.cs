using System;
namespace Retro.FastInject.Annotations;

/// <summary>
/// Specifies that a class, struct, or interface is registered with a scoped lifecycle in the dependency injection container.
/// </summary>
/// <remarks>
/// Applying this attribute indicates that the type should be instantiated once per scope.
/// Different scopes typically correspond to specific contexts, such as a single web request in web applications.
/// This attribute can be applied multiple times to allow registration for different service types.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = true)]
public class ScopedAttribute(Type serviceType) : DependencyAttribute(serviceType, ServiceScope.Scoped);

/// <summary>
/// Attribute specifying that the decorated type is registered with a scoped lifecycle in a dependency injection container.
/// </summary>
/// <remarks>
/// This attribute is used to indicate that the associated type should be instantiated once per scope.
/// Scopes often represent specific contextual lifetimes, such as a web request or an operation.
/// Can be applied multiple times to facilitate registration for different service types.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = true)]
public class ScopedAttribute<TService>() : ScopedAttribute(typeof(TService));