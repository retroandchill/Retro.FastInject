using Microsoft.Extensions.DependencyInjection;
namespace Retro.FastInject.Core;

/// <summary>
/// Defines a service scope interface that provides functionality for resolving
/// services and managing their lifecycle during compile-time.
/// </summary>
public interface ICompileTimeServiceScope : ICompileTimeServiceProvider, IServiceScope;