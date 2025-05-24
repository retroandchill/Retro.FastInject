using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Retro.FastInject.ServiceHierarchy;

/// Provides a mechanism to compare instances of ITypeSymbol for equality and hashing purposes,
/// using the default SymbolEqualityComparer from the Microsoft.CodeAnalysis namespace.
/// This class is commonly used to ensure proper comparisons and lookups of ITypeSymbol objects
/// in collections like dictionaries or sets, where equality and hash codes are required.
public class TypeSymbolEqualityComparer : IEqualityComparer<ITypeSymbol> {
  /// Gets a singleton instance of the TypeSymbolEqualityComparer class.
  /// This property provides a shared, thread-safe instance of TypeSymbolEqualityComparer
  /// that can be used for comparing and hashing ITypeSymbol objects. It ensures consistent
  /// equality checks and hash code generation within collections such as dictionaries or
  /// sets that contain ITypeSymbol keys or values. The comparison behavior adheres to
  /// the rules of the default SymbolEqualityComparer from the Microsoft.CodeAnalysis namespace.
  public static TypeSymbolEqualityComparer Instance { get; } = new();

  /// <inheritdoc />
  public bool Equals(ITypeSymbol? x, ITypeSymbol? y) {
    if (ReferenceEquals(x, y)) return true;
    if (x == null || y == null) return false;

    if (x is not INamedTypeSymbol namedX || y is not INamedTypeSymbol namedY) return SymbolEqualityComparer.Default.Equals(x, y);

    // If both are generic types with type parameters
    if (namedX.IsGenericType && namedY.IsGenericType &&
        namedX.TypeArguments.Any(t => t is ITypeParameterSymbol) &&
        namedY.TypeArguments.Any(t => t is ITypeParameterSymbol)) {
      // Compare their constructed forms
      return SymbolEqualityComparer.Default.Equals(namedX.ConstructedFrom, namedY.ConstructedFrom);
    }

    // For all other cases, use default comparison
    return SymbolEqualityComparer.Default.Equals(x, y);
  }


  /// <inheritdoc />
  public int GetHashCode(ITypeSymbol obj) {
    if (obj is INamedTypeSymbol { IsGenericType: true } namedType &&
        namedType.TypeArguments.Any(t => t is ITypeParameterSymbol)) {
      return SymbolEqualityComparer.Default.GetHashCode(namedType.ConstructedFrom);
    }
    return SymbolEqualityComparer.Default.GetHashCode(obj);
  }

}