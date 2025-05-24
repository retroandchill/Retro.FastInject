using Microsoft.Extensions.Logging;
namespace Retro.FastInject.Sample.Services;

public class SingletonService(ILogger<SingletonService> logger, string val = "hello") : ISingletonService {
  
}