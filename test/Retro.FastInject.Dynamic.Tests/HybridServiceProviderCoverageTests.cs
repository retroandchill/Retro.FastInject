using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Retro.FastInject.Annotations;

namespace Retro.FastInject.Dynamic.Tests;

[ServiceProvider(AllowDynamicRegistrations = true)]
public partial class TestCoverageServiceProvider;

/// <summary>
/// Tests for specific code paths in the HybridServiceProvider implementation.
/// </summary>
[TestFixture]
public class HybridServiceProviderCoverageTests {

  [Test]
  public void GetService_IServiceProvider_ReturnsSelf() {
    // Arrange
    var services = new ServiceCollection();
    var provider = new TestCoverageServiceProvider(services);

    // Act
    var serviceProvider = provider.GetService<IServiceProvider>();

    // Assert
    Assert.That(serviceProvider, Is.SameAs(provider));
  }

  [Test]
  public void GetService_IServiceScopeFactory_ReturnsSelf() {
    // Arrange
    var services = new ServiceCollection();
    var provider = new TestCoverageServiceProvider(services);

    // Act
    var scopeFactory = provider.GetService<IServiceScopeFactory>();

    // Assert
    Assert.That(scopeFactory, Is.SameAs(provider));
  }

  [Test]
  public void GetService_WithFactoryRegistration_InvokesFactory() {
    // Arrange
    var services = new ServiceCollection();
    var factoryInvoked = false;
    services.AddSingleton<FactoryService>(sp => {
      factoryInvoked = true;
      return new FactoryService("Created via factory");
    });
    var provider = new TestCoverageServiceProvider(services);

    // Act
    var service = provider.GetService<FactoryService>();

    // Assert
    Assert.Multiple(() => {
      Assert.That(factoryInvoked, Is.True, "Factory should be invoked");
      Assert.That(service, Is.Not.Null);
      Assert.That(service!.Value, Is.EqualTo("Created via factory"));
    });
  }

  [Test]
  public void GetService_WithImplementationInstance_ReturnsInstance() {
    // Arrange
    var services = new ServiceCollection();
    var instance = new FactoryService("Predefined instance");
    services.AddSingleton<FactoryService>(instance);
    var provider = new TestCoverageServiceProvider(services);

    // Act
    var service = provider.GetService<FactoryService>();

    // Assert
    Assert.That(service, Is.SameAs(instance));
  }

  [Test]
  public void GetService_MultipleRegistrations_ReturnsLastOne() {
    // Arrange
    var services = new ServiceCollection();
    services.AddSingleton<MultiRegistrationService>(new MultiRegistrationService("First"));
    services.AddSingleton<MultiRegistrationService>(new MultiRegistrationService("Second"));
    services.AddSingleton<MultiRegistrationService>(new MultiRegistrationService("Last"));
    var provider = new TestCoverageServiceProvider(services);

    // Act
    var service = provider.GetService<MultiRegistrationService>();

    // Assert
    Assert.That(service, Is.Not.Null);
    Assert.That(service!.Name, Is.EqualTo("Last"));
  }

  [Test]
  public void GetService_Scope_ReturnsScopedServiceProvider() {
    // Arrange
    var services = new ServiceCollection();
    var provider = new TestCoverageServiceProvider(services);

    // Act
    using var scope = provider.CreateScope();
    var scopeServiceProvider = scope.ServiceProvider.GetService<IServiceProvider>();

    // Assert
    Assert.That(scopeServiceProvider, Is.SameAs(scope.ServiceProvider));
  }

  [Test]
  public void GetService_Scope_ReturnsRootForScopeFactory() {
    // Arrange
    var services = new ServiceCollection();
    var provider = new TestCoverageServiceProvider(services);

    // Act
    using var scope = provider.CreateScope();
    var scopeFactory = scope.ServiceProvider.GetService<IServiceScopeFactory>();

    // Assert
    Assert.That(scopeFactory, Is.SameAs(provider));
  }

  [Test]
  public void GetService_WithParameterizedConstructor_ResolvesParameters() {
    // Arrange
    var services = new ServiceCollection();
    services.AddSingleton<string>("Dependency string");
    services.AddSingleton<ServiceWithParameters>();
    var provider = new TestCoverageServiceProvider(services);

    // Act
    var service = provider.GetService<ServiceWithParameters>();

    // Assert
    Assert.That(service, Is.Not.Null);
    Assert.That(service!.DependencyValue, Is.EqualTo("Dependency string"));
  }

  [Test]
  public void GetService_WithOptionalParameter_UsesDefaultValue() {
    // Arrange
    var services = new ServiceCollection();
    services.AddSingleton<ServiceWithOptionalParameter>();
    var provider = new TestCoverageServiceProvider(services);

    // Act
    var service = provider.GetService<ServiceWithOptionalParameter>();

    // Assert
    Assert.That(service, Is.Not.Null);
    Assert.That(service!.OptionalValue, Is.EqualTo("default"));
  }

  [Test]
  public void GetService_WithMultipleConstructors_UsesLargestResolvable() {
    // Arrange
    var services = new ServiceCollection();
    services.AddSingleton<string>("Dependency string");
    services.AddSingleton<ServiceWithMultipleConstructors>();
    var provider = new TestCoverageServiceProvider(services);

    // Act
    var service = provider.GetService<ServiceWithMultipleConstructors>();

    // Assert
    Assert.That(service, Is.Not.Null);
    Assert.That(service!.ConstructorUsed, Is.EqualTo("string"));
  }

  [Test]
  public void GetService_WithKeyedParameter_ResolvesKeyedService() {
    // Arrange
    var services = new ServiceCollection();
    services.AddKeyedSingleton<string, string>("test-key", (_, _) => "Keyed dependency");
    services.AddSingleton<ServiceWithKeyedParameter>();
    var provider = new TestCoverageServiceProvider(services);

    // Act
    var service = provider.GetService<ServiceWithKeyedParameter>();

    // Assert
    Assert.That(service, Is.Not.Null);
    Assert.That(service!.KeyedValue, Is.EqualTo("Keyed dependency"));
  }
}

public class FactoryService(string value) {
  public string Value { get; } = value;

}

public class MultiRegistrationService(string name) {
  public string Name { get; } = name;

}

public class ServiceWithParameters(string dependencyValue) {
  public string DependencyValue { get; } = dependencyValue;

}

public class ServiceWithOptionalParameter(string optionalValue = "default") {
  public string OptionalValue { get; } = optionalValue;

}

public class ServiceWithMultipleConstructors {
  public string ConstructorUsed { get; }

  // Default constructor
  public ServiceWithMultipleConstructors() {
    ConstructorUsed = "default";
  }

  // Constructor with dependency
  public ServiceWithMultipleConstructors(string dependency) {
    ConstructorUsed = "string";
  }

  // Constructor with multiple dependencies - shouldn't be used as List<int> can't be resolved
  public ServiceWithMultipleConstructors(string dependency, List<int> values) {
    ConstructorUsed = "multiple";
  }
}

public class ServiceWithKeyedParameter([FromKeyedServices("test-key")] string keyedValue) {
  public string KeyedValue { get; } = keyedValue;

}
