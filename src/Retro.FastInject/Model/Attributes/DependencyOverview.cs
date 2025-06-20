using Microsoft.CodeAnalysis;
using Retro.FastInject.Annotations;
using Retro.SourceGeneratorUtilities.Utilities.Attributes;

namespace Retro.FastInject.Model.Attributes;

/// <summary>
/// Represents an overview of a dependency in the dependency injection system.
/// </summary>
/// <param name="Type">
/// The type symbol representing the dependency being registered.
/// </param>
/// <param name="Scope">
/// The service scope that determines the lifetime of the dependency (e.g., Singleton, Scoped, Transient).
/// </param>
[AttributeInfoType<DependencyAttribute>]
public record DependencyOverview(ITypeSymbol Type, ServiceScope Scope) {
  /// <summary>
  /// Gets or initializes the key associated with the dependency overview.
  /// This key can be utilized to uniquely identify a dependency
  /// within the dependency injection system.
  /// </summary>
  public string? Key { get; init; }
}