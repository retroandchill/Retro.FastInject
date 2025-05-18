using Microsoft.CodeAnalysis;
using Retro.FastInject.Annotations;
namespace Retro.FastInject.ServiceHierarchy;

public class ServiceInjection(ServiceRegistration registration, string parameters) {

  public string ServiceType { get; } = registration.Type.ToDisplayString();
  
  public string FieldName { get; } = registration.FieldName;

  public bool FromOtherService { get; } = registration.ImplementationType is not null;
  
  public string? OtherType = registration.ImplementationType?.ToDisplayString();
  
  public bool IsSingleton { get; } = registration.ImplementationType is null && registration.Lifetime == ServiceScope.Singleton;
  
  public bool IsScoped { get; } = registration.ImplementationType is null && registration.Lifetime == ServiceScope.Scoped;
  
  public bool IsTransient { get; } = registration.ImplementationType is null && registration.Lifetime == ServiceScope.Transient;
  
  public string? Key { get; } = registration.Key;
  
  public string Parameters { get; } = parameters;
}