using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Retro.FastInject.Annotations;

namespace Retro.FastInject.Core.Tests;

[ServiceProvider]
[Singleton<PluginA>]
[Singleton<PluginB>]
[Singleton<PluginC>]
[Singleton<PluginConsumer>]
[Singleton<ListConsumer>]
[Singleton<CollectionConsumer>]
public partial class TestEnumerableServiceProvider;

public interface IPlugin {
  string GetName();
}

public class PluginA : IPlugin {
  public string GetName() => "Plugin A";
}

public class PluginB : IPlugin {
  public string GetName() => "Plugin B";
}

public class PluginC : IPlugin {
  public string GetName() => "Plugin C";
}

public class PluginConsumer {
  public IEnumerable<IPlugin> Plugins { get; }

  public PluginConsumer(IEnumerable<IPlugin> plugins) {
    Plugins = plugins;
  }
}

public class ListConsumer {
  public IReadOnlyList<IPlugin> Plugins { get; }

  public ListConsumer(IReadOnlyList<IPlugin> plugins) {
    Plugins = plugins;
  }
}

public class CollectionConsumer {
  public IReadOnlyCollection<IPlugin> Plugins { get; }

  public CollectionConsumer(IReadOnlyCollection<IPlugin> plugins) {
    Plugins = plugins;
  }
}

/// <summary>
/// Tests for enumerable and collection service injection in compile-time service providers.
/// </summary>
[TestFixture]
public class EnumerableServiceTests {

  [Test]
  public void GetService_EnumerableConsumer_InjectsAllImplementations() {
    // Arrange
    var provider = new TestEnumerableServiceProvider();

    // Act
    var consumer = provider.VerifyServiceResolved<PluginConsumer>();

    // Assert
    Assert.That(consumer.Plugins, Is.Not.Null);
    var plugins = consumer.Plugins.ToList();
    Assert.That(plugins, Has.Count.EqualTo(3), "Should inject all 3 plugin implementations");

    var names = plugins.Select(p => p.GetName()).ToList();
    Assert.That(names, Contains.Item("Plugin A"));
    Assert.That(names, Contains.Item("Plugin B"));
    Assert.That(names, Contains.Item("Plugin C"));
  }

  [Test]
  public void GetService_ListConsumer_InjectsAllImplementations() {
    // Arrange
    var provider = new TestEnumerableServiceProvider();

    // Act
    var consumer = provider.VerifyServiceResolved<ListConsumer>();

    // Assert
    Assert.That(consumer.Plugins, Is.Not.Null);
    Assert.That(consumer.Plugins, Has.Count.EqualTo(3), "Should inject all 3 plugin implementations");

    var names = consumer.Plugins.Select(p => p.GetName()).ToList();
    Assert.That(names, Contains.Item("Plugin A"));
    Assert.That(names, Contains.Item("Plugin B"));
    Assert.That(names, Contains.Item("Plugin C"));
  }

  [Test]
  public void GetService_CollectionConsumer_InjectsAllImplementations() {
    // Arrange
    var provider = new TestEnumerableServiceProvider();

    // Act
    var consumer = provider.VerifyServiceResolved<CollectionConsumer>();

    // Assert
    Assert.That(consumer.Plugins, Is.Not.Null);
    Assert.That(consumer.Plugins, Has.Count.EqualTo(3), "Should inject all 3 plugin implementations");

    var names = consumer.Plugins.Select(p => p.GetName()).ToList();
    Assert.That(names, Contains.Item("Plugin A"));
    Assert.That(names, Contains.Item("Plugin B"));
    Assert.That(names, Contains.Item("Plugin C"));
  }

  [Test]
  public void GetService_EnumerableOfInterface_ReturnsAllImplementations() {
    // Arrange
    var provider = new TestEnumerableServiceProvider();

    // Act
    var plugins = provider.GetService<IEnumerable<IPlugin>>()?.ToList();

    // Assert
    Assert.That(plugins, Is.Not.Null);
    Assert.That(plugins, Has.Count.EqualTo(3), "Should return all 3 plugin implementations");

    var names = plugins.Select(p => p.GetName()).ToList();
    Assert.That(names, Contains.Item("Plugin A"));
    Assert.That(names, Contains.Item("Plugin B"));
    Assert.That(names, Contains.Item("Plugin C"));
  }
}