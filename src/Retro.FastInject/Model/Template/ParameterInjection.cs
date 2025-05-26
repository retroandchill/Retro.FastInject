using Microsoft.CodeAnalysis;
using Retro.FastInject.Generation;
using Retro.FastInject.Model.Manifest;

namespace Retro.FastInject.Model.Template;

/// <summary>
/// Represents a parameter resolution for dependency injection, containing information 
/// about a parameter and how it should be resolved.
/// </summary>
public record ParameterInjection {
  /// <summary>
  /// Gets or sets the type of the parameter.
  /// </summary>
  public required string ParameterType { get; init; }

  /// <summary>
  /// Gets or sets the name of the parameter.
  /// </summary>
  public required string ParameterName { get; init; }

  /// <summary>
  /// Gets or sets the selected service for this parameter, if any.
  /// </summary>
  public ResolvedInjection? SelectedService { get; init; }

  /// <summary>
  /// Gets a value indicating whether the parameter is associated with a key for resolution.
  /// </summary>
  public bool WithKey => Key is not null;

  /// <summary>
  /// Gets or sets the key used for resolving the parameter during dependency injection.
  /// </summary>
  public string? Key { get; init; }

  /// <summary>
  /// Gets a value indicating whether the parameter has a default value specified.
  /// </summary>
  public bool HasDefaultValue => DefaultValue is not null;

  /// <summary>
  /// Gets or sets the default value for this parameter, if any.
  /// </summary>
  public string? DefaultValue { get; init; }

  /// <summary>
  /// Gets a value indicating whether the parameter represents a generic collection type.
  /// The determination is based on the parameter's type metadata.
  /// </summary>
  public bool IsCollection { get; init; }

  /// <summary>
  /// Gets or sets a value indicating whether this parameter should use dynamic resolution.
  /// </summary>
  public bool UseDynamic { get; init; }

  /// <summary>
  /// Indicates whether the parameter resolution should be treated as a lazy-loaded dependency,
  /// meaning its instantiation will be deferred until it is accessed.
  /// </summary>
  public bool IsLazy { get; init; }

  /// <summary>
  /// Gets or sets a value indicating whether this parameter type is nullable.
  /// </summary>
  public bool IsNullable { get; init; }

  /// <summary>
  /// Indicates whether the parameter injection is the last in the sequence of parameters.
  /// </summary>
  public bool IsLast { get; init; }

  /// Creates a new instance of the <see cref="ParameterInjection"/> class based on the given <see cref="ParameterResolution"/> object.
  /// <param name="parameter">
  /// The parameter resolution from which to construct the parameter injection.
  /// </param>
  /// <param name="isLast">
  /// Indicates whether this parameter is the last in its containing collection.
  /// </param>
  /// <returns>
  /// A new <see cref="ParameterInjection"/> object populated with data from the provided parameter resolution.
  /// </returns>
  public static ParameterInjection FromResolution(ParameterResolution parameter, bool isLast) {
    var baseParameterType = parameter.ParameterType is INamedTypeSymbol { IsGenericType: true} genericType && genericType.IsLazyType() ? genericType.TypeArguments[0] : parameter.ParameterType;
    
    return new ParameterInjection {
        ParameterType = baseParameterType.ToDisplayString(),
        ParameterName = parameter.Parameter.Name,
        SelectedService = parameter.SelectedService is not null ? ResolvedInjection.FromRegistration(parameter.SelectedService, parameter.UseDynamic) : null,
        Key = parameter.Key,
        DefaultValue = parameter.DefaultValue,
        IsCollection = baseParameterType is INamedTypeSymbol namedType && namedType.IsGenericCollectionType(),
        UseDynamic = parameter.UseDynamic,
        IsLazy = parameter.IsLazy,
        IsNullable = parameter.IsNullable,
        IsLast = isLast
    };
  }

}