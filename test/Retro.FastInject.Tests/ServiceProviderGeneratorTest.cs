using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Retro.FastInject.Annotations;
using Xunit;
using Xunit.Abstractions;
using static Retro.FastInject.Tests.Utils.GeneratorTestHelpers;
using DependencyAttribute = System.Runtime.CompilerServices.DependencyAttribute;

namespace Retro.FastInject.Tests;

public class ServiceProviderGeneratorTests {
  private readonly ITestOutputHelper _output;

  public ServiceProviderGeneratorTests(ITestOutputHelper output) {
    _output = output;
  }

  [Fact]
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
    Assert.Empty(diagnostics);
    var generatedTrees = outputCompilation.SyntaxTrees.Except(compilation.SyntaxTrees).ToList();
    _output.WriteLine("Generated files count: " + generatedTrees.Count);
    foreach (var tree in generatedTrees) {
      _output.WriteLine("Generated file content:");
      _output.WriteLine(tree.ToString());
    }
  }

  [Fact]
  public async Task Generator_WithDependencyAttributes_ShouldProcessAllDependencies() {
    // Arrange
    const string source = """

                          using Retro.FastInject.Annotations;

                          namespace TestNamespace
                          {
                              public interface ITestService {}
                              public class TestService : ITestService {}
                              public interface IScopedService {}
                              public class ScopedService : IScopedService {}

                              [ServiceProvider]
                              [Singleton<TestService>]
                              [Scoped<ScopedService>]
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
    Assert.Empty(diagnostics);
    var generatedTrees = outputCompilation.SyntaxTrees.Except(compilation.SyntaxTrees).ToList();
    _output.WriteLine("Generated files count: " + generatedTrees.Count);
    foreach (var tree in generatedTrees) {
      _output.WriteLine("Generated file content:");
      _output.WriteLine(tree.ToString());
    }
  }

  [Fact]
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
    Assert.Empty(diagnostics);
    var generatedTrees = outputCompilation.SyntaxTrees.Except(compilation.SyntaxTrees).ToList();
    _output.WriteLine("Generated files count: " + generatedTrees.Count);
    foreach (var tree in generatedTrees) {
      _output.WriteLine("Generated file content:");
      _output.WriteLine(tree.ToString());
    }
  }
}