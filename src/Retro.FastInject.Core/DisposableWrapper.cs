using Retro.ReadOnlyParams.Annotations;

namespace Retro.FastInject.Core;

public readonly struct DisposableWrapper([ReadOnly] IDisposable? disposable, [ReadOnly] IAsyncDisposable? asyncDisposable) : IDisposable, IAsyncDisposable {

  public void Dispose() {
    disposable?.Dispose();
  }

  public ValueTask DisposeAsync() {
    return asyncDisposable?.DisposeAsync() ?? default;
  }
}