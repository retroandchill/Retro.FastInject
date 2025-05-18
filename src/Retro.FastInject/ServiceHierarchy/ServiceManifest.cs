using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Retro.FastInject.Annotations;
namespace Retro.FastInject.ServiceHierarchy;

public class ServiceManifest {
  

  private readonly Dictionary<ITypeSymbol, List<ServiceRegistration>> _services;
  private readonly Dictionary<ITypeSymbol, ITypeSymbol> _indirectServices;
  private readonly HashSet<ITypeSymbol> _baseTypes;

  public ServiceManifest() {
    _services = new Dictionary<ITypeSymbol, List<ServiceRegistration>>(TypeSymbolEqualityComparer.Instance);
    _indirectServices = new Dictionary<ITypeSymbol, ITypeSymbol>(TypeSymbolEqualityComparer.Instance);
    _baseTypes = new HashSet<ITypeSymbol>(TypeSymbolEqualityComparer.Instance);
  }

  public void AddService(ITypeSymbol serviceType, ServiceScope lifetime, ITypeSymbol? implementationType = null, string? key = null) {
    if (!_services.TryGetValue(serviceType, out var registrations)) {
      registrations = [];
      _services[serviceType] = registrations;
    }

    registrations.Add(new ServiceRegistration {
        Type = serviceType,
        Key = key,
        Lifetime = lifetime,
        ImplementationType = implementationType is null || implementationType.Equals(serviceType, SymbolEqualityComparer.Default) ? null : implementationType,
        IndexForType = registrations.Count
    });
  }

  public void AddIndirectService(ITypeSymbol serviceType, ITypeSymbol implementationType) {
    _indirectServices[serviceType] = implementationType;
  }

  public void AddBaseType(ITypeSymbol baseType) {
    _baseTypes.Add(baseType);
  }

  public IEnumerable<ITypeSymbol> GetTypesWithMultipleResolutions() {
    return _services.Where(kvp => kvp.Value.Count > 1).Select(kvp => kvp.Key);
  }

  public IEnumerable<ServiceRegistration> GetKeyedServices() {
    return _services.Values
        .SelectMany(list => list)
        .Where(reg => reg.Key != null);
  }

  public IEnumerable<ServiceRegistration> GetUnnamedServices() {
    return _services.Values
        .SelectMany(list => list)
        .Where(reg => reg.Key == null);
  }

  public IEnumerable<ServiceRegistration> GetServicesByLifetime(ServiceScope lifetime) {
    return _services.Values
        .SelectMany(list => list)
        .Where(reg => reg.Lifetime == lifetime);
  }

  public IEnumerable<ITypeSymbol> GetBaseTypes() {
    return _baseTypes;
  }
}