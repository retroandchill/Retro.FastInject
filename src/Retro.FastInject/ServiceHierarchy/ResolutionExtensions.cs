namespace Retro.FastInject.ServiceHierarchy;

/// <summary>
/// Provides extension methods for resolving and manipulating parameter resolution details
/// used in the dependency injection service hierarchy.
/// </summary>
public static class ResolutionExtensions {
  /// <summary>
  /// Retrieves the default value of the argument for the provided parameter resolution.
  /// </summary>
  /// <param name="parameterResolution">The parameter resolution from which to obtain the default value.</param>
  /// <returns>
  /// A string representing the default value of the argument. If a default value is defined, it is returned.
  /// Otherwise, a service resolution expression or "null" is returned based on the context.
  /// </returns>
  public static string GetArgDefaultValue(this ParameterResolution parameterResolution) {
    if (parameterResolution.SelectedService is null || parameterResolution.DefaultValue is not null) {
      return parameterResolution.DefaultValue ?? "null";
    }
              
    var serviceType = parameterResolution.SelectedService.Type;
    return parameterResolution.SelectedService.IndexForType > 0 
        ? $"this.Get{serviceType.Name}_{parameterResolution.SelectedService.IndexForType}()" 
        : $"((IServiceProvider<{serviceType.ToDisplayString()}>) this).GetService()";
  }
}