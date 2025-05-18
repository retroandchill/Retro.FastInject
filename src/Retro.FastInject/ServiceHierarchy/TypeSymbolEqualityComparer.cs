using System.Collections.Generic;
using Microsoft.CodeAnalysis;
namespace Retro.FastInject.ServiceHierarchy;

public class TypeSymbolEqualityComparer : IEqualityComparer<ITypeSymbol>
{
  public static TypeSymbolEqualityComparer Instance { get; } = new();

  public bool Equals(ITypeSymbol? x, ITypeSymbol? y) => SymbolEqualityComparer.Default.Equals(x, y);
    
  public int GetHashCode(ITypeSymbol obj) => SymbolEqualityComparer.Default.GetHashCode(obj);
}
