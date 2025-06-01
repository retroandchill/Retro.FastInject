using Retro.FastInject.Annotations;
namespace Retro.FastInject.Sample.Cli.Services;

[ServiceProvider]
[Singleton<SingletonClass>]
[Scoped<ScopedClass>]
[Transient<TransientClass>]
public partial class CliServiceProvider;