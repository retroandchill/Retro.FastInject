using System;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Retro.FastInject.Annotations;

namespace Retro.FastInject.Dynamic.Tests;

[ServiceProvider(AllowDynamicRegistrations = true)]
[Singleton<CompileTimeKeyedService>(Key = "compile-time")]
[Singleton<KeyedServiceConsumer>]
public partial class TestKeyedServiceProvider;

public interface IKeyedService {
  string GetValue();
}

public class CompileTimeKeyedService : IKeyedService {
  public string GetValue() => "Compile-time keyed service";
}

public class KeyedServiceConsumer(
    [FromKeyedServices("compile-time")] IKeyedService compiledKeyedService,
    [FromKeyedServices("dynamic"), AllowDynamic]
    IKeyedService? dynamicKeyedService = null) {

  public string GetCompiledValue() => compiledKeyedService.GetValue();
  public string? GetDynamicValue() => dynamicKeyedService?.GetValue();
  public bool HasDynamicService => dynamicKeyedService != null;
}

/// <summary>
/// Tests for keyed service resolution in hybrid service providers.
/// </summary>
[TestFixture]
public class KeyedServiceTests {

  [Test]
  public void CompileTimeKeyedService_ResolvesByKey() {
    // Arrange
    var services = new ServiceCollection();
    var provider = new TestKeyedServiceProvider(services);

    // Act
    var service = provider.GetKeyedService<IKeyedService>("compile-time");

    // Assert
    Assert.That(service, Is.Not.Null);
    Assert.That(service!.GetValue(), Is.EqualTo("Compile-time keyed service"));
  }

  [Test]
  public void DynamicKeyedService_ResolvesByKey() {
    // Arrange
    var services = new ServiceCollection();
    
    // Register a dynamic service with a key
    services.AddKeyedSingleton<IKeyedService, DynamicKeyedService>("dynamic");
    var provider = new TestKeyedServiceProvider(services);

    // Act
    var service = provider.GetKeyedService<IKeyedService>("dynamic");

    // Assert
    Assert.That(service, Is.Not.Null);
    Assert.That(service!.GetValue(), Is.EqualTo("Dynamic keyed service"));
  }

  [Test]
  public void KeyedServiceConsumer_ResolvesBothServices() {
    // Arrange
    var services = new ServiceCollection();
    services.AddKeyedSingleton<IKeyedService, DynamicKeyedService>("dynamic");
    var provider = new TestKeyedServiceProvider(services);

    // Act
    var consumer = provider.GetService<KeyedServiceConsumer>();

    // Assert
    Assert.That(consumer, Is.Not.Null);
    
    Assert.Multiple(() => {
      Assert.That(consumer!.HasDynamicService, Is.True);
      Assert.That(consumer.GetCompiledValue(), Is.EqualTo("Compile-time keyed service"));
      Assert.That(consumer.GetDynamicValue(), Is.EqualTo("Dynamic keyed service"));
    });
  }

  [Test]
  public void KeyedServiceConsumer_MissingDynamicService_StillWorks() {
    // Arrange
    var services = new ServiceCollection();
    var provider = new TestKeyedServiceProvider(services);

    // Act
    var consumer = provider.GetService<KeyedServiceConsumer>();

    // Assert
    Assert.That(consumer, Is.Not.Null);
    
    Assert.Multiple(() => {
      Assert.That(consumer!.HasDynamicService, Is.False);
      Assert.That(consumer.GetCompiledValue(), Is.EqualTo("Compile-time keyed service"));
      Assert.That(consumer.GetDynamicValue(), Is.Null);
    });
  }

  [Test]
  public void GetRequiredKeyedService_MissingKey_ThrowsException() {
    // Arrange
    var services = new ServiceCollection();
    var provider = new TestKeyedServiceProvider(services);

    // Act & Assert
    Assert.Throws<InvalidOperationException>(() => 
      provider.GetRequiredKeyedService<IKeyedService>("missing-key"));
  }
}

public class DynamicKeyedService : IKeyedService {
  public string GetValue() => "Dynamic keyed service";
}
