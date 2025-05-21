using Retro.FastInject.Annotations;
using Retro.FastInject.Sample.Services;

namespace Retro.FastInject.Sample;

[ServiceProvider]
[Singleton<SingletonService>]
[Singleton<KeyedSingleton>(Key = "keyed")]
[Singleton<OtherSingletonService>(Key = "other")]
[Scoped<ScopedService>]
[Transient<TransientService>]
[Singleton<ValueService>]
public sealed partial class SampleServiceProvider(int value, float simpleValue) {

  [Instance]
  private float SimpleValue { get; } = simpleValue;

  [Factory(ServiceScope.Transient)]
  private FactoryConstructedService CreateFactoryConstructedService() {
    return new FactoryConstructedService(value);
  }
  
}