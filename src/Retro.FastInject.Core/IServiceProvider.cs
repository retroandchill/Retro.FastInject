namespace Retro.FastInject.Core;

public interface IServiceProvider<out T> {
  
  T GetService();
  
}