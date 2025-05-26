using Microsoft.CodeAnalysis;
using Retro.FastInject.Model.Manifest;

namespace Retro.FastInject.Model.Template;

/// <summary>
/// Represents a parameter resolution for dependency injection, containing information 
/// about a parameter and how it should be resolved.
/// </summary>
public record ParameterInjection
{
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
    
    public bool WithKey => Key is not null;
    
    public string? Key { get; init; }
    
    public bool HasDefaultValue => DefaultValue is not null;

    /// <summary>
    /// Gets or sets the default value for this parameter, if any.
    /// </summary>
    public string? DefaultValue { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this parameter should use dynamic resolution.
    /// </summary>
    public bool UseDynamic { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this parameter type is nullable.
    /// </summary>
    public bool IsNullable { get; init; }

    public bool IsLast { get; init; }

    public static ParameterInjection FromResolution(ParameterResolution parameter, bool isLast)
    {
        return new ParameterInjection {
            ParameterType = parameter.ParameterType.ToDisplayString(),
            ParameterName = parameter.Parameter.Name,
            SelectedService = parameter.SelectedService is not null ? ResolvedInjection.FromRegistration(parameter.SelectedService) : null,
            Key = parameter.Key,
            DefaultValue = parameter.DefaultValue,
            UseDynamic = parameter.UseDynamic,
            IsNullable = parameter.IsNullable,
            IsLast = isLast
        };
    }
  
}
