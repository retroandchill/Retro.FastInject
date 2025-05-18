using System.Collections.Generic;
using Microsoft.CodeAnalysis;
namespace Retro.FastInject.ServiceHierarchy;

public class ServiceTypeComparator : IEqualityComparer<ServiceDeclaration> {
  
  public static ServiceTypeComparator Instance { get; } = new();

  public bool Equals(ServiceDeclaration x, ServiceDeclaration y) {
    return SymbolEqualityComparer.Default.Equals(x.Type, y.Type);
  }
  public int GetHashCode(ServiceDeclaration obj) {
    return SymbolEqualityComparer.Default.GetHashCode(obj.Type);
  }
}