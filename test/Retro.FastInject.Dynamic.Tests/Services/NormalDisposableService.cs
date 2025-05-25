namespace Retro.FastInject.Dynamic.Tests.Services;

public sealed class NormalDisposableService : IDisposableService {
  public int DisposeCount { get; private set; }

  public void Dispose() {
    DisposeCount++;
  }
}