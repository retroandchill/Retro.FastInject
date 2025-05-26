using Retro.ReadOnlyParams.Annotations;

namespace Retro.FastInject.Core;

/// <summary>
/// A structure that provides support for managing and disposing of synchronous and asynchronous resources.
/// </summary>
/// <remarks>
/// This structure implements both <see cref="System.IDisposable"/> and <see cref="System.IAsyncDisposable"/> interfaces,
/// allowing efficient management of resources that require cleanup.
/// </remarks>
/// <threadsafety>
/// This structure is immutable and thread-safe.
/// </threadsafety>
public readonly struct DisposableWrapper([ReadOnly] IDisposable? disposable, [ReadOnly] IAsyncDisposable? asyncDisposable) : IDisposable, IAsyncDisposable {
  /// <inheritdoc />
  public void Dispose() {
    disposable?.Dispose();
  }

  /// <inheritdoc />
  public ValueTask DisposeAsync() {
    if (asyncDisposable is not null) {
      return asyncDisposable.DisposeAsync();
    }
    
    Dispose();
    return default;
  }
}