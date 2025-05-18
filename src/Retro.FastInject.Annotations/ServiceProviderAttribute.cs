using System;
namespace Retro.FastInject.Annotations;

/// <summary>
/// An attribute indicating that the adorned class serves as a service provider
/// within the Retro.FastInject dependency injection framework. The attribute
/// marks classes responsible for managing dependency registrations and resolutions.
/// </summary>
/// <remarks>
/// The <c>ServiceProviderAttribute</c> is typically used to annotate classes that
/// define service registrations using additional attributes like
/// <c>SingletonAttribute</c>, <c>ScopedAttribute</c>, or <c>TransientAttribute</c>.
/// </remarks>
/// <example>
/// This attribute is applied to a class to designate it as a provider for dependency
/// injection services. Within the provider, additional registration attributes such
/// as <c>SingletonAttribute</c> can be used to specify service lifetimes.
/// </example>
[AttributeUsage(AttributeTargets.Class)]
public class ServiceProviderAttribute : Attribute;