namespace Retro.FastInject.Dynamic.Tests.Services;

public interface IDisposableService : IDisposable {
  
  int DisposeCount { get; }
  
}