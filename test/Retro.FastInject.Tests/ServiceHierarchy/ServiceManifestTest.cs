using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
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
                                                          _manifest.CheckConstructorDependencies(registration, compilation));
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
                                                          _manifest.CheckConstructorDependencies(registration, compilation));
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
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(registration, compilation));
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
                                                          _manifest.CheckConstructorDependencies(registration, compilation));
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
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(registration, compilation));
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
    var dependencyInterface = compilation.GetTypeSymbol("Test.IDependency");
    var dependencyType = compilation.GetTypeSymbol("Test.Dependency");

    // Register the dependency
    _manifest.AddService(dependencyType, ServiceScope.Singleton);
    _manifest.AddService(dependencyInterface, ServiceScope.Singleton, dependencyType);

    // Arrange
    var registration = new ServiceRegistration { Type = serviceType };

    // Act & Assert
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(registration, compilation));
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
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(registration, compilation));
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
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(registration, compilation));
  }

  [Test]
  public void CheckConstructorDependencies_WithIEnumerableDependency_Succeeds() {
    // Create a class with an IEnumerable dependency
    const string code = """
                        using System.Collections.Generic;

                        namespace Test {
                          public interface IPlugin { }
                          
                          public class Plugin1 : IPlugin { }
                          
                          public class Plugin2 : IPlugin { }
                          
                          public class ServiceWithIEnumerable {
                            public ServiceWithIEnumerable(IEnumerable<IPlugin> plugins) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var serviceType = compilation.GetTypeSymbol("Test.ServiceWithIEnumerable");
    var pluginInterface = compilation.GetTypeSymbol("Test.IPlugin");
    var plugin1Type = compilation.GetTypeSymbol("Test.Plugin1");
    var plugin2Type = compilation.GetTypeSymbol("Test.Plugin2");

    // Register the plugin implementations
    _manifest.AddService(plugin1Type, ServiceScope.Singleton);
    _manifest.AddService(pluginInterface, ServiceScope.Singleton, plugin1Type);
    _manifest.AddService(plugin2Type, ServiceScope.Singleton);
    _manifest.AddService(pluginInterface, ServiceScope.Singleton, plugin2Type);

    // Arrange
    var registration = new ServiceRegistration { Type = serviceType };

    // Act & Assert
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(registration, compilation));

    // Verify that the constructor resolution has been stored
    var resolution = _manifest.GetAllConstructorResolutions().FirstOrDefault(r =>
                                                                                 SymbolEqualityComparer.Default.Equals(r.Type, serviceType));

    Assert.That(resolution, Is.Not.Null);
    Assert.That(resolution.Parameters, Has.Count.EqualTo(1));

    var paramResolution = resolution.Parameters[0];
    Assert.That(paramResolution.Parameter.Type.ToDisplayString(),
                Is.EqualTo("System.Collections.Generic.IEnumerable<Test.IPlugin>"));
  }

  [Test]
  public void CheckConstructorDependencies_WithIReadOnlyCollectionDependency_Succeeds() {
    // Create a class with an IReadOnlyCollection dependency
    const string code = """
                        using System.Collections.Generic;

                        namespace Test {
                          public interface IStrategy { }
                          
                          public class StrategyA : IStrategy { }
                          
                          public class StrategyB : IStrategy { }
                          
                          public class ServiceWithReadOnlyCollection {
                            public ServiceWithReadOnlyCollection(IReadOnlyCollection<IStrategy> strategies) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var serviceType = compilation.GetTypeSymbol("Test.ServiceWithReadOnlyCollection");
    var strategyInterface = compilation.GetTypeSymbol("Test.IStrategy");
    var strategyAType = compilation.GetTypeSymbol("Test.StrategyA");
    var strategyBType = compilation.GetTypeSymbol("Test.StrategyB");

    // Register the strategy implementations
    _manifest.AddService(strategyAType, ServiceScope.Singleton);
    _manifest.AddService(strategyInterface, ServiceScope.Singleton, strategyAType);
    _manifest.AddService(strategyBType, ServiceScope.Singleton);
    _manifest.AddService(strategyInterface, ServiceScope.Singleton, strategyBType);

    // Arrange
    var registration = new ServiceRegistration { Type = serviceType };

    // Act & Assert
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(registration, compilation));

    // Verify that the constructor resolution has been stored
    var resolution = _manifest.GetAllConstructorResolutions().FirstOrDefault(r =>
                                                                                 SymbolEqualityComparer.Default.Equals(r.Type, serviceType));

    Assert.That(resolution, Is.Not.Null);
    Assert.That(resolution.Parameters, Has.Count.EqualTo(1));

    var paramResolution = resolution.Parameters[0];
    Assert.That(paramResolution.Parameter.Type.ToDisplayString(),
                Is.EqualTo("System.Collections.Generic.IReadOnlyCollection<Test.IStrategy>"));
  }

  [Test]
  public void CheckConstructorDependencies_WithIReadOnlyListDependency_Succeeds() {
    // Create a class with an IReadOnlyList dependency
    const string code = """
                        using System.Collections.Generic;

                        namespace Test {
                          public interface IHandler { }
                          
                          public class HandlerOne : IHandler { }
                          
                          public class HandlerTwo : IHandler { }
                          
                          public class ServiceWithReadOnlyList {
                            public ServiceWithReadOnlyList(IReadOnlyList<IHandler> handlers) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var serviceType = compilation.GetTypeSymbol("Test.ServiceWithReadOnlyList");
    var handlerInterface = compilation.GetTypeSymbol("Test.IHandler");
    var handler1Type = compilation.GetTypeSymbol("Test.HandlerOne");
    var handler2Type = compilation.GetTypeSymbol("Test.HandlerTwo");

    // Register the handler implementations
    _manifest.AddService(handler1Type, ServiceScope.Singleton);
    _manifest.AddService(handlerInterface, ServiceScope.Singleton, handler1Type);
    _manifest.AddService(handler2Type, ServiceScope.Singleton);
    _manifest.AddService(handlerInterface, ServiceScope.Singleton, handler2Type);

    // Arrange
    var registration = new ServiceRegistration { Type = serviceType };

    // Act & Assert
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(registration, compilation));

    // Verify that the constructor resolution has been stored
    var resolution = _manifest.GetAllConstructorResolutions().FirstOrDefault(r =>
                                                                                 SymbolEqualityComparer.Default.Equals(r.Type, serviceType));

    Assert.That(resolution, Is.Not.Null);
    Assert.That(resolution.Parameters, Has.Count.EqualTo(1));

    var paramResolution = resolution.Parameters[0];
    Assert.That(paramResolution.Parameter.Type.ToDisplayString(),
                Is.EqualTo("System.Collections.Generic.IReadOnlyList<Test.IHandler>"));
  }

  [Test]
  public void CheckConstructorDependencies_WithImmutableArrayDependency_Succeeds() {
    // Create a class with an ImmutableArray dependency
    const string code = """
                        using System.Collections.Immutable;

                        namespace Test {
                          public interface IValidator { }
                          
                          public class ValidatorA : IValidator { }
                          
                          public class ValidatorB : IValidator { }
                          
                          public class ServiceWithImmutableArray {
                            public ServiceWithImmutableArray(ImmutableArray<IValidator> validators) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var serviceType = compilation.GetTypeSymbol("Test.ServiceWithImmutableArray");
    var validatorInterface = compilation.GetTypeSymbol("Test.IValidator");
    var validatorAType = compilation.GetTypeSymbol("Test.ValidatorA");
    var validatorBType = compilation.GetTypeSymbol("Test.ValidatorB");

    // Register the validator implementations
    _manifest.AddService(validatorAType, ServiceScope.Singleton);
    _manifest.AddService(validatorInterface, ServiceScope.Singleton, validatorAType);
    _manifest.AddService(validatorBType, ServiceScope.Singleton);
    _manifest.AddService(validatorInterface, ServiceScope.Singleton, validatorBType);

    // Arrange
    var registration = new ServiceRegistration { Type = serviceType };

    // Act & Assert
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(registration, compilation));

    // Verify that the constructor resolution has been stored
    var resolution = _manifest.GetAllConstructorResolutions().FirstOrDefault(r =>
                                                                                 SymbolEqualityComparer.Default.Equals(r.Type, serviceType));

    Assert.That(resolution, Is.Not.Null);
    Assert.That(resolution.Parameters, Has.Count.EqualTo(1));

    var paramResolution = resolution.Parameters[0];
    Assert.That(paramResolution.Parameter.Type.ToDisplayString(),
                Is.EqualTo("System.Collections.Immutable.ImmutableArray<Test.IValidator>"));
  }

  [Test]
  public void CheckConstructorDependencies_WithMultipleCollectionTypes_Succeeds() {
    // Create a class with multiple collection type dependencies
    const string code = """
                        using System.Collections.Generic;
                        using System.Collections.Immutable;

                        namespace Test {
                          public interface IFeature { }
                          
                          public class Feature1 : IFeature { }
                          
                          public class Feature2 : IFeature { }
                          
                          public class ServiceWithMultipleCollections {
                            public ServiceWithMultipleCollections(
                              IEnumerable<IFeature> allFeatures, 
                              IReadOnlyCollection<IFeature> featureCollection,
                              IReadOnlyList<IFeature> featureList,
                              ImmutableArray<IFeature> featureArray) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var serviceType = compilation.GetTypeSymbol("Test.ServiceWithMultipleCollections");
    var featureInterface = compilation.GetTypeSymbol("Test.IFeature");
    var feature1Type = compilation.GetTypeSymbol("Test.Feature1");
    var feature2Type = compilation.GetTypeSymbol("Test.Feature2");

    // Register the feature implementations
    _manifest.AddService(feature1Type, ServiceScope.Singleton);
    _manifest.AddService(featureInterface, ServiceScope.Singleton, feature1Type);
    _manifest.AddService(feature2Type, ServiceScope.Singleton);
    _manifest.AddService(featureInterface, ServiceScope.Singleton, feature2Type);

    // Arrange
    var registration = new ServiceRegistration { Type = serviceType };

    // Act & Assert
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(registration, compilation));

    // Verify that the constructor resolution has been stored
    var resolution = _manifest.GetAllConstructorResolutions().FirstOrDefault(r =>
                                                                                 SymbolEqualityComparer.Default.Equals(r.Type, serviceType));

    Assert.That(resolution, Is.Not.Null);
    Assert.That(resolution.Parameters, Has.Count.EqualTo(4));
  }

  [Test]
  public void CheckConstructorDependencies_WithEmptyCollectionDependency_Suceeds() {
    // Create a class with a collection dependency for which no implementations exist
    const string code = """
                        using System.Collections.Generic;

                        namespace Test {
                          public interface INotRegistered { }
                          
                          public class ServiceWithEmptyCollection {
                            public ServiceWithEmptyCollection(IEnumerable<INotRegistered> items) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var serviceType = compilation.GetTypeSymbol("Test.ServiceWithEmptyCollection");

    // Arrange (no implementations registered)
    var registration = new ServiceRegistration { Type = serviceType };

    // Act & Assert
    // This should fail because there are no implementations of INotRegistered
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(registration, compilation));

    // Verify that the constructor resolution has been stored
    var resolution = _manifest.GetAllConstructorResolutions().FirstOrDefault(r =>
                                                                                 SymbolEqualityComparer.Default.Equals(r.Type, serviceType));

    Assert.That(resolution, Is.Not.Null);
    Assert.That(resolution.Parameters, Has.Count.EqualTo(1));
  }

  [Test]
  public void CheckConstructorDependencies_WithEmptyCollectionDependency_RequireNonEmpty_Fails() {
    // Create a class with a collection dependency for which no implementations exist
    const string code = """
                        using System.Collections.Generic;
                        using Retro.FastInject.Annotations;

                        namespace Test {
                          public interface INotRegistered { }
                          
                          public class ServiceWithEmptyCollection {
                            public ServiceWithEmptyCollection([RequireNonEmpty] IEnumerable<INotRegistered> items) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var serviceType = compilation.GetTypeSymbol("Test.ServiceWithEmptyCollection");

    // Arrange (no implementations registered)
    var registration = new ServiceRegistration { Type = serviceType };

    // Act & Assert
    // This should fail because there are no implementations of INotRegistered
    var ex = Assert.Throws<InvalidOperationException>(() =>
                                                          _manifest.CheckConstructorDependencies(registration, compilation));

    Assert.That(ex?.Message, Contains.Substring("Cannot resolve the following dependencies"));
  }

  [Test]
  public void CheckConstructorDependencies_WithKeyedService_WrongKeyName_ThrowsInvalidOperationException() {
    // Create interface with implementation registered under a specific key
    const string code = """
                        using Retro.FastInject.Annotations;
                        using Microsoft.Extensions.DependencyInjection;

                        namespace Test {
                          public interface IKeyedService { }
                          
                          public class KeyedServiceImpl : IKeyedService { }
                          
                          public class WrongKeyConsumer {
                            public WrongKeyConsumer([FromKeyedServices("wrongKey")] IKeyedService service) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var serviceType = compilation.GetTypeSymbol("Test.WrongKeyConsumer");
    var interfaceType = compilation.GetTypeSymbol("Test.IKeyedService");
    var implType = compilation.GetTypeSymbol("Test.KeyedServiceImpl");

    // Register implementation with a different key than what's requested
    _manifest.AddService(implType, ServiceScope.Singleton);
    _manifest.AddService(interfaceType, ServiceScope.Singleton, implType, key: "correctKey");

    // Arrange
    var registration = new ServiceRegistration { Type = serviceType };

    // Act & Assert
    // This should fail because the wrong key is requested
    var ex = Assert.Throws<InvalidOperationException>(() =>
                                                          _manifest.CheckConstructorDependencies(registration, compilation));

    Assert.That(ex?.Message, Contains.Substring("Cannot resolve the following dependencies"));
    Assert.That(ex?.Message, Contains.Substring("with key 'wrongKey'"));
  }

  [Test]
  public void CheckConstructorDependencies_WithKeyedService_RequestNonKeyedService_ThrowsInvalidOperationException() {
    // Create service registered without a key but requested with one
    const string code = """
                        using Retro.FastInject.Annotations;
                        using Microsoft.Extensions.DependencyInjection;

                        namespace Test {
                          public interface INonKeyedService { }
                          
                          public class NonKeyedServiceImpl : INonKeyedService { }
                          
                          public class KeyRequestingConsumer {
                            public KeyRequestingConsumer([FromKeyedServices("someKey")] INonKeyedService service) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var serviceType = compilation.GetTypeSymbol("Test.KeyRequestingConsumer");
    var interfaceType = compilation.GetTypeSymbol("Test.INonKeyedService");
    var implType = compilation.GetTypeSymbol("Test.NonKeyedServiceImpl");

    // Register implementation without a key
    _manifest.AddService(implType, ServiceScope.Singleton);
    _manifest.AddService(interfaceType, ServiceScope.Singleton, implType); // No key specified

    // Arrange
    var registration = new ServiceRegistration { Type = serviceType };

    // Act & Assert
    // This should fail because we're requesting a keyed service but it's registered without a key
    var ex = Assert.Throws<InvalidOperationException>(() =>
                                                          _manifest.CheckConstructorDependencies(registration, compilation));

    Assert.That(ex?.Message, Contains.Substring("Cannot resolve the following dependencies"));
    Assert.That(ex?.Message, Contains.Substring("with key 'someKey'"));
  }

  [Test]
  public void CheckConstructorDependencies_WithMultipleServices_NoKey_ThrowsInvalidOperationException() {
    // Create interface with multiple implementations
    const string code = """
                        namespace Test {
                          public interface IMultiService { }
                          
                          public class ServiceImpl1 : IMultiService { }
                          
                          public class ServiceImpl2 : IMultiService { }
                          
                          public class ServiceConsumer {
                            public ServiceConsumer(IMultiService service) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var serviceType = compilation.GetTypeSymbol("Test.ServiceConsumer");
    var interfaceType = compilation.GetTypeSymbol("Test.IMultiService");
    var impl1Type = compilation.GetTypeSymbol("Test.ServiceImpl1");
    var impl2Type = compilation.GetTypeSymbol("Test.ServiceImpl2");

    // Register multiple implementations for the same interface
    _manifest.AddService(impl1Type, ServiceScope.Singleton);
    _manifest.AddService(interfaceType, ServiceScope.Singleton, impl1Type);
    _manifest.AddService(impl2Type, ServiceScope.Singleton);
    _manifest.AddService(interfaceType, ServiceScope.Singleton, impl2Type);

    // Arrange
    var registration = new ServiceRegistration { Type = serviceType };

    // Act & Assert
    // This should fail because there are multiple implementations of IMultiService without a key
    var ex = Assert.Throws<InvalidOperationException>(() =>
                                                          _manifest.CheckConstructorDependencies(registration, compilation));

    Assert.That(ex?.Message, Contains.Substring("Cannot resolve the following dependencies"));
    Assert.That(ex?.Message, Contains.Substring("Multiple registrations found: 2"));
  }

  [Test]
  public void CheckConstructorDependencies_WithMultipleServices_WithKey_Succeeds() {
    // Create interface with multiple implementations and use key to resolve
    const string code = """
                        using Retro.FastInject.Annotations;
                        using Microsoft.Extensions.DependencyInjection;

                        namespace Test {
                          public interface IMultiService { }
                          
                          public class ServiceImpl1 : IMultiService { }
                          
                          public class ServiceImpl2 : IMultiService { }
                          
                          public class ServiceConsumer {
                            public ServiceConsumer([FromKeyedServices("impl1")] IMultiService service) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var serviceType = compilation.GetTypeSymbol("Test.ServiceConsumer");
    var interfaceType = compilation.GetTypeSymbol("Test.IMultiService");
    var impl1Type = compilation.GetTypeSymbol("Test.ServiceImpl1");
    var impl2Type = compilation.GetTypeSymbol("Test.ServiceImpl2");

    // Register multiple implementations for the same interface with different keys
    _manifest.AddService(impl1Type, ServiceScope.Singleton, key: "impl1");
    _manifest.AddService(interfaceType, ServiceScope.Singleton, impl1Type, key: "impl1");
    _manifest.AddService(impl2Type, ServiceScope.Singleton, key: "impl2");
    _manifest.AddService(interfaceType, ServiceScope.Singleton, impl2Type, key: "impl2");

    // Arrange
    var registration = new ServiceRegistration { Type = serviceType };

    // Act & Assert
    // This should succeed because we use a key to disambiguate
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(registration, compilation));

    // Verify that the constructor resolution has been stored
    var resolution = _manifest.GetAllConstructorResolutions().FirstOrDefault(r =>
                                                                                 SymbolEqualityComparer.Default.Equals(r.Type, serviceType));

    Assert.That(resolution, Is.Not.Null);
    Assert.That(resolution.Parameters, Has.Count.EqualTo(1));

    var paramResolution = resolution.Parameters[0];
    Assert.Multiple(() => {
      Assert.That(paramResolution.Key, Is.EqualTo("impl1"));
      Assert.That(paramResolution.SelectedService, Is.Not.Null);
      Assert.That(SymbolEqualityComparer.Default.Equals(paramResolution.SelectedService?.Type, impl1Type), Is.True);
    });
  }

  [Test]
  public void CheckConstructorDependencies_WithMultipleServices_MixedCollectionAndSingular_Succeeds() {
    // Create a scenario with both collection and singular service injections
    const string code = """
                        using System.Collections.Generic;
                        using Retro.FastInject.Annotations;
                        using Microsoft.Extensions.DependencyInjection;

                        namespace Test {
                          public interface IMultiService { }
                          
                          public class ServiceImpl1 : IMultiService { }
                          
                          public class ServiceImpl2 : IMultiService { }
                          
                          public class ComplexServiceConsumer {
                            public ComplexServiceConsumer(
                                [FromKeyedServices("primary")] IMultiService primaryService,
                                IEnumerable<IMultiService> allServices) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var serviceType = compilation.GetTypeSymbol("Test.ComplexServiceConsumer");
    var interfaceType = compilation.GetTypeSymbol("Test.IMultiService");
    var impl1Type = compilation.GetTypeSymbol("Test.ServiceImpl1");
    var impl2Type = compilation.GetTypeSymbol("Test.ServiceImpl2");

    // Register multiple implementations with different keys
    _manifest.AddService(impl1Type, ServiceScope.Singleton, key: "primary");
    _manifest.AddService(interfaceType, ServiceScope.Singleton, impl1Type, key: "primary");
    _manifest.AddService(impl2Type, ServiceScope.Singleton, key: "secondary");
    _manifest.AddService(interfaceType, ServiceScope.Singleton, impl2Type, key: "secondary");

    // Arrange
    var registration = new ServiceRegistration { Type = serviceType };

    // Act & Assert
    // This should succeed - resolving both the keyed service and the collection
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(registration, compilation));

    // Verify that the constructor resolution has been stored
    var resolution = _manifest.GetAllConstructorResolutions().FirstOrDefault(r =>
                                                                                 SymbolEqualityComparer.Default.Equals(r.Type, serviceType));

    Assert.That(resolution, Is.Not.Null);
    Assert.That(resolution.Parameters, Has.Count.EqualTo(2));

    // First parameter should be the keyed service
    var keyedParamResolution = resolution.Parameters[0];
    Assert.Multiple(() => {
      Assert.That(keyedParamResolution.Key, Is.EqualTo("primary"));
      Assert.That(keyedParamResolution.SelectedService, Is.Not.Null);
    });

    // Second parameter should be the collection
    var collectionParamResolution = resolution.Parameters[1];
    Assert.That(collectionParamResolution.Parameter.Type.ToDisplayString(),
                Is.EqualTo("System.Collections.Generic.IEnumerable<Test.IMultiService>"));
  }

  [Test]
  public void CheckConstructorDependencies_WithMultipleServices_DifferentLifetimes_Succeeds() {
    // Create interface with multiple implementations with different lifetimes
    const string code = """
                        using Retro.FastInject.Annotations;
                        using Microsoft.Extensions.DependencyInjection;

                        namespace Test {
                          public interface IMixedLifetimeService { }
                          
                          public class SingletonImpl : IMixedLifetimeService { }
                          
                          public class TransientImpl : IMixedLifetimeService { }
                          
                          public class LifetimeConsumer {
                            public LifetimeConsumer([FromKeyedServices("singleton")] IMixedLifetimeService service) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var serviceType = compilation.GetTypeSymbol("Test.LifetimeConsumer");
    var interfaceType = compilation.GetTypeSymbol("Test.IMixedLifetimeService");
    var singletonType = compilation.GetTypeSymbol("Test.SingletonImpl");
    var transientType = compilation.GetTypeSymbol("Test.TransientImpl");

    // Register multiple implementations with different lifetimes
    _manifest.AddService(singletonType, ServiceScope.Singleton, key: "singleton");
    _manifest.AddService(interfaceType, ServiceScope.Singleton, singletonType, key: "singleton");
    _manifest.AddService(transientType, ServiceScope.Transient, key: "transient");
    _manifest.AddService(interfaceType, ServiceScope.Transient, transientType, key: "transient");

    // Arrange
    var registration = new ServiceRegistration { Type = serviceType };

    // Act & Assert
    // This should succeed because we use a key to disambiguate
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(registration, compilation));

    // Verify that the constructor resolution has been stored with correct lifetime
    var resolution = _manifest.GetAllConstructorResolutions().FirstOrDefault(r =>
                                                                                 SymbolEqualityComparer.Default.Equals(r.Type, serviceType));

    Assert.That(resolution, Is.Not.Null);
    Assert.That(resolution.Parameters, Has.Count.EqualTo(1));

    var paramResolution = resolution.Parameters[0];
    Assert.Multiple(() => {
      Assert.That(paramResolution.Key, Is.EqualTo("singleton"));
      Assert.That(paramResolution.SelectedService, Is.Not.Null);
    });
    Assert.Multiple(() => {
      Assert.That(paramResolution.SelectedService.Lifetime, Is.EqualTo(ServiceScope.Singleton));
      Assert.That(SymbolEqualityComparer.Default.Equals(paramResolution.SelectedService.Type, singletonType), Is.True);
    });
  }
}