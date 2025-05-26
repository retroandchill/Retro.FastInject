using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Retro.FastInject.Annotations;

namespace Retro.FastInject.Dynamic.Tests;

[ServiceProvider(AllowDynamicRegistrations = true)]
[Singleton<CompileTimeService>]
[Singleton<ServiceWithDynamicDependency>]
[Scoped<ScopedCompileTimeService>]
public partial class TestHybridServiceProvider;

public class CompileTimeService {
  public string GetValue() => "Compile-time service";
}

public class DynamicService {
  private readonly CompileTimeService _compileTimeService;

  public DynamicService(CompileTimeService compileTimeService) {
    _compileTimeService = compileTimeService;
  }

  public string GetCombinedValue() => $"Dynamic service with {_compileTimeService.GetValue()}";
}

public class ServiceWithDynamicDependency {
  private readonly DynamicService _dynamicService;

  public ServiceWithDynamicDependency([AllowDynamic] DynamicService dynamicService) {
    _dynamicService = dynamicService;
  }

  public string GetValue() => _dynamicService.GetCombinedValue();
}

public class ScopedCompileTimeService {
  public string GetValue() => "Scoped compile-time service";
}

/// <summary>
/// Tests for the hybrid service provider with both compile-time and dynamic services.
/// </summary>
[TestFixture]
public class HybridServiceProviderTests {

  [Test]
  public void CompileTimeServices_AreAccessible() {
    // Arrange
    var services = new ServiceCollection();
    var provider = new TestHybridServiceProvider(services);

    // Act
    var service = provider.GetService<CompileTimeService>();

    // Assert
    Assert.That(service, Is.Not.Null);
    Assert.That(service.GetValue(), Is.EqualTo("Compile-time service"));
  }

  [Test]
  public void DynamicServices_CanBeResolved_WhenRegistered() {
    // Arrange
    var services = new ServiceCollection();
    services.AddSingleton<DynamicService>();
    var provider = new TestHybridServiceProvider(services);

    // Act
    var service = provider.GetService<DynamicService>();

    // Assert
    Assert.That(service, Is.Not.Null);
    Assert.That(service.GetCombinedValue(), Contains.Substring("Compile-time service"));
  }

  [Test]
  public void ServiceWithDynamicDependency_ResolvesCorrectly() {
    // Arrange
    var services = new ServiceCollection();
    services.AddSingleton<DynamicService>();
    var provider = new TestHybridServiceProvider(services);

    // Act
    var service = provider.GetService<ServiceWithDynamicDependency>();

    // Assert
    Assert.That(service, Is.Not.Null);
    Assert.That(service.GetValue(), Contains.Substring("Dynamic service with Compile-time service"));
  }

  [Test]
  public void DynamicServices_ReturnsNull_WhenNotRegistered() {
    // Arrange
    var services = new ServiceCollection();
    var provider = new TestHybridServiceProvider(services);

    // Act
    var service = provider.GetService<DynamicService>();

    // Assert
    Assert.That(service, Is.Null);
  }

  [Test]
  public void Scope_IsolatesScopedServices() {
    // Arrange
    var services = new ServiceCollection();
    services.AddScoped<DynamicScopedService>();
    var provider = new TestHybridServiceProvider(services);

    // Act
    DynamicScopedService? service1;
    DynamicScopedService? service2;
    
    using (var scope1 = provider.CreateScope()) {
      service1 = scope1.ServiceProvider.GetService<DynamicScopedService>();
    }
    
    using (var scope2 = provider.CreateScope()) {
      service2 = scope2.ServiceProvider.GetService<DynamicScopedService>();
    }

    // Assert
    Assert.That(service1, Is.Not.SameAs(service2));
  }

  private class DynamicScopedService {
    // Empty service for scoping tests
  }
}
