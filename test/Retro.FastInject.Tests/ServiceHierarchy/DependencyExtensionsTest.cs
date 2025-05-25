using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Retro.FastInject.Annotations;
using Retro.FastInject.Generation;
using static Retro.FastInject.Tests.Utils.GeneratorTestHelpers;

namespace Retro.FastInject.Tests.ServiceHierarchy;

[TestFixture]
public class DependencyExtensionsTests {
  private Compilation _compilation;

  [SetUp]
  public void Setup() {
    const string source = """
                          using System;
                          using Retro.FastInject.Annotations;

                          namespace TestNamespace 
                          {
                              public interface IService {}
                              public class ServiceA : IService {}
                              public class ServiceB : IService {}
                              public class ServiceC : IService {}
                              public class ServiceD : IService {}
                              
                              [Singleton<ServiceA>]
                              [Scoped(typeof(ServiceB))]
                              [Dependency(typeof(ServiceC), ServiceScope.Transient, Key = "CustomKey")]
                              public class TestClass1 {}

                              [Import<TestClass1>]
                              [Singleton<ServiceD>]
                              public class TestClass2 {}
                              
                              [Import(typeof(TestClass1)]
                              [Singleton<ServiceD>]
                              public class TestClass3 {}
                          }
                          """;

    _compilation = CreateCompilation(source,
                                     typeof(SingletonAttribute),
                                     typeof(DependencyAttribute),
                                     typeof(ImportAttribute));
  }

  [Test]
  public void GetInjectedServices_WithGenericAttribute_ShouldReturnCorrectService() {
    // Arrange
    var testClass = GetTypeSymbol("TestClass1");

    // Act
    var services = testClass.GetInjectedServices().ToList();

    // Assert
    var serviceA = services.FirstOrDefault(s => s.Type.Name == "ServiceA");
    Assert.That(serviceA, Is.Not.Null);
    Assert.Multiple(() => {
      Assert.That(serviceA.Lifetime, Is.EqualTo(ServiceScope.Singleton));
      Assert.That(serviceA.Key, Is.Null);
    });
  }

  [Test]
  public void GetInjectedServices_WithExplicitTypeofDependency_ShouldReturnCorrectService() {
    // Arrange
    var testClass = GetTypeSymbol("TestClass1");

    // Act
    var services = testClass.GetInjectedServices().ToList();

    // Assert
    var serviceB = services.FirstOrDefault(s => s.Type.Name == "ServiceB");
    Assert.That(serviceB, Is.Not.Null);
    Assert.Multiple(() => {
      Assert.That(serviceB.Lifetime, Is.EqualTo(ServiceScope.Scoped));
      Assert.That(serviceB.Key, Is.Null);
    });
  }

  [Test]
  public void GetInjectedServices_WithKeyedDependency_ShouldReturnServiceWithKey() {
    // Arrange
    var testClass = GetTypeSymbol("TestClass1");

    // Act
    var services = testClass.GetInjectedServices().ToList();

    // Assert
    var serviceC = services.FirstOrDefault(s => s.Type.Name == "ServiceC");
    Assert.That(serviceC, Is.Not.Null);
    Assert.Multiple(() => {
      Assert.That(serviceC.Lifetime, Is.EqualTo(ServiceScope.Transient));
      Assert.That(serviceC.Key, Is.EqualTo("CustomKey"));
    });
  }

  [Test]
  public void GetInjectedServices_WithImportedServices_ShouldReturnAllServices() {
    // Arrange
    var testClass = GetTypeSymbol("TestClass2");

    // Act
    var services = testClass.GetInjectedServices().ToList();

    // Assert
    Assert.That(services, Has.Count.EqualTo(4)); // All services from TestClass1 + ServiceD
    Assert.Multiple(() => {
      Assert.That(services.Any(s => s.Type.Name == "ServiceD"), Is.True);
      Assert.That(services.Any(s => s.Type.Name == "ServiceA"), Is.True);
      Assert.That(services.Any(s => s.Type.Name == "ServiceB"), Is.True);
      Assert.That(services.Any(s => s.Type.Name == "ServiceC"), Is.True);
    });
  }

  [Test]
  public void GetInjectedServices_WithImportedServicesNonGeneric_ShouldReturnAllServices() {
    // Arrange
    var testClass = GetTypeSymbol("TestClass3");

    // Act
    var services = testClass.GetInjectedServices().ToList();

    // Assert
    Assert.That(services, Has.Count.EqualTo(4)); // All services from TestClass1 + ServiceD
    Assert.Multiple(() => {
      Assert.That(services.Any(s => s.Type.Name == "ServiceD"), Is.True);
      Assert.That(services.Any(s => s.Type.Name == "ServiceA"), Is.True);
      Assert.That(services.Any(s => s.Type.Name == "ServiceB"), Is.True);
      Assert.That(services.Any(s => s.Type.Name == "ServiceC"), Is.True);
    });
  }

  private ITypeSymbol GetTypeSymbol(string typeName) {
    return _compilation.GetTypeByMetadataName($"TestNamespace.{typeName}")
           ?? throw new InvalidOperationException($"Type {typeName} not found");
  }
}