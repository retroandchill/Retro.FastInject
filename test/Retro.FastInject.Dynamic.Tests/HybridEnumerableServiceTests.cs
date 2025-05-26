using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Retro.FastInject.Annotations;

namespace Retro.FastInject.Dynamic.Tests;

[ServiceProvider(AllowDynamicRegistrations = true)]
[Singleton<CompileTimePlugin>]
[Singleton<PluginConsumer>]
[Singleton<DynamicPluginConsumer>]
[Singleton<MixedPluginConsumer>]
public sealed partial class TestHybridEnumerableServiceProvider;

public interface IPlugin {
  string GetName();
}

public class CompileTimePlugin : IPlugin {
  public string GetName() => "Compile-time Plugin";
}

public class DynamicPluginA : IPlugin {
  public string GetName() => "Dynamic Plugin A";
}

public class DynamicPluginB : IPlugin {
  public string GetName() => "Dynamic Plugin B";
}

public class PluginConsumer {
  public IEnumerable<IPlugin> Plugins { get; }

  public PluginConsumer(IEnumerable<IPlugin> plugins) {
    Plugins = plugins;
  }

  public int Count => Plugins.Count();

  public bool Contains(string pluginName) =>
      Plugins.Any(p => p.GetName() == pluginName);
}

public class DynamicPluginConsumer {
  public IEnumerable<IPlugin> Plugins { get; }

  public DynamicPluginConsumer([AllowDynamic] IEnumerable<IPlugin> plugins) {
    Plugins = plugins;
  }

  public int Count => Plugins.Count();

  public bool Contains(string pluginName) =>
      Plugins.Any(p => p.GetName() == pluginName);
}

public class MixedPluginConsumer {
  private readonly CompileTimePlugin _compileTimePlugin;
  private readonly IEnumerable<IPlugin> _dynamicPlugins;

  public MixedPluginConsumer(
      CompileTimePlugin compileTimePlugin,
      [AllowDynamic] IEnumerable<IPlugin> dynamicPlugins) {
    _compileTimePlugin = compileTimePlugin;
    _dynamicPlugins = dynamicPlugins;
  }

  public bool HasCompileTimePlugin => _compileTimePlugin != null;

  public int DynamicPluginsCount => _dynamicPlugins.Count();

  public IEnumerable<string> GetAllPluginNames() {
    yield return _compileTimePlugin.GetName();

    foreach (var plugin in _dynamicPlugins) {
      yield return plugin.GetName();
    }
  }
}

public interface IUnusedPlugin;

/// <summary>
/// Tests for enumerable service injection in hybrid service providers.
/// </summary>
[TestFixture]
public class HybridEnumerableServiceTests {

  [Test]
  public void GetService_EnumerableConsumer_InjectsCompileTimeImplementations() {
    // Arrange
    var services = new ServiceCollection();
    var provider = new TestHybridEnumerableServiceProvider(services);

    // Act
    var consumer = provider.GetService<PluginConsumer>();

    // Assert
    Assert.That(consumer, Is.Not.Null);
    Assert.Multiple(() => {
      Assert.That(consumer!.Count, Is.EqualTo(1), "Should inject only the compile-time plugin");
      Assert.That(consumer.Contains("Compile-time Plugin"), Is.True);
    });
  }

  [Test]
  public void GetService_EnumerableConsumer_InjectsAllImplementations() {
    // Arrange
    var services = new ServiceCollection();
    services.AddSingleton<IPlugin, DynamicPluginA>();
    services.AddSingleton<IPlugin, DynamicPluginB>();
    var provider = new TestHybridEnumerableServiceProvider(services);

    // Act
    var consumer = provider.GetService<DynamicPluginConsumer>();

    // Assert
    Assert.That(consumer, Is.Not.Null);
    Assert.That(consumer!.Count, Is.EqualTo(3), "Should inject all 3 plugin implementations");

    Assert.Multiple(() => {
      Assert.That(consumer.Contains("Compile-time Plugin"), Is.True);
      Assert.That(consumer.Contains("Dynamic Plugin A"), Is.True);
      Assert.That(consumer.Contains("Dynamic Plugin B"), Is.True);
    });
  }

  [Test]
  public void GetService_MixedPluginConsumer_InjectsBothTypes() {
    // Arrange
    var services = new ServiceCollection();
    services.AddSingleton<IPlugin, DynamicPluginA>();
    services.AddSingleton<IPlugin, DynamicPluginB>();
    var provider = new TestHybridEnumerableServiceProvider(services);

    // Act
    var consumer = provider.GetService<MixedPluginConsumer>();

    // Assert
    Assert.That(consumer, Is.Not.Null);

    Assert.Multiple(() => {
      Assert.That(consumer!.HasCompileTimePlugin, Is.True, "Should have compile-time plugin");
      Assert.That(consumer.DynamicPluginsCount, Is.EqualTo(3), "Should inject 2 dynamic plugins and one compile-time plugin");
    });

    var pluginNames = consumer!.GetAllPluginNames().ToList();
    Assert.That(pluginNames, Has.Count.EqualTo(4));

    Assert.Multiple(() => {
      Assert.That(pluginNames, Contains.Item("Compile-time Plugin"));
      Assert.That(pluginNames, Contains.Item("Dynamic Plugin A"));
      Assert.That(pluginNames, Contains.Item("Dynamic Plugin B"));
    });
  }

  [Test]
  public void GetService_EnumerableOfInterface_ReturnsAllImplementations() {
    // Arrange
    var services = new ServiceCollection();
    services.AddSingleton<IPlugin, DynamicPluginA>();
    services.AddSingleton<IPlugin, DynamicPluginB>();
    var provider = new TestHybridEnumerableServiceProvider(services);

    // Act
    var plugins = provider.GetService<IEnumerable<IPlugin>>()?.ToList();

    // Assert
    Assert.That(plugins, Is.Not.Null);
    Assert.That(plugins, Has.Count.EqualTo(3), "Should return all 3 plugin implementations");

    var names = plugins!.Select(p => p.GetName()).ToList();

    Assert.Multiple(() => {
      Assert.That(names, Contains.Item("Compile-time Plugin"));
      Assert.That(names, Contains.Item("Dynamic Plugin A"));
      Assert.That(names, Contains.Item("Dynamic Plugin B"));
    });
  }

  [Test]
  public void GetService_EnumerableOfUnusedType_ReturnsEmptyArray() {
    // Arrange
    var services = new ServiceCollection();
    services.AddSingleton<IPlugin, DynamicPluginA>();
    services.AddSingleton<IPlugin, DynamicPluginB>();
    var provider = new TestHybridEnumerableServiceProvider(services);

    // Act
    var plugins = provider.GetService<IEnumerable<IUnusedPlugin>>()?.ToList();

    // Assert
    Assert.That(plugins, Is.Not.Null);
    Assert.That(plugins, Has.Count.EqualTo(0), "Should not find any plugins");
  }
}