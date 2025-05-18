using Retro.FastInject.Annotations;
using Retro.FastInject.Sample.Services;

namespace Retro.FastInject.Sample;

[ServiceProvider]
[Singleton<SingletonService>]
[Singleton<KeyedSingleton>(Key = "keyed")]
[Scoped<ScopedService>]
[Transient<TransientService>]
public partial class SampleServiceProvider(int value, float simpleValue) {

  [Instance]
  private float SimpleValue { get; } = simpleValue;

  [Factory(ServiceScope.Transient)]
  public FactoryConstructedService CreateFactoryConstructedService() {
    return new FactoryConstructedService(value);
  }
  
}