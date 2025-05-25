using Microsoft.Extensions.DependencyInjection;
namespace Retro.FastInject.Core;

/// <summary>
/// Defines a service scope interface that provides functionality for resolving
/// services and managing their lifecycle during compile-time.
/// </summary>
public interface ICompileTimeServiceScope : IKeyedServiceProvider, IServiceScope, IAsyncDisposable {

  /// <summary>
  /// Attempts to add the given instance to the scope's disposable resources if it implements IDisposable or IAsyncDisposable.
  /// </summary>
  /// <param name="instance">The instance to potentially add to the scope's disposable resources.</param>
  void TryAddDisposable(object instance);
  
}