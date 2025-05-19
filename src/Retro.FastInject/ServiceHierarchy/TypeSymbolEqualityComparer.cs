using System.Collections.Generic;
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
  public bool Equals(ITypeSymbol? x, ITypeSymbol? y) => SymbolEqualityComparer.Default.Equals(x, y);

  /// <inheritdoc />
  public int GetHashCode(ITypeSymbol obj) => SymbolEqualityComparer.Default.GetHashCode(obj);
}