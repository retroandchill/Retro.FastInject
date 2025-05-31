using Retro.FastInject.Annotations;
namespace Retro.FastInject.Sample.WebApi.Services;

[ServiceProvider(AllowDynamicRegistrations = true)]
[Scoped<WeatherForcastService>]
public sealed partial class AppServiceProvider {
  
  [Factory(ServiceScope.Transient)]
  private static Logger<T> CreateLogger<T>([AllowDynamic] ILoggerFactory loggerFactory) {
    return new Logger<T>(loggerFactory);
  }
  
}