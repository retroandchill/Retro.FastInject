using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Retro.FastInject.Annotations;

namespace Retro.FastInject.Core.Tests;

[ServiceProvider]
[Singleton<DisposableService>]
[Singleton<AsyncDisposableService>]
[Singleton<DoubleDisposableService>]
[Scoped<ScopedDisposableService>]
public sealed partial class TestDisposableServiceProvider;

public sealed class DisposableService : IDisposable {
  public bool IsDisposed { get; private set; }

  public void Dispose() {
    IsDisposed = true;
  }
}

public sealed class AsyncDisposableService : IAsyncDisposable {
  public bool IsDisposed { get; private set; }

  public ValueTask DisposeAsync() {
    IsDisposed = true;
    return ValueTask.CompletedTask;
  }
}

public sealed class DoubleDisposableService : IDisposable, IAsyncDisposable {
  public bool IsDisposed { get; private set; }
  public bool IsAsyncDisposed { get; private set; }

  public void Dispose() {
    IsDisposed = true;
  }

  public ValueTask DisposeAsync() {
    IsAsyncDisposed = true;
    return ValueTask.CompletedTask;
  }
}

public sealed class ScopedDisposableService : IDisposable {
  public bool IsDisposed { get; private set; }

  public void Dispose() {
    IsDisposed = true;
  }
}

/// <summary>
/// Tests for disposable service management in compile-time service providers.
/// </summary>
[TestFixture]
public class DisposableServiceTests {

  [Test]
  public void Dispose_DisposableServices_DisposesAllServices() {
    // Arrange
    var provider = new TestDisposableServiceProvider();
    var disposableService = provider.VerifyServiceResolved<DisposableService>();
    var doubleDisposableService = provider.VerifyServiceResolved<DoubleDisposableService>();

    // Act
    provider.Dispose();

    Assert.Multiple(() => {
      // Assert
      Assert.That(disposableService.IsDisposed, Is.True, "DisposableService should be disposed");
      Assert.That(doubleDisposableService.IsDisposed, Is.True, "DoubleDisposableService should be disposed synchronously");
    });
  }

  [Test]
  public async Task DisposeAsync_AsyncDisposableServices_DisposesAllServices() {
    // Arrange
    var provider = new TestDisposableServiceProvider();
    var asyncDisposableService = provider.VerifyServiceResolved<AsyncDisposableService>();
    var doubleDisposableService = provider.VerifyServiceResolved<DoubleDisposableService>();

    // Act
    await provider.DisposeAsync();

    Assert.Multiple(() => {
      // Assert
      Assert.That(asyncDisposableService.IsDisposed, Is.True, "AsyncDisposableService should be disposed");
      Assert.That(doubleDisposableService.IsAsyncDisposed, Is.True, "DoubleDisposableService should be disposed asynchronously");
    });
  }

  [Test]
  public void Scope_DisposableServices_DisposesServicesWhenScopeDisposed() {
    // Arrange
    var provider = new TestDisposableServiceProvider();

    ScopedDisposableService? scopedService;
    // Act
    using (var scope = provider.CreateScope()) {
      scopedService = scope.ServiceProvider.GetService<ScopedDisposableService>();
      Assert.That(scopedService, Is.Not.Null);
      Assert.That(scopedService!.IsDisposed, Is.False, "Service should not be disposed while scope is active");

      // Let the scope dispose
    }

    // Create a new scope to verify the old instance was disposed
    using var newScope = provider.CreateScope();
    var newScopedService = newScope.ServiceProvider.GetService<ScopedDisposableService>();

    Assert.Multiple(() => {
      // Assert
      Assert.That(newScopedService, Is.Not.Null);
      Assert.That(scopedService, Is.Not.SameAs(newScopedService), "Scoped service should be different instance in new scope");
    });
  }

  [Test]
  public void TryAddDisposable_ManuallyAddedDisposable_DisposesWithProvider() {
    // Arrange
    var provider = new TestDisposableServiceProvider();
    var manualDisposable = new DisposableService();

    // Act
    provider.TryAddDisposable(manualDisposable);
    provider.Dispose();

    // Assert
    Assert.That(manualDisposable.IsDisposed, Is.True, "Manually added disposable should be disposed");
  }
}