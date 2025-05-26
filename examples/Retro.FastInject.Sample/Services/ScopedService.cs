using Retro.FastInject.Annotations;
namespace Retro.FastInject.Sample.Services;

public class ScopedService(ISingletonService singleton, ITransientService transient, [AllowDynamic] IDynamicService dynamicService, [AllowDynamic] IOptionalService? optionalService) : IScopedService {

  public ISingletonService Singleton { get; } = singleton;
  public ITransientService Transient { get; } = transient;
  public IDynamicService DynamicService { get; } = dynamicService;
  public IOptionalService? OptionalService { get; } = optionalService;

}