using System;

namespace Retro.FastInject.Annotations;

/// <summary>
/// Specifies that a particular property or field represents an instance dependency
/// to be provided or managed by the dependency injection framework.
/// </summary>
/// <remarks>
/// The <c>InstanceAttribute</c> is primarily used to mark properties or fields
/// in classes that are to be injected or resolved as single-instance dependencies.
/// It is applied to ensure the annotated element is uniquely managed within
/// the dependency injection container.
/// </remarks>
/// <example>
/// Apply the <c>InstanceAttribute</c> on a property or field to designate it
/// as an instance dependency.
/// </example>
/// <seealso cref="Attribute"/>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class InstanceAttribute : Attribute {

  /// <summary>
  /// Gets or initializes the unique identifier associated with the instance dependency.
  /// </summary>
  /// <remarks>
  /// This property can be used to specify a distinct key for the annotated dependency,
  /// enabling differentiation of multiple instances managed by the dependency injection container.
  /// It is particularly useful when resolving dependencies with the same type but requiring specific
  /// identification within the dependency injection configuration.
  /// </remarks>
  public string? Key { get; init; }
  
}