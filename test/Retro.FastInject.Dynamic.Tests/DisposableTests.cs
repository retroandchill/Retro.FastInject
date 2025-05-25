using Microsoft.Extensions.DependencyInjection;
using Retro.FastInject.Annotations;
using Retro.FastInject.Dynamic.Tests.Services;
namespace Retro.FastInject.Dynamic.Tests;

public class DependentService([AllowDynamic] IDisposableService disposable) {
  public IDisposableService Disposable { get; } = disposable;
}

[ServiceProvider(AllowDynamicRegistrations = true)]
[Singleton<DependentService>]
public partial class DynamicSingletonProvider;

[ServiceProvider(AllowDynamicRegistrations = true)]
[Scoped<DependentService>]
public partial class DynamicScopedProvider;

public class DisposableTests {

  [Test]
  public void TestDynamicServiceDisposesCorrectly() {
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSingleton<IDisposableService, NormalDisposableService>();

    IDisposableService disposableService;
    using (var provider = new DynamicSingletonProvider(serviceCollection)) {
      var dependentService = provider.GetService<DependentService>();
      Assert.That(dependentService, Is.Not.Null);
      disposableService = dependentService.Disposable;
    }
    
    Assert.That(disposableService.DisposeCount, Is.EqualTo(1));
  }
  
  [Test]
  public async Task TestDynamicServiceDisposesCorrectly_Async() {
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSingleton<IDisposableService, NormalDisposableService>();

    IDisposableService disposableService;
    await using (var provider = new DynamicSingletonProvider(serviceCollection)) {
      var dependentService = provider.GetService<DependentService>();
      Assert.That(dependentService, Is.Not.Null);
      disposableService = dependentService.Disposable;
    }
    
    Assert.That(disposableService.DisposeCount, Is.EqualTo(1));
  }
  
  [Test]
  public void TestDynamicServiceDisposesCorrectlyWhenScoped() {
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSingleton<IDisposableService, NormalDisposableService>();
    
    IDisposableService disposableService;
    using (var provider = new DynamicScopedProvider(serviceCollection)) {
      using (var scope = provider.CreateScope()) {
        var dependentService = scope.ServiceProvider.GetService<DependentService>();
        Assert.That(dependentService, Is.Not.Null);
        disposableService = dependentService.Disposable;
      }

      Assert.That(disposableService.DisposeCount, Is.EqualTo(0));
    }
    
    Assert.That(disposableService.DisposeCount, Is.EqualTo(1));
  }
  
  [Test]
  public async Task TestDynamicServiceDisposesCorrectlyWhenScoped_Async() {
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSingleton<IDisposableService, NormalDisposableService>();
    
    IDisposableService disposableService;
    await using (var provider = new DynamicScopedProvider(serviceCollection)) {
      using (var scope = provider.CreateScope()) {
        var dependentService = scope.ServiceProvider.GetService<DependentService>();
        Assert.That(dependentService, Is.Not.Null);
        disposableService = dependentService.Disposable;
      }

      Assert.That(disposableService.DisposeCount, Is.EqualTo(0));
    }
    
    Assert.That(disposableService.DisposeCount, Is.EqualTo(1));
  }
  
}