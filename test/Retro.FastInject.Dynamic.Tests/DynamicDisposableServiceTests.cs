using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Retro.FastInject.Annotations;

namespace Retro.FastInject.Dynamic.Tests;

[ServiceProvider(AllowDynamicRegistrations = true)]
[Singleton<CompileTimeDisposableService>]
[Scoped<ScopedCompileTimeDisposableService>]
public sealed partial class TestDisposableHybridServiceProvider;

public sealed class CompileTimeDisposableService : IDisposable {
  public bool IsDisposed { get; private set; }

  public void Dispose() {
    IsDisposed = true;
  }
}

public sealed class ScopedCompileTimeDisposableService : IDisposable {
  public bool IsDisposed { get; private set; }

  public void Dispose() {
    IsDisposed = true;
  }
}

public sealed class DynamicDisposableService : IDisposable {
  public bool IsDisposed { get; private set; }

  public void Dispose() {
    IsDisposed = true;
  }
}

public sealed class DynamicAsyncDisposableService : IAsyncDisposable {
  public bool IsDisposed { get; private set; }

  public ValueTask DisposeAsync() {
    IsDisposed = true;
    return ValueTask.CompletedTask;
  }
}

public sealed class DynamicDoubleDisposableService : IDisposable, IAsyncDisposable {
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

public sealed class ScopedDynamicDisposableService : IDisposable {
  public bool IsDisposed { get; private set; }

  public void Dispose() {
    IsDisposed = true;
  }
}

/// <summary>
/// Tests for disposable service management in hybrid service providers.
/// </summary>
[TestFixture]
public class DynamicDisposableServiceTests {

  [Test]
  public void Dispose_DisposableServices_DisposesAllServices() {
    // Arrange
    var services = new ServiceCollection();
    services.AddSingleton<DynamicDisposableService>();
    var provider = new TestDisposableHybridServiceProvider(services);
    
    var compileTimeService = provider.GetService<CompileTimeDisposableService>();
    var dynamicService = provider.GetService<DynamicDisposableService>();
    
    Assert.Multiple(() => {
      Assert.That(compileTimeService, Is.Not.Null);
      Assert.That(dynamicService, Is.Not.Null);
    });

    // Act
    provider.Dispose();

    // Assert
    Assert.Multiple(() => {
      Assert.That(compileTimeService!.IsDisposed, Is.True, "Compile-time disposable service should be disposed");
      Assert.That(dynamicService!.IsDisposed, Is.True, "Dynamic disposable service should be disposed");
    });
  }

  [Test]
  public async Task DisposeAsync_AsyncDisposableServices_DisposesAllServices() {
    // Arrange
    var services = new ServiceCollection();
    services.AddSingleton<DynamicAsyncDisposableService>();
    services.AddSingleton<DynamicDoubleDisposableService>();
    var provider = new TestDisposableHybridServiceProvider(services);
    
    var asyncDisposableService = provider.GetService<DynamicAsyncDisposableService>();
    var doubleDisposableService = provider.GetService<DynamicDoubleDisposableService>();
    
    Assert.Multiple(() => {
      Assert.That(asyncDisposableService, Is.Not.Null);
      Assert.That(doubleDisposableService, Is.Not.Null);
    });

    // Act
    await provider.DisposeAsync();

    // Assert
    Assert.Multiple(() => {
      Assert.That(asyncDisposableService!.IsDisposed, Is.True, "Async disposable service should be disposed");
      Assert.That(doubleDisposableService!.IsAsyncDisposed, Is.True, "Double disposable service should be disposed asynchronously");
    });
  }
  
  [Test]
  public void Scope_DisposesServices_WhenScopeDisposed() {
    // Arrange
    var services = new ServiceCollection();
    services.AddScoped<ScopedDynamicDisposableService>();
    var provider = new TestDisposableHybridServiceProvider(services);

    // Act & Assert
    ScopedDynamicDisposableService? scopedService;
    using (var scope = provider.CreateScope()) {
      scopedService = scope.ServiceProvider.GetService<ScopedDynamicDisposableService>();
      Assert.That(scopedService, Is.Not.Null);
      Assert.That(scopedService!.IsDisposed, Is.False, "Service should not be disposed while scope is active");
    }
    
    Assert.That(scopedService!.IsDisposed, Is.True, "Scoped service should be disposed when scope is disposed");
  }

  [Test]
  public void Scope_CompileTimeAndDynamicDisposables_BothDisposed() {
    // Arrange
    var services = new ServiceCollection();
    services.AddScoped<ScopedDynamicDisposableService>();
    var provider = new TestDisposableHybridServiceProvider(services);

    // Act
    ScopedCompileTimeDisposableService? compileScopedService;
    ScopedDynamicDisposableService? dynamicScopedService;
    
    using (var scope = provider.CreateScope()) {
      compileScopedService = scope.ServiceProvider.GetService<ScopedCompileTimeDisposableService>();
      dynamicScopedService = scope.ServiceProvider.GetService<ScopedDynamicDisposableService>();
      
      Assert.Multiple(() => {
        Assert.That(compileScopedService, Is.Not.Null);
        Assert.That(dynamicScopedService, Is.Not.Null);
      });
    }

    // Assert
    Assert.Multiple(() => {
      Assert.That(compileScopedService!.IsDisposed, Is.True, "Compile-time scoped service should be disposed");
      Assert.That(dynamicScopedService!.IsDisposed, Is.True, "Dynamic scoped service should be disposed");
    });
  }

  [Test]
  public void TryAddDisposable_ManuallyAddedDisposable_DisposesWithProvider() {
    // Arrange
    var services = new ServiceCollection();
    var provider = new TestDisposableHybridServiceProvider(services);
    var manualDisposable = new DynamicDisposableService();

    // Act
    provider.TryAddDisposable(manualDisposable);
    provider.Dispose();

    // Assert
    Assert.That(manualDisposable.IsDisposed, Is.True, "Manually added disposable should be disposed");
  }
}
