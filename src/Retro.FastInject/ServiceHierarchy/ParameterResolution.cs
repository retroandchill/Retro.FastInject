using Microsoft.CodeAnalysis;

namespace Retro.FastInject.ServiceHierarchy;

/// <summary>
/// Records a service resolution for a constructor parameter.
/// </summary>
public class ParameterResolution {
  /// <summary>
  /// The parameter symbol
  /// </summary>
  public IParameterSymbol Parameter { get; set; } = null!;

  /// <summary>
  /// The type being injected
  /// </summary>
  public ITypeSymbol ParameterType { get; set; } = null!;

  /// <summary>
  /// Key used for resolution, null for non-keyed services
  /// </summary>
  public string? Key { get; set; }

  /// <summary>
  /// The service registration that was selected for this parameter
  /// </summary>
  public ServiceRegistration? SelectedService { get; set; }

  /// <summary>
  /// Whether the parameter was resolved through an indirect service
  /// </summary>
  public bool IsIndirectResolution { get; set; }

  /// <summary>
  /// The indirect implementation type if IsIndirectResolution is true
  /// </summary>
  public ITypeSymbol? IndirectImplementationType { get; set; }
}