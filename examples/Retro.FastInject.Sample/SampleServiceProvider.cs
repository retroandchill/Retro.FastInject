using Microsoft.Extensions.Logging;
using Retro.FastInject.Annotations;
using Retro.FastInject.Sample.Services;

namespace Retro.FastInject.Sample;

[ServiceProvider]
[Singleton<SingletonService>]
[Singleton<KeyedSingleton>(Key = "keyed")]
[Singleton<OtherSingletonService>(Key = "other")]
[Scoped<ScopedService>]
[Transient<TransientService>]
[Transient(typeof(Logger<>))]
[Singleton<ValueService>]
[Singleton<GenericService<int>>]
[Singleton<GenericService<float>>]
public sealed partial class SampleServiceProvider(int value, float simpleValue) {

  [Instance]
  private float SimpleValue { get; } = simpleValue;

  [Factory(ServiceScope.Transient)]
  private FactoryConstructedService CreateFactoryConstructedService() {
    return new FactoryConstructedService(value);
  }

  [Factory]
  private static LoggerFactory CreateLoggerFactory() {
    return new LoggerFactory();
  }
  
}