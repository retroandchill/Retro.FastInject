using Microsoft.Extensions.DependencyInjection;
namespace Retro.FastInject.Core;

/// <summary>
/// Represents a factory for creating scopes in a compile-time dependency injection system.
/// </summary>
public interface ICompileTimeScopeFactory : IServiceScopeFactory {

  /// Creates and returns the root scope for dependency injection.
  /// <returns>
  /// An instance of ICompileTimeServiceScope representing the root dependency injection scope.
  /// </returns>
  ICompileTimeServiceScope GetRootScope();

}