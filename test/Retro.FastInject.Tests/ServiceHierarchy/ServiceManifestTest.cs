using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Retro.FastInject.Annotations;
using Retro.FastInject.ServiceHierarchy;
using Retro.FastInject.Tests.Utils;

namespace Retro.FastInject.Tests.ServiceHierarchy;

[TestFixture]
public class ServiceManifestTest {
  private ServiceManifest _manifest;

  private readonly ImmutableArray<Type> _references = [
      typeof(object),
      typeof(ServiceScope),
      typeof(FromKeyedServicesAttribute)
  ];

  [SetUp]
  public void Setup() {
    _manifest = new ServiceManifest();
  }

  [Test]
  public void CheckConstructorDependencies_NonNamedType_ThrowsInvalidOperationException() {
    // Create a type parameter which is not a named type
    const string code = """
                        namespace Test {
                          public class GenericClass<T> {
                            public T Value { get; set; }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var genericType = (INamedTypeSymbol)compilation.GetTypeSymbol("Test.GenericClass`1");
    var typeParam = genericType.TypeParameters[0]; // Get the type parameter T

    // Arrange
    var registration = new ServiceRegistration { Type = typeParam };

    // Act & Assert
    var ex = Assert.Throws<InvalidOperationException>(() =>
                                                          _manifest.CheckConstructorDependencies(registration));
    Assert.That(ex?.Message, Contains.Substring("is not a named type"));
  }

  [Test]
  public void CheckConstructorDependencies_MultiplePublicConstructors_ThrowsInvalidOperationException() {
    // Create a class with multiple constructors
    const string code = """
                        namespace Test {
                          public class MultipleConstructors {
                            public MultipleConstructors() { }
                            public MultipleConstructors(int value) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var typeSymbol = compilation.GetTypeSymbol("Test.MultipleConstructors");

    // Arrange
    var registration = new ServiceRegistration { Type = typeSymbol };

    // Act & Assert
    var ex = Assert.Throws<InvalidOperationException>(() =>
                                                          _manifest.CheckConstructorDependencies(registration));
    Assert.That(ex?.Message, Contains.Substring("has multiple public constructors"));
  }

  [Test]
  public void CheckConstructorDependencies_WithValidFactoryMethod_Succeeds() {
    // Create a class and factory method
    const string code = """
                              namespace Test {
                                public class ServiceClass { }
                                
                                public static class Factory {
                                  public static ServiceClass CreateService() {
                                    return new ServiceClass();
                                  }
                                }
                              }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var factoryMethod = compilation.GetMethodSymbol("Test.Factory", "CreateService");
    var serviceType = compilation.GetTypeSymbol("Test.ServiceClass");

    // Arrange
    var registration = new ServiceRegistration {
        Type = serviceType,
        AssociatedSymbol = factoryMethod
    };

    // Act & Assert
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(registration));
  }

  [Test]
  public void CheckConstructorDependencies_WithMissingDependencies_ThrowsInvalidOperationException() {
    // Create a class with dependency
    const string code = """
                              namespace Test {
                                public interface IDependency { }
                                
                                public class ServiceWithDependency {
                                  public ServiceWithDependency(IDependency dependency) { }
                                }
                              }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var serviceType = compilation.GetTypeSymbol("Test.ServiceWithDependency");

    // Arrange
    var registration = new ServiceRegistration { Type = serviceType };

    // Act & Assert
    var ex = Assert.Throws<InvalidOperationException>(() =>
                                                          _manifest.CheckConstructorDependencies(registration));
    Assert.That(ex?.Message, Contains.Substring("Cannot resolve the following dependencies"));
  }

  [Test]
  public void CheckConstructorDependencies_WithResolvableDependencies_Succeeds() {
    // Create a class with dependency
    const string code = """
                              namespace Test {
                                public interface IDependency { }
                                
                                public class ServiceWithDependency {
                                  public ServiceWithDependency(IDependency dependency) { }
                                }
                              }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var serviceType = compilation.GetTypeSymbol("Test.ServiceWithDependency");
    var dependencyType = compilation.GetTypeSymbol("Test.IDependency");

    // Register the dependency
    _manifest.AddService(dependencyType, ServiceScope.Singleton);

    // Arrange
    var registration = new ServiceRegistration { Type = serviceType };

    // Act & Assert
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(registration));
  }

  [Test]
  public void CheckConstructorDependencies_WithResolvableIndirectDependencies_Succeeds() {
    // Create a class with dependency
    const string code = """
                              namespace Test {
                                public interface IDependency { }
                                
                                public class Dependency : IDependency { }
                                
                                public class ServiceWithDependency {
                                  public ServiceWithDependency(IDependency dependency) { }
                                }
                              }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var serviceType = compilation.GetTypeSymbol("Test.ServiceWithDependency");
    var dependencyType = compilation.GetTypeSymbol("Test.Dependency");

    // Register the dependency
    _manifest.AddService(dependencyType, ServiceScope.Singleton);

    // Arrange
    var registration = new ServiceRegistration { Type = serviceType };

    // Act & Assert
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(registration));
  }

  [Test]
  public void CheckConstructorDependencies_WithNullableDependency_Succeeds() {
    // Create a class with nullable dependency
    const string code = """
                              namespace Test {
                                public interface IDependency { }
                                
                                public class ServiceWithNullableDependency {
                                  public ServiceWithNullableDependency(IDependency? dependency = null) { }
                                }
                              }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var serviceType = compilation.GetTypeSymbol("Test.ServiceWithNullableDependency");

    // Arrange
    var registration = new ServiceRegistration { Type = serviceType };

    // Act & Assert
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(registration));
  }

  [Test]
  public void CheckConstructorDependencies_WithKeyedDependency_Succeeds() {
    // Create a class with FromKeyedServices attribute
    var attributeCode = $$"""
                          using {{typeof(FromKeyedServicesAttribute).Namespace}};
                                
                          namespace Test {
                            public interface IDependency { }
                                  
                            public class ServiceWithKeyedDependency {
                              public ServiceWithKeyedDependency([FromKeyedServices("testKey")] IDependency dependency) { }
                            }
                          }
                          """;

    var compilation = GeneratorTestHelpers.CreateCompilation(attributeCode, _references);
    var serviceType = compilation.GetTypeSymbol("Test.ServiceWithKeyedDependency");
    var dependencyType = compilation.GetTypeSymbol("Test.IDependency");

    // Register the keyed dependency
    _manifest.AddService(dependencyType, ServiceScope.Singleton, key: "testKey");

    // Arrange
    var registration = new ServiceRegistration { Type = serviceType };

    // Act & Assert
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(registration));
  }
}