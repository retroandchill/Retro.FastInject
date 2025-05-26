using System;
namespace Retro.FastInject.Annotations;

/// <summary>
/// Specifies that a class or struct requires the import of another type for dependency injection purposes.
/// </summary>
/// <remarks>
/// This attribute is used to indicate that the decorated type depends on the specified module type
/// and should import its services and dependencies. The attribute supports optional dynamic registration of dependencies.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public class ImportAttribute(Type moduleType) : Attribute {

  /// <summary>
  /// Represents the type of the module that is required to be imported by the annotated class or struct
  /// for dependency injection purposes. This property provides the specific type of the module whose
  /// services and dependencies should be made available to the decorated type.
  /// </summary>
  public Type ModuleType { get; } = moduleType;
  
  /// <summary>
  /// Specifies whether dynamic registrations are allowed for the associated property or field.
  /// A value of `true` enables dynamic registrations, while a value of `false` disables them.
  /// The default value is `false`. Any occurrence of the value `true` in the service provider declaration
  /// will enable dynamic registrations.
  /// </summary>
  public bool AllowDynamicRegistrations { get; init; } = false;
  
}

/// <summary>
/// Marks a class or struct as requiring the import of a specific type for dependency injection.
/// </summary>
/// <remarks>
/// This attribute allows the specification of a dependency on a module or type.
/// It can be used with type arguments to indicate the required dependency. Additionally,
/// it supports configurable dynamic registration to facilitate more flexible dependency management.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public class ImportAttribute<TModule>() : ImportAttribute(typeof(TModule));
