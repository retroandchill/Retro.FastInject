using Microsoft.Extensions.DependencyInjection;
namespace Retro.FastInject.Core;

/// <summary>
/// Defines a service provider that can be used at compile time to resolve and manage services.
/// </summary>
/// <remarks>
/// This interface extends features for service resolution, scoping, and resource management.
/// </remarks>
public interface ICompileTimeServiceProvider : IKeyedServiceProvider,
                                               IDisposable,
                                               IAsyncDisposable {

  /// <summary>
  /// Attempts to add a disposable instance to the service provider's management.
  /// </summary>
  /// <param name="instance">The object instance to be managed as a disposable resource.</param>
  void TryAddDisposable(object instance);

}