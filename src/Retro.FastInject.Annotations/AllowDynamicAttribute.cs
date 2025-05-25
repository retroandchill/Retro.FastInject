using System;
namespace Retro.FastInject.Annotations;

/// <summary>
/// Represents an attribute that allows dynamic resolution of a parameter during runtime.
/// </summary>
/// <remarks>
/// The AllowDynamicAttribute can be applied to parameters to indicate that these
/// parameters should resolve dynamically using a dependency injection mechanism or
/// similar runtime resolution strategies.
/// </remarks>
/// <example>
/// This attribute may be used in scenarios where the type or behavior of the resolved
/// service is determined at runtime, such as resolving services from key-based factories or dynamic providers.
/// </example>
[AttributeUsage(AttributeTargets.Parameter)]
public class AllowDynamicAttribute : Attribute;