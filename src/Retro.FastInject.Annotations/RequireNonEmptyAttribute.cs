using System;
namespace Retro.FastInject.Annotations;

/// <summary>
/// Indicates that the target parameter must not be associated with an empty collection.
/// </summary>
/// <remarks>
/// This attribute can be applied to method parameters to enforce that the corresponding
/// collection, if applicable, should not be empty at runtime. If the specified parameter
/// has this attribute and the associated collection is empty, the resolution process
/// may fail based on the logic implemented in the consuming application.
/// </remarks>
/// <example>
/// This attribute is commonly used in conjunction with dependency injection to ensure
/// that services of a specific type are available and non-empty.
/// </example>
[AttributeUsage(AttributeTargets.Parameter)]
public class RequireNonEmptyAttribute : Attribute;