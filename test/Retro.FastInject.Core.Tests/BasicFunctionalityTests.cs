using System;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Retro.FastInject.Annotations;

namespace Retro.FastInject.Core.Tests;

[ServiceProvider]
[Singleton<SimpleService>]
[Singleton<ServiceWithDependency>]
[Transient<TransientService>]
public partial class TestServiceProvider;

public class SimpleService {
  public string GetValue() => "Simple value";
}

public class ServiceWithDependency(SimpleService simpleService) {
  public SimpleService SimpleService { get; } = simpleService;

  public string GetDependencyValue() => SimpleService.GetValue();
}

public class TransientService {
  public Guid Id { get; } = Guid.NewGuid();
}

public class UnregisteredService;

/// <summary>
/// Tests for basic functionality of compile-time service providers.
/// </summary>
[TestFixture]
public class BasicFunctionalityTests {

  [Test]
  public void GetService_SingletonService_ReturnsInstance() {
    // Arrange
    var provider = new TestServiceProvider();

    // Act
    var service = provider.VerifyServiceResolved<SimpleService>();

    // Assert
    Assert.That(service.GetValue(), Is.EqualTo("Simple value"));
  }

  [Test]
  public void GetService_SingletonService_ReturnsSameInstance() {
    // Arrange
    var provider = new TestServiceProvider();

    // Act
    var service1 = provider.GetService<SimpleService>();
    var service2 = provider.GetService<SimpleService>();

    Assert.Multiple(() => {
      // Assert
      Assert.That(service1, Is.Not.Null);
      Assert.That(service2, Is.Not.Null);
    });
    Assert.That(service1, Is.SameAs(service2), "Singleton services should return the same instance");
  }

  [Test]
  public void GetService_TransientService_ReturnsNewInstance() {
    // Arrange
    var provider = new TestServiceProvider();

    // Act
    var service1 = provider.GetService<TransientService>();
    var service2 = provider.GetService<TransientService>();

    Assert.Multiple(() => {
      // Assert
      Assert.That(service1, Is.Not.Null);
      Assert.That(service2, Is.Not.Null);
    });
    Assert.That(service1, Is.Not.SameAs(service2), "Transient services should return different instances");
    Assert.That(service1.Id, Is.Not.EqualTo(service2.Id), "Transient services should have different IDs");
  }

  [Test]
  public void GetService_ServiceWithDependency_InjectsDependency() {
    // Arrange
    var provider = new TestServiceProvider();

    // Act
    var service = provider.VerifyServiceResolved<ServiceWithDependency>();

    // Assert
    Assert.That(service.GetDependencyValue(), Is.EqualTo("Simple value"));
  }

  [Test]
  public void GetService_UnregisteredService_ReturnsNull() {
    // Arrange
    var provider = new TestServiceProvider();

    // Act
    var service = provider.GetService<UnregisteredService>();

    // Assert
    Assert.That(service, Is.Null);
  }
}