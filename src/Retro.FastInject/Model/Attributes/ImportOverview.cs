using Microsoft.CodeAnalysis;
using Retro.FastInject.Annotations;
using Retro.SourceGeneratorUtilities.Utilities.Attributes;
namespace Retro.FastInject.Model.Attributes;

/// <summary>
/// Represents an overview of a dependency import, defining the module type required for dependency injection.
/// </summary>
/// <param name="ModuleType">
/// The symbol representing the type of the module to be imported.
/// </param>
[AttributeInfoType<ImportAttribute>]
public record ImportOverview(ITypeSymbol ModuleType) {

  /// <summary>
  /// Gets or sets a value indicating whether dynamic registrations are allowed
  /// for the associated dependency module. When set to <c>true</c>, dependencies
  /// can be dynamically registered during runtime, enabling more flexible
  /// dependency injection scenarios.
  /// </summary>
  public bool AllowDynamicRegistrations { get; init; }
  
}

/// <summary>
/// Represents an overview for importing a single parameter type required for dependency injection.
/// Inherits the base import overview functionality while allowing the specification of a module type.
/// </summary>
/// <param name="ModuleType">
/// The symbol representing the type of the module to be imported.
/// </param>
[AttributeInfoType(typeof(ImportAttribute<>))]
public record ImportOneParamOverview(ITypeSymbol ModuleType) : ImportOverview(ModuleType);