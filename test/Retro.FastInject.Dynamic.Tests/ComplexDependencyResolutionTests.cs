using System;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Retro.FastInject.Annotations;

namespace Retro.FastInject.Dynamic.Tests;

[ServiceProvider(AllowDynamicRegistrations = true)]
[Singleton<CompileTimeRootService>]
[Singleton<ServiceWithCompileTimeDependency>]
[Singleton<ServiceWithMixedDependencies>]
[Scoped<ScopedServiceWithDynamicDependency>]
public partial class TestComplexDependencyServiceProvider;

public interface ICompileTimeService {
  string GetValue();
}

public class CompileTimeRootService : ICompileTimeService {
  public string GetValue() => "Compile-time Root";
}

public class ServiceWithCompileTimeDependency(ICompileTimeService service) {

  public string GetDependencyValue() => service.GetValue();
}

public interface IDynamicService {
  string GetValue();
}

public class ComplexDynamicService(ICompileTimeService compileTimeService) : IDynamicService {

  public string GetValue() => $"Dynamic service depending on {compileTimeService.GetValue()}";
}

public class AnotherDynamicService : IDynamicService {
  public string GetValue() => "Another dynamic service";
}

public class ServiceWithMixedDependencies(
    ICompileTimeService compileTimeService,
    [AllowDynamic] IDynamicService dynamicService) {

  public string GetCompileTimeValue() => compileTimeService.GetValue();
  public string GetDynamicValue() => dynamicService.GetValue();
}

public class ScopedServiceWithDynamicDependency(
    ICompileTimeService compileTimeService,
    [AllowDynamic] IDynamicService dynamicService) {
  public Guid Id { get; } = Guid.NewGuid();

  public string GetCombinedValue() => 
    $"{compileTimeService.GetValue()} + {dynamicService.GetValue()}";
}

/// <summary>
/// Tests for complex dependency resolution in hybrid service providers.
/// </summary>
[TestFixture]
public class ComplexDependencyResolutionTests {

  [Test]
  public void CompileTimeService_ResolvesCorrectly() {
    // Arrange
    var services = new ServiceCollection();
    var provider = new TestComplexDependencyServiceProvider(services);

    // Act
    var service = provider.GetService<ICompileTimeService>();
    var concreteService = provider.GetService<CompileTimeRootService>();

    // Assert
    Assert.Multiple(() => {
      Assert.That(service, Is.Not.Null);
      Assert.That(concreteService, Is.Not.Null);
      Assert.That(service, Is.SameAs(concreteService));
    });
  }

  [Test]
  public void ServiceWithCompileTimeDependency_ResolvesCorrectly() {
    // Arrange
    var services = new ServiceCollection();
    var provider = new TestComplexDependencyServiceProvider(services);

    // Act
    var service = provider.GetService<ServiceWithCompileTimeDependency>();

    // Assert
    Assert.That(service, Is.Not.Null);
    Assert.That(service!.GetDependencyValue(), Is.EqualTo("Compile-time Root"));
  }

  [Test]
  public void DynamicService_DependingOnCompileTimeService_ResolvesCorrectly() {
    // Arrange
    var services = new ServiceCollection();
    services.AddSingleton<IDynamicService, ComplexDynamicService>();
    var provider = new TestComplexDependencyServiceProvider(services);

    // Act
    var service = provider.GetService<IDynamicService>();

    // Assert
    Assert.That(service, Is.Not.Null);
    Assert.That(service!.GetValue(), Contains.Substring("Compile-time Root"));
  }

  [Test]
  public void ServiceWithMixedDependencies_ResolvesCorrectly() {
    // Arrange
    var services = new ServiceCollection();
    services.AddSingleton<IDynamicService, ComplexDynamicService>();
    var provider = new TestComplexDependencyServiceProvider(services);

    // Act
    var service = provider.GetService<ServiceWithMixedDependencies>();

    // Assert
    Assert.That(service, Is.Not.Null);
    
    Assert.Multiple(() => {
      Assert.That(service!.GetCompileTimeValue(), Is.EqualTo("Compile-time Root"));
      Assert.That(service.GetDynamicValue(), Contains.Substring("Dynamic service depending on Compile-time Root"));
    });
  }

  [Test]
  public void ServiceWithMixedDependencies_DifferentImplementation_ResolvesCorrectly() {
    // Arrange
    var services = new ServiceCollection();
    services.AddSingleton<IDynamicService, AnotherDynamicService>();
    var provider = new TestComplexDependencyServiceProvider(services);

    // Act
    var service = provider.GetService<ServiceWithMixedDependencies>();

    // Assert
    Assert.That(service, Is.Not.Null);
    
    Assert.Multiple(() => {
      Assert.That(service!.GetCompileTimeValue(), Is.EqualTo("Compile-time Root"));
      Assert.That(service.GetDynamicValue(), Is.EqualTo("Another dynamic service"));
    });
  }

  [Test]
  public void ScopedServiceWithDynamicDependency_CreatesDifferentInstances() {
    // Arrange
    var services = new ServiceCollection();
    services.AddScoped<IDynamicService, ComplexDynamicService>();
    var provider = new TestComplexDependencyServiceProvider(services);

    // Act
    ScopedServiceWithDynamicDependency? service1;
    ScopedServiceWithDynamicDependency? service2;
    
    using (var scope1 = provider.CreateScope()) {
      service1 = scope1.ServiceProvider.GetService<ScopedServiceWithDynamicDependency>();
    }
    
    using (var scope2 = provider.CreateScope()) {
      service2 = scope2.ServiceProvider.GetService<ScopedServiceWithDynamicDependency>();
    }

    // Assert
    Assert.Multiple(() => {
      Assert.That(service1, Is.Not.Null);
      Assert.That(service2, Is.Not.Null);
      Assert.That(service1, Is.Not.SameAs(service2));
      Assert.That(service1!.Id, Is.Not.EqualTo(service2!.Id));
    });
  }

  [Test]
  public void ScopedServiceWithDynamicDependency_SameScope_SameInstance() {
    // Arrange
    var services = new ServiceCollection();
    services.AddScoped<IDynamicService, ComplexDynamicService>();
    var provider = new TestComplexDependencyServiceProvider(services);

    // Act
    using var scope = provider.CreateScope();
    var service1 = scope.ServiceProvider.GetService<ScopedServiceWithDynamicDependency>();
    var service2 = scope.ServiceProvider.GetService<ScopedServiceWithDynamicDependency>();

    // Assert
    Assert.Multiple(() => {
      Assert.That(service1, Is.Not.Null);
      Assert.That(service2, Is.Not.Null);
      Assert.That(service1, Is.SameAs(service2));
    });
  }
}
