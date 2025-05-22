using System.Collections.Generic;
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
  /// The default value for the parameter, if specified.
  /// </summary>
  public string? DefaultValue { get; set; }

  /// <summary>
  /// Indicates whether the parameter resolution has no associated declaration
  /// available for the requested service or type.
  /// </summary>
  public bool HasNoDeclaration { get; set; }

  /// <summary>
  /// Indicates whether the parameter has multiple service registrations available for resolution.
  /// </summary>
  public bool HasMultipleRegistrations { get; set; }

  /// <summary>
  /// A collection of service registrations associated with a parameter
  /// when multiple service registrations are available.
  /// </summary>
  public List<ServiceRegistration> MultipleServices { get; set; } = [];
}