using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Retro.FastInject.Utils;

/// <summary>
/// Provides extension methods for handling attributes in .NET code analysis.
/// </summary>
public static class AttributeExtensions {

  /// <summary>
  /// Determines whether the specified attribute is of the given attribute type.
  /// </summary>
  /// <typeparam name="T">The type of the attribute to check. Must inherit from <see cref="System.Attribute"/>.</typeparam>
  /// <param name="attribute">The attribute data to inspect.</param>
  /// <returns>
  /// <c>true</c> if the attribute is of the specified type; otherwise, <c>false</c>.
  /// </returns>
  public static bool IsOfAttributeType<T>(this AttributeData attribute) where T : Attribute {
    return attribute.AttributeClass?.IsOfType<T>() ?? false;
  }

  /// <summary>
  /// Retrieves the attribute of the specified type applied to the given symbol, if any.
  /// </summary>
  /// <typeparam name="T">The type of the attribute to retrieve. Must inherit from <see cref="System.Attribute"/>.</typeparam>
  /// <param name="symbol">The symbol to inspect for the attribute.</param>
  /// <returns>
  /// The attribute of type <typeparamref name="T"/> if found, otherwise <c>null</c>.
  /// </returns>
  public static AttributeData? GetAttribute<T>(this ISymbol symbol) where T : Attribute {
    return symbol.GetAttributes()
        .FirstOrDefault(a => a.IsOfAttributeType<T>());
  }

}