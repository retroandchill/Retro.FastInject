namespace Retro.FastInject.Sample.Services;

public class ScopedService(ISingletonService singleton, ITransientService transient) : IScopedService {
  
}