using System;
namespace Retro.FastInject.Annotations;

/// <summary>
/// Specifies that the attributed class, struct, or interface should be registered as a singleton
/// in a dependency injection container.
/// </summary>
/// <remarks>
/// Services marked with this attribute are instantiated once and shared throughout the application lifetime.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = true)]
public class SingletonAttribute(Type serviceType) : DependencyAttribute(serviceType, ServiceScope.Singleton);

/// <summary>
/// Defines an attribute to indicate that the attributed class, struct, or interface should be registered as a singleton
/// in a dependency injection container.
/// </summary>
/// <remarks>
/// This attribute is used to declare a service that will be instantiated only once and shared throughout
/// the lifetime of the application. It enables dependency injection systems to manage singleton lifetimes.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = true)]
public class SingletonAttribute<TService>() : SingletonAttribute(typeof(TService));