namespace Retro.FastInject.Sample.WebApi.Services;

public class AppServiceProviderFactory : IServiceProviderFactory<IServiceCollection> {

  public IServiceCollection CreateBuilder(IServiceCollection services) {
    return services;
  }

  public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder) {
    return new AppServiceProvider(containerBuilder);
  }
}