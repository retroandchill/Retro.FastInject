using System;
using Microsoft.CodeAnalysis;

namespace Retro.FastInject.Utils;

/// <summary>
/// Provides extension methods for <see cref="IParameterSymbol"/> objects.
/// </summary>
public static class ParameterExtensions {
  /// <summary>
  /// Retrieves the default value of the parameter as a string representation.
  /// </summary>
  /// <param name="parameter">
  /// The parameter symbol from which to retrieve the default value.
  /// </param>
  /// <returns>
  /// A string representation of the parameter's default value, or <c>null</c> if the parameter does not have an explicit default value.
  /// </returns>
  public static string? GetDefaultValueString(this IParameterSymbol parameter) {
    if (!parameter.HasExplicitDefaultValue) {
      return null;
    }

    var value = parameter.ExplicitDefaultValue;

    switch (value) {
      // Handle null
      case null:
        return "null";
      // Handle strings
      case string str:
        return $"\"{str.Replace("\"", "\\\"")}\"";
      // Handle chars
      case char c:
        return $"'{c}'";
      // Handle boolean
      case bool b:
        return b ? "true" : "false";
      // Handle numeric types
      case IFormattable:
        return value switch {
            // Special handling for decimal
            decimal m => $"{m}m",
            // Special handling for float
            float f => $"{f}f",
            // Special handling for double
            double d => $"{d}d",
            // Special handling for long
            long l => $"{l}L",
            _ => value.ToString()
        };
    }

    // Handle enums
    if (parameter.Type.TypeKind == TypeKind.Enum)
      return $"{parameter.Type.ToDisplayString()}.{value}";

    // Handle value types with default
    return parameter.Type.IsValueType ? $"default({parameter.Type.ToDisplayString()})" : "null";
  }
}