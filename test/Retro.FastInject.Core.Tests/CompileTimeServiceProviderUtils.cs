using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Retro.FastInject.Annotations;

namespace Retro.FastInject.Core.Tests;

/// <summary>
/// Base class for all compile-time service provider tests providing common utility methods.
/// </summary>
public static class CompileTimeServiceProviderUtils {

  /// <summary>
  /// Helper method to verify a service has been resolved correctly.
  /// </summary>
  /// <typeparam name="TService">The service type to verify.</typeparam>
  /// <param name="serviceProvider">The service provider instance.</param>
  /// <returns>The resolved service.</returns>
  public static TService VerifyServiceResolved<TService>(this ICompileTimeServiceProvider serviceProvider) {
    // Get the service from the provider
    var service = serviceProvider.GetService<TService>();

    // Assert that the service was resolved
    Assert.That(service, Is.Not.Null, $"Service of type {typeof(TService).Name} should be resolved");

    return service;
  }
}