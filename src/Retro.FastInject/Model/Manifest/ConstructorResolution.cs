using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Retro.FastInject.Model.Manifest;

/// <summary>
/// Records all service resolutions for a constructor.
/// </summary>
public class ConstructorResolution {
  /// <summary>
  /// The constructor that was resolved
  /// </summary>
  public IMethodSymbol Constructor { get; set; } = null!;

  /// <summary>
  /// The type the constructor belongs to
  /// </summary>
  public ITypeSymbol Type { get; set; } = null!;

  /// <summary>
  /// All parameter resolutions for this constructor
  /// </summary>
  public List<ParameterResolution> Parameters { get; } = [];
}