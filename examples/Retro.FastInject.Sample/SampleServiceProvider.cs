using Microsoft.Extensions.Logging;
using Retro.FastInject.Annotations;
using Retro.FastInject.Sample.Services;

namespace Retro.FastInject.Sample;

[ServiceProvider(AllowDynamicRegistrations = true)]
[Singleton<SingletonService>]
[Singleton<KeyedSingleton>(Key = "keyed")]
[Singleton<OtherSingletonService>(Key = "other")]
[Scoped<ScopedService>]
[Transient<TransientService>]
[Transient(typeof(Logger<>))]
[Singleton<ValueService>]
[Singleton<GenericService<int>>]
[Singleton<GenericService<float>>]
public sealed partial class SampleServiceProvider {
  
  private readonly int _value;
  
  private SampleServiceProvider(int value, float simpleValue) {
    _value = value;
    SimpleValue = simpleValue;
  }

  [Instance]
  private float SimpleValue { get; }

  [Factory(ServiceScope.Transient)]
  private FactoryConstructedService CreateFactoryConstructedService() {
    return new FactoryConstructedService(_value);
  }

  [Factory]
  private static LoggerFactory CreateLoggerFactory() {
    return new LoggerFactory();
  }
  
}