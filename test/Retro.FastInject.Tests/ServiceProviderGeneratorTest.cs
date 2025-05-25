using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using Retro.FastInject.Annotations;
using static Retro.FastInject.Tests.Utils.GeneratorTestHelpers;

namespace Retro.FastInject.Tests;

public class ServiceProviderGeneratorTests {
  [Test]
  public async Task Generator_WithoutPartialKeyword_ShouldReportError() {
    // Arrange
    const string source = """
                          using Retro.FastInject.Annotations;
                          namespace TestNamespace {
                              [ServiceProvider]
                              public class TestServiceProvider {}
                          }
                          """;

    // Act
    var (diagnostics, _) = await RunGenerator(source);

    // Assert
    Assert.That(diagnostics, Has.Exactly(1).Items);
    var diagnostic = diagnostics.Single();
    Assert.Multiple(() => {
      Assert.That(diagnostic.Id, Is.EqualTo("FastInject001"));
      Assert.That(diagnostic.GetMessage(), Contains.Substring("must be declared partial"));
    });
  }

  [Test]
  public async Task Generator_WithMissingDependency_ShouldReportError() {
    // Arrange
    const string source = """
                          using Retro.FastInject.Annotations;
                          namespace TestNamespace {
                              public interface IService {
                                  void DoSomething(IMissingDependency dep);
                              }
                              public class Service : IService {
                                  public Service(IMissingDependency dep) {}
                                  public void DoSomething(IMissingDependency dep) {}
                              }
                              [ServiceProvider]
                              [Singleton<Service>]
                              public partial class TestServiceProvider {}
                          }
                          """;

    // Act
    var (diagnostics, _) = await RunGenerator(source);

    // Assert
    Assert.That(diagnostics, Has.Exactly(1).Items);
    var diagnostic = diagnostics.Single();
    Assert.Multiple(() => {
      Assert.That(diagnostic.Id, Is.EqualTo("FastInject002"));
      Assert.That(diagnostic.GetMessage(), Contains.Substring("dependencies"));
    });
  }

  [Test]
  public async Task Generator_WithValidServiceProvider_ShouldGenerateCode() {
    // Arrange
    const string source = """
                          using Retro.FastInject.Annotations;
                          namespace TestNamespace {
                              [ServiceProvider]
                              public partial class TestServiceProvider {}
                          }
                          """;

    // Act
    var (diagnostics, output) = await RunGenerator(source);

    // Assert
    Assert.Multiple(() => {
      Assert.That(diagnostics, Is.Empty);
      Assert.That(output.SyntaxTrees.Count(), Is.GreaterThan(1));
    });
  }

  [Test]
  public async Task Generator_WithComplexDependencies_ShouldGenerateValidCode() {
    // Arrange
    const string source = """
                          using System;
                          using Retro.FastInject.Annotations;
                          namespace TestNamespace {
                              public interface IService {}
                              public class Service : IService {
                                  public Service(ILogger logger) {}
                              }
                              public interface ILogger {}
                              public class Logger : ILogger {}
                              
                              [ServiceProvider]
                              [Singleton<Service>]
                              [Singleton<Logger>]
                              public partial class TestServiceProvider {}
                          }
                          """;

    // Act
    var (diagnostics, output) = await RunGenerator(source);

    // Assert
    Assert.Multiple(() => {
      Assert.That(diagnostics, Is.Empty);
      Assert.That(output.SyntaxTrees.Count(), Is.GreaterThan(1));
    });
  }

  [Test]
  public async Task Generator_WithKeyedServices_ShouldGenerateValidCode() {
    // Arrange
    const string source = """
                          using Retro.FastInject.Annotations;
                          namespace TestNamespace {
                              public interface IService {}
                              public class ServiceA : IService {}
                              public class ServiceB : IService {}
                              
                              [ServiceProvider]
                              [Singleton<ServiceA>(Key = "A")]
                              [Singleton<ServiceB>(Key = "B")]
                              public partial class TestServiceProvider {}
                          }
                          """;

    // Act
    var (diagnostics, output) = await RunGenerator(source);

    // Assert
    Assert.Multiple(() => {
      Assert.That(diagnostics, Is.Empty);
      Assert.That(output.SyntaxTrees.Count(), Is.GreaterThan(1));
    });
  }

  [Test]
  public async Task Generator_WithDisposableServices_ShouldGenerateDisposalCode() {
    // Arrange
    const string source = """
                          using System;
                          using Retro.FastInject.Annotations;
                          namespace TestNamespace {
                              public interface IService : IDisposable {}
                              public class Service : IService {
                                  public void Dispose() {}
                              }
                              
                              [ServiceProvider]
                              [Singleton<Service>]
                              public partial class TestServiceProvider {}
                          }
                          """;

    // Act
    var (diagnostics, output) = await RunGenerator(source);

    // Assert
    Assert.Multiple(() => {
      Assert.That(diagnostics, Is.Empty);
      Assert.That(output.SyntaxTrees.Count(), Is.GreaterThan(1));
    });
  }

  [Test]
  public async Task Generator_WithGenericServices_ShouldGenerateValidCode() {
    // Arrange
    const string source = """
                          using System;
                          using Retro.FastInject.Annotations;
                          namespace TestNamespace {
                              public interface IDependency {}
                          
                              public interface IDependency<T> : IDependency {}
                              
                              public class Dependency<T> : IDependency<T> {}
                          
                              public interface IService {}
                              public class Service : IService {
                                public Service(IDependency<int> dependency) {}
                              }
                              
                              [ServiceProvider]
                              [Singleton<Service>]
                              [Transient(typeof(Dependency<>))]
                              public partial class TestServiceProvider {}
                          }
                          """;

    // Act
    var (diagnostics, output) = await RunGenerator(source);

    // Assert
    Assert.Multiple(() => {
      Assert.That(diagnostics, Is.Empty);
      Assert.That(output.SyntaxTrees.Count(), Is.GreaterThan(1));
    });
  }

  [Test]
  public async Task Generator_WithIEnumerableDependency_ShouldGenerateValidCode() {
    // Arrange
    const string source = """
                          using System.Collections.Generic;
                          using Retro.FastInject.Annotations;
                          namespace TestNamespace {
                              public interface IPlugin {}
                              
                              public class PluginA : IPlugin {}
                              
                              public class PluginB : IPlugin {}
                              
                              public class PluginService {
                                  public PluginService(IEnumerable<IPlugin> plugins) {}
                              }
                              
                              [ServiceProvider]
                              [Singleton<PluginA>]
                              [Singleton<PluginB>]
                              [Singleton<PluginService>]
                              public partial class TestServiceProvider {}
                          }
                          """;
  
    // Act
    var (diagnostics, output) = await RunGenerator(source);
  
    // Assert
    Assert.Multiple(() => {
      Assert.That(diagnostics, Is.Empty);
      Assert.That(output.SyntaxTrees.Count(), Is.GreaterThan(1));
    });
  }
  
  private static Task<(ImmutableArray<Diagnostic> Diagnostics, Compilation Output)> RunGenerator(string source) {
    var compilation = CreateCompilation(source, typeof(ServiceProviderAttribute));
    var generator = new ServiceProviderGenerator();
    var driver = CSharpGeneratorDriver.Create(generator);
    driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);
    return Task.FromResult((diagnostics, outputCompilation));
  }
}