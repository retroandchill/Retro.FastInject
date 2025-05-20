using System;
using System.Linq;
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
    
    // Handle enums
    if (parameter.Type.TypeKind == TypeKind.Enum) {
      var enumType = (INamedTypeSymbol)parameter.Type;
      var enumMembers = enumType.GetMembers().OfType<IFieldSymbol>()
          .Where(f => f.HasConstantValue);
        
      var enumMember = enumMembers.FirstOrDefault(m => 
                                                      Equals(m.ConstantValue, parameter.ExplicitDefaultValue));
        
      return enumMember != null ? $"{parameter.Type.ToDisplayString()}.{enumMember.Name}" :
          // Fallback to numeric value if no matching member is found
          $"({parameter.Type.ToDisplayString()}){value}";
    }

    return value switch {
        // Handle strings
        string str => $"\"{str.Replace("\"", "\\\"")}\"",
        // Handle chars
        char c => $"'{c}'",
        // Handle boolean
        bool b => b ? "true" : "false",
        // Handle numeric types
        IFormattable and not Enum => value switch {
            // Special handling for decimal
            decimal m => $"{m}m",
            // Special handling for float
            float f => $"{f}f",
            // Special handling for double
            double d => $"{d}d",
            // Special handling for long
            long l => $"{l}L",
            _ => value.ToString()
        },
        // Handle value types with default
        _ => parameter.Type.IsValueType ? $"default({parameter.Type.ToDisplayString()})" : "null"
    };
  }
}