using Microsoft.Extensions.DependencyInjection;
namespace Retro.FastInject.Core;

public interface ICompileTimeScopeFactory : IServiceScopeFactory {

  ICompileTimeServiceScope GetRootScope();

}