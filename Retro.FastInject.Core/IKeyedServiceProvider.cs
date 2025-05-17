namespace Retro.FastInject.Core;

public interface IKeyedServiceProvider<out T> {
  
  T GetKeyedService(object? serviceKey);
  
}