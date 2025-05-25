using Retro.FastInject.Annotations;
namespace Retro.FastInject.Sample.Services;

public class ScopedService(ISingletonService singleton, ITransientService transient, [AllowDynamic] IDynamicService dynamicService, [AllowDynamic] IOptionalService? optionalService) : IScopedService {
  
}