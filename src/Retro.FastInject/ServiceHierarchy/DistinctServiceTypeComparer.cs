using System.Collections.Generic;

namespace Retro.FastInject.ServiceHierarchy;

public class DistinctServiceTypeComparer : IEqualityComparer<ServiceRegistration> {
  
  public static DistinctServiceTypeComparer Instance { get; } = new();
  
  public bool Equals(ServiceRegistration x, ServiceRegistration y) {
    return TypeSymbolEqualityComparer.Instance.Equals(x.Type, y.Type);
  }

  public int GetHashCode(ServiceRegistration obj) {
    return TypeSymbolEqualityComparer.Instance.GetHashCode(obj.Type);
  }
}