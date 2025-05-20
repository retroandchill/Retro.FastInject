using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using Retro.FastInject.Annotations;
using static Retro.FastInject.Tests.Utils.GeneratorTestHelpers;

namespace Retro.FastInject.Tests;

public class ServiceProviderGeneratorTests {

  [Test]
  public async Task Generator_WithBasicServiceProvider_ShouldFindServiceProviderAttribute() {
    // Arrange
    const string source = """

                          using Retro.FastInject.Annotations;

                          namespace TestNamespace
                          {
                              [ServiceProvider]
                              public partial class TestServiceProvider
                              {
                              }
                          }
                          """;

    var compilation = CreateCompilation(source, typeof(ServiceProviderAttribute));

    // Act
    var driver = CSharpGeneratorDriver.Create(new ServiceProviderGenerator());
    driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

    // Assert - Add breakpoint here to inspect the process
    Assert.That(diagnostics, Is.Empty);
    var generatedTrees = outputCompilation.SyntaxTrees.Except(compilation.SyntaxTrees).ToList();
    TestContext.WriteLine("Generated files count: " + generatedTrees.Count);
    foreach (var tree in generatedTrees) {
      TestContext.WriteLine("Generated file content:");
      TestContext.WriteLine(tree.ToString());
    }
  }

  [Test]
  public async Task Generator_WithDependencyAttributes_ShouldProcessAllDependencies() {
    // Arrange
    const string source = """

                          using System;
                          using Retro.FastInject.Annotations;

                          namespace TestNamespace
                          {
                              public interface ITestService {}
                              public class TestService : ITestService {}
                              public interface IScopedService {}
                              public class ScopedService : IScopedService {}
                              public interface ITransientService {}
                              public class TransientService : ITransientService, IDisposable, IAsyncDisposable {
                                public void Dispose() {
                                }
                                
                                public ValueTask DisposeAsync() {
                                  return default;
                                }
                              }

                              [ServiceProvider]
                              [Singleton<TestService>]
                              [Scoped<ScopedService>]
                              [Transient<TransientService>]
                              public partial class TestServiceProvider
                              {
                              }
                          }
                          """;

    var compilation = CreateCompilation(source, typeof(ServiceProviderAttribute));

    // Act
    var driver = CSharpGeneratorDriver.Create(new ServiceProviderGenerator());
    driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

    // Assert - Add breakpoint here to inspect the process
    Assert.That(diagnostics, Is.Empty);
    var generatedTrees = outputCompilation.SyntaxTrees.Except(compilation.SyntaxTrees).ToList();
    TestContext.WriteLine("Generated files count: " + generatedTrees.Count);
    foreach (var tree in generatedTrees) {
      TestContext.WriteLine("Generated file content:");
      TestContext.WriteLine(tree.ToString());
    }
  }

  [Test]
  public async Task Generator_WithKeyedServices_ShouldProcessKeyedDependencies() {
    // Arrange
    const string source = """

                          using Retro.FastInject.Annotations;

                          namespace TestNamespace
                          {
                              public interface IKeyed {}
                              public class KeyedService : IKeyed {}

                              [ServiceProvider]
                              [Singleton<KeyedService>(Key = "primary")]
                              [Singleton<KeyedService>(Key = "secondary")]
                              public partial class TestServiceProvider
                              {
                                [Instance]
                                public int Value { get; } = 1;
                              }
                          }
                          """;

    var compilation = CreateCompilation(source, typeof(ServiceProviderAttribute));

    // Act
    var driver = CSharpGeneratorDriver.Create(new ServiceProviderGenerator());
    driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

    // Assert - Add breakpoint here to inspect the process
    Assert.That(diagnostics, Is.Empty);
    var generatedTrees = outputCompilation.SyntaxTrees.Except(compilation.SyntaxTrees).ToList();
    TestContext.WriteLine("Generated files count: " + generatedTrees.Count);
    foreach (var tree in generatedTrees) {
      TestContext.WriteLine("Generated file content:");
      TestContext.WriteLine(tree.ToString());
    }
  }
}