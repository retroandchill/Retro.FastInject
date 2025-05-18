using System;

namespace Retro.FastInject.Annotations;

[AttributeUsage(AttributeTargets.Method)]
public class FactoryAttribute(ServiceScope scope = ServiceScope.Singleton) : Attribute() {

  public ServiceScope Scope { get; } = scope;
  
  public string? Key { get; init; }

}