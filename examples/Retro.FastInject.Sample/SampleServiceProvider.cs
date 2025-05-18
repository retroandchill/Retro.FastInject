using Retro.FastInject.Annotations;
using Retro.FastInject.Sample.Services;

namespace Retro.FastInject.Sample;

[ServiceProvider]
[Singleton<SingletonService>]
[Singleton<KeyedSingleton>(Key = "keyed")]
[Scoped<ScopedService>]
[Transient<TransientService>]
public partial class SampleServiceProvider {
}