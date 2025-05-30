﻿using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Retro.FastInject.Annotations;
using Retro.FastInject.Generation;
using Retro.FastInject.Model.Manifest;
using Retro.FastInject.Tests.Utils;
using Retro.FastInject.Utils;

namespace Retro.FastInject.Tests.ServiceHierarchy;

[TestFixture]
public class ServiceManifestTest {
  private ServiceManifest _manifest;

  private readonly ImmutableArray<Type> _references = [
      typeof(object),
      typeof(ServiceScope),
      typeof(FromKeyedServicesAttribute),
      typeof(Logger<>)
  ];

  [SetUp]
  public void Setup() {
    _manifest = new ServiceManifest();
  }

  [Test]
  public void CheckConstructorDependencies_UnboundGenericService_SpecializesCorrectly() {
    // Create a generic service class and a consumer that depends on it
    const string code = """
                        using System.Collections.Generic;

                        namespace Test {
                          public class GenericService<T> {
                            public T Value { get; set; }
                          }
                          
                          public class Consumer {
                            public Consumer(GenericService<string> service) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var consumerType = compilation.GetTypeSymbol("Test.Consumer");
    var genericServiceType = compilation.GetTypeSymbol("Test.GenericService`1");

    // Register the open generic
    _manifest.AddService(genericServiceType, ServiceScope.Singleton);

    // Arrange
    var registration = new ServiceRegistration { Type = consumerType };

    // Act & Assert - should resolve GenericService<string> from open generic registration
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(registration, compilation));

    // Verify the constructor resolution
    var resolution = _manifest.GetAllConstructorResolutions().FirstOrDefault(r =>
        SymbolEqualityComparer.Default.Equals(r.Type, consumerType));

    Assert.That(resolution, Is.Not.Null);
    Assert.That(resolution.Parameters, Has.Count.EqualTo(1));

    var paramResolution = resolution.Parameters[0];
    Assert.That(paramResolution.Parameter.Type.ToDisplayString(), Is.EqualTo("Test.GenericService<string>"));
  }

  [Test]
  public void CheckConstructorDependencies_MultipleGenericSpecializations_ResolveCorrectly() {
    // Create a generic service with multiple specializations
    const string code = """
                        namespace Test {
                          public interface IGenericRepo<T> { }
                          
                          public class GenericRepo<T> : IGenericRepo<T> { }
                          
                          public class StringConsumer {
                            public StringConsumer(IGenericRepo<string> repo) { }
                          }
                          
                          public class IntConsumer {
                            public IntConsumer(IGenericRepo<int> repo) { }
                          }
                          
                          public class MultiConsumer {
                            public MultiConsumer(IGenericRepo<string> stringRepo, IGenericRepo<int> intRepo) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var stringConsumerType = compilation.GetTypeSymbol("Test.StringConsumer");
    var intConsumerType = compilation.GetTypeSymbol("Test.IntConsumer");
    var multiConsumerType = compilation.GetTypeSymbol("Test.MultiConsumer");
    var genericRepoType = compilation.GetTypeSymbol("Test.GenericRepo`1");
    var genericRepoInterfaceType = compilation.GetTypeSymbol("Test.IGenericRepo`1");

    // Register the generic repository
    _manifest.AddService(genericRepoType, ServiceScope.Singleton);
    _manifest.AddService(genericRepoInterfaceType, ServiceScope.Singleton, genericRepoType);

    // Register and validate consumers
    var stringConsumerRegistration = new ServiceRegistration { Type = stringConsumerType };
    var intConsumerRegistration = new ServiceRegistration { Type = intConsumerType };
    var multiConsumerRegistration = new ServiceRegistration { Type = multiConsumerType };

    // All should resolve without errors
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(stringConsumerRegistration, compilation));
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(intConsumerRegistration, compilation));
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(multiConsumerRegistration, compilation));

    // Verify the correct specializations in multi consumer
    var multiResolution = _manifest.GetAllConstructorResolutions().FirstOrDefault(r =>
        SymbolEqualityComparer.Default.Equals(r.Type, multiConsumerType));

    Assert.That(multiResolution, Is.Not.Null);
    Assert.That(multiResolution.Parameters, Has.Count.EqualTo(2));
    Assert.Multiple(() => {
      Assert.That(multiResolution.Parameters[0].Parameter.Type.ToDisplayString(), Is.EqualTo("Test.IGenericRepo<string>"));
      Assert.That(multiResolution.Parameters[1].Parameter.Type.ToDisplayString(), Is.EqualTo("Test.IGenericRepo<int>"));
    });
  }

  [Test]
  public void CheckConstructorDependencies_ComplexGenericStructure_SpecializesCorrectly() {
    // Create a nested generic structure to test
    const string code = """
                        using System.Collections.Generic;

                        namespace Test {
                          public interface IRepository<T> { }
                          
                          public class Repository<T> : IRepository<T> { }
                          
                          public interface IService<TRepo, TEntity> where TRepo : IRepository<TEntity> { }
                          
                          public class Service<TRepo, TEntity> : IService<TRepo, TEntity> where TRepo : IRepository<TEntity> { }
                          
                          public class User { }
                          
                          public class ComplexConsumer {
                            public ComplexConsumer(IService<IRepository<User>, User> userService) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var consumerType = compilation.GetTypeSymbol("Test.ComplexConsumer");
    var repositoryType = compilation.GetTypeSymbol("Test.Repository`1");
    var repositoryInterfaceType = compilation.GetTypeSymbol("Test.IRepository`1");
    var serviceType = compilation.GetTypeSymbol("Test.Service`2");
    var serviceInterfaceType = compilation.GetTypeSymbol("Test.IService`2");

    // Register open generics
    _manifest.AddService(repositoryType, ServiceScope.Singleton);
    _manifest.AddService(repositoryInterfaceType, ServiceScope.Singleton, repositoryType);
    _manifest.AddService(serviceType, ServiceScope.Singleton);
    _manifest.AddService(serviceInterfaceType, ServiceScope.Singleton, serviceType);

    // Arrange
    var registration = new ServiceRegistration { Type = consumerType };

    // Act & Assert - should resolve the complex generic structure
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(registration, compilation));

    // Verify the constructor resolution
    var resolution = _manifest.GetAllConstructorResolutions().FirstOrDefault(r =>
        SymbolEqualityComparer.Default.Equals(r.Type, consumerType));

    Assert.That(resolution, Is.Not.Null);
    Assert.That(resolution.Parameters, Has.Count.EqualTo(1));

    var paramResolution = resolution.Parameters[0];
    Assert.That(paramResolution.Parameter.Type.ToDisplayString(),
        Is.EqualTo("Test.IService<Test.IRepository<Test.User>, Test.User>"));
  }

  [Test]
  public void CheckConstructorDependencies_GenericMethodDependency_ResolvesCorrectly() {
    // Create a class with a generic factory method that needs to be specialized
    const string code = """
                        namespace Test {
                          public class Item { }
                          
                          public class Consumer {
                            public Consumer(Factory<Item> factory) { }
                          }
                          
                          public class Factory<T> {
                            public T Create() => default;
                          }
                          
                          public static class GenericFactory {
                            public static Factory<T> CreateFactory<T>() {
                              return new Factory<T>();
                            }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var consumerType = compilation.GetTypeSymbol("Test.Consumer");
    var factoryType = compilation.GetTypeSymbol("Test.Factory`1");
    var factoryMethod = compilation.GetMethodSymbol("Test.GenericFactory", "CreateFactory");

    // Register the factory type with the generic factory method
    _manifest.AddService(factoryType, ServiceScope.Singleton, associatedSymbol: factoryMethod);

    // Arrange
    var registration = new ServiceRegistration { Type = consumerType };

    // Act & Assert - should resolve the factory with the correct type argument
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(registration, compilation));

    // Verify the constructor resolution
    var resolution = _manifest.GetAllConstructorResolutions().FirstOrDefault(r =>
        SymbolEqualityComparer.Default.Equals(r.Type, consumerType));

    Assert.That(resolution, Is.Not.Null);
    Assert.That(resolution.Parameters, Has.Count.EqualTo(1));

    var paramResolution = resolution.Parameters[0];
    Assert.That(paramResolution.Parameter.Type.ToDisplayString(), Is.EqualTo("Test.Factory<Test.Item>"));
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
  public void ValidateDependencyGraph_WithCircularDependency_ThrowsInvalidOperationException() {
    // Create a class structure with circular dependency: A → B → C → A
    const string code = """
                        namespace Test {
                          public class ServiceA {
                            public ServiceA(ServiceB b) { }
                          }
                          
                          public class ServiceB {
                            public ServiceB(ServiceC c) { }
                          }
                          
                          public class ServiceC {
                            public ServiceC(ServiceA a) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var serviceAType = compilation.GetTypeSymbol("Test.ServiceA");
    var serviceBType = compilation.GetTypeSymbol("Test.ServiceB");
    var serviceCType = compilation.GetTypeSymbol("Test.ServiceC");

    // Register services in the manifest
    var regA = _manifest.AddService(serviceAType, ServiceScope.Singleton);
    var regB = _manifest.AddService(serviceBType, ServiceScope.Singleton);
    var regC = _manifest.AddService(serviceCType, ServiceScope.Singleton);

    // Check dependencies to build the constructor resolutions
    _manifest.CheckConstructorDependencies(regA, compilation);
    _manifest.CheckConstructorDependencies(regB, compilation);
    _manifest.CheckConstructorDependencies(regC, compilation);

    // Act & Assert
    var exception = Assert.Throws<InvalidOperationException>(() => _manifest.ValidateDependencyGraph());

    // Verify the exception message contains the circular dependency information
    Assert.That(exception.Message, Does.Contain("Detected circular dependency:"));
    Assert.That(exception.Message, Does.Contain("ServiceA"));
    Assert.That(exception.Message, Does.Contain("ServiceB"));
    Assert.That(exception.Message, Does.Contain("ServiceC"));
    Assert.That(exception.Message, Does.Contain("→")); // Contains the arrow character used in formatting
  }

  [Test]
  public void ValidateDependencyGraph_WithValidDependencies_Succeeds() {
    // Create a class structure with valid dependencies: A → B → C (no cycles)
    const string code = """
                        namespace Test {
                          public class ServiceC { 
                            public ServiceC() { }
                          }
                          
                          public class ServiceB {
                            public ServiceB(ServiceC c) { }
                          }
                          
                          public class ServiceA {
                            public ServiceA(ServiceB b) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var serviceAType = compilation.GetTypeSymbol("Test.ServiceA");
    var serviceBType = compilation.GetTypeSymbol("Test.ServiceB");
    var serviceCType = compilation.GetTypeSymbol("Test.ServiceC");

    // Register services in the manifest
    var regC = _manifest.AddService(serviceCType, ServiceScope.Singleton);
    var regB = _manifest.AddService(serviceBType, ServiceScope.Singleton);
    var regA = _manifest.AddService(serviceAType, ServiceScope.Singleton);

    // Check dependencies to build the constructor resolutions
    _manifest.CheckConstructorDependencies(regC, compilation);
    _manifest.CheckConstructorDependencies(regB, compilation);
    _manifest.CheckConstructorDependencies(regA, compilation);

    // Act & Assert - should not throw an exception
    Assert.DoesNotThrow(() => _manifest.ValidateDependencyGraph());
  }

  [Test]
  public void ValidateDependencyGraph_WithValueTypeAndNullableValueTypeCycle_ThrowsInvalidOperationException() {
    // Create a class structure with circular dependency involving a value type and its nullable version
    const string code = """
                        namespace Test {
                          public struct ValueService {
                            public ValueService(NullableValueConsumer consumer) { }
                          }
                          
                          public class NullableValueConsumer {
                            public NullableValueConsumer(ValueService? valueService) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var valueServiceType = compilation.GetTypeSymbol("Test.ValueService");
    var nullableConsumerType = compilation.GetTypeSymbol("Test.NullableValueConsumer");

    // Register services in the manifest
    var regValueService = _manifest.AddService(valueServiceType, ServiceScope.Singleton);
    var regNullableConsumer = _manifest.AddService(nullableConsumerType, ServiceScope.Singleton);

    // Check dependencies to build the constructor resolutions
    _manifest.CheckConstructorDependencies(regValueService, compilation);
    _manifest.CheckConstructorDependencies(regNullableConsumer, compilation);

    // Act & Assert
    var exception = Assert.Throws<InvalidOperationException>(() => _manifest.ValidateDependencyGraph());

    // Verify the exception message contains the circular dependency information
    Assert.That(exception.Message, Does.Contain("Detected circular dependency:"));
    Assert.That(exception.Message, Does.Contain("ValueService"));
    Assert.That(exception.Message, Does.Contain("NullableValueConsumer"));
    Assert.That(exception.Message, Does.Contain("→")); // Contains the arrow character used in formatting
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

  [Test]
  public void CheckConstructorDependencies_WithInterfacePlugins_ResolveCorrectCollection() {
    // Create a test with interface-based plugins and collection injection
    const string code = """
                        using System.Collections.Immutable;

                        namespace Test {
                          public interface IBasePlugin {}

                          public class GenericPlugin<T> : IBasePlugin { }
                          
                          public class ConcretePlugin : IBasePlugin { }
                          
                          // This consumer expects ALL IBasePlugin implementations
                          public class CollectionConsumer {
                            public CollectionConsumer(ImmutableArray<IBasePlugin> plugins) { }
                          }
                          
                          public class StringConsumer {
                            public StringConsumer(GenericPlugin<string> plugin) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);

    // Get all type symbols
    var basePluginType = compilation.GetTypeSymbol("Test.IBasePlugin");
    var genericPluginType = compilation.GetTypeSymbol("Test.GenericPlugin`1");
    var concretePluginType = compilation.GetTypeSymbol("Test.ConcretePlugin");
    var collectionConsumerType = compilation.GetTypeSymbol("Test.CollectionConsumer");
    var stringConsumerType = compilation.GetTypeSymbol("Test.StringConsumer");

    // Step 1: Register all the plugins
    // Register concrete plugin
    _manifest.AddService(concretePluginType, ServiceScope.Singleton);
    _manifest.AddService(basePluginType, ServiceScope.Singleton, concretePluginType);

    // Register generic plugin
    _manifest.AddService(genericPluginType, ServiceScope.Singleton);
    _manifest.AddService(basePluginType, ServiceScope.Singleton, genericPluginType);

    // Step 2: Register and resolve consumers
    var collectionConsumerReg = new ServiceRegistration { Type = collectionConsumerType };
    var stringConsumerReg = new ServiceRegistration { Type = stringConsumerType };

    // Resolve dependencies for both consumers
    _manifest.CheckConstructorDependencies(collectionConsumerReg, compilation);
    _manifest.CheckConstructorDependencies(stringConsumerReg, compilation);

    // Step 3: Verify that StringConsumer resolved its dependency correctly
    var stringResolution = _manifest.GetAllConstructorResolutions()
        .First(r => SymbolEqualityComparer.Default.Equals(r.Type, stringConsumerType));

    Assert.That(stringResolution.Parameters, Has.Count.EqualTo(1));
    Assert.That(stringResolution.Parameters[0].Parameter.Type.ToDisplayString(),
        Is.EqualTo("Test.GenericPlugin<string>"));

    // Step 4: Verify that CollectionConsumer has a collection with both plugins
    var collectionResolution = _manifest.GetAllConstructorResolutions()
        .First(r => SymbolEqualityComparer.Default.Equals(r.Type, collectionConsumerType));

    Assert.That(collectionResolution.Parameters, Has.Count.EqualTo(1));

    var collectedServices = collectionResolution.Parameters[0].SelectedService?.CollectedServices;
    Assert.That(collectedServices, Is.Not.Null);
    Assert.That(collectedServices, Has.Count.EqualTo(2), "Collection should contain exactly 2 plugins");
  }

  [Test]
  public void ValidateDependencyGraph_WithIEnumerableCircularDependency_ThrowsInvalidOperationException() {
    // Create a class structure with circular dependency involving an IEnumerable:
    // ServiceA depends on IEnumerable<IService> which includes ServiceB
    // ServiceB depends on ServiceA, creating a cycle
    const string code = """
                        using System.Collections.Generic;

                        namespace Test {
                          public interface IService { }
                          
                          public class ServiceA {
                            public ServiceA(IEnumerable<IService> services) { }
                          }
                          
                          public class ServiceB : IService {
                            public ServiceB(ServiceA serviceA) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var serviceAType = compilation.GetTypeSymbol("Test.ServiceA");
    var serviceBType = compilation.GetTypeSymbol("Test.ServiceB");
    var serviceInterfaceType = compilation.GetTypeSymbol("Test.IService");

    // Register services in the manifest
    var regA = _manifest.AddService(serviceAType, ServiceScope.Singleton);
    var regB = _manifest.AddService(serviceBType, ServiceScope.Singleton);

    // Register interface implementations
    _manifest.AddService(serviceInterfaceType, ServiceScope.Singleton, serviceBType);

    // Check dependencies to build the constructor resolutions
    _manifest.CheckConstructorDependencies(regA, compilation);
    _manifest.CheckConstructorDependencies(regB, compilation);

    // Act & Assert
    var exception = Assert.Throws<InvalidOperationException>(() => _manifest.ValidateDependencyGraph());

    // Verify the exception message contains the circular dependency information
    Assert.That(exception.Message, Does.Contain("Detected circular dependency:"));
    Assert.That(exception.Message, Does.Contain("ServiceA"));
    Assert.That(exception.Message, Does.Contain("ServiceB"));
    Assert.That(exception.Message, Does.Contain("→")); // Contains the arrow character used in formatting
  }

  [Test]
  public void ValidateDependencyGraph_WithLazySingletonCircularDependency_Succeeds() {
    // Create a class structure with circular dependency, but one uses Lazy<T>
    const string code = """
                        using System;

                        namespace Test {
                          public class ServiceA {
                            public ServiceA(Lazy<ServiceB> serviceB) { }
                          }
                          
                          public class ServiceB {
                            public ServiceB(ServiceA serviceA) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var serviceAType = compilation.GetTypeSymbol("Test.ServiceA");
    var serviceBType = compilation.GetTypeSymbol("Test.ServiceB");

    // Register services in the manifest as singletons
    var regA = _manifest.AddService(serviceAType, ServiceScope.Singleton);
    var regB = _manifest.AddService(serviceBType, ServiceScope.Singleton);

    // Check dependencies to build the constructor resolutions
    _manifest.CheckConstructorDependencies(regA, compilation);
    _manifest.CheckConstructorDependencies(regB, compilation);

    // Act & Assert - should not throw because Lazy<T> breaks the cycle
    Assert.DoesNotThrow(() => _manifest.ValidateDependencyGraph());
  }

  [Test]
  public void ValidateDependencyGraph_WithLazyScopedCircularDependency_Succeeds() {
    // Create a class structure with circular dependency where one is scoped, one is singleton
    const string code = """
                        using System;

                        namespace Test {
                          public class ServiceA {
                            public ServiceA(Lazy<ServiceB> serviceB) { }
                          }
                          
                          public class ServiceB {
                            public ServiceB(ServiceA serviceA) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var serviceAType = compilation.GetTypeSymbol("Test.ServiceA");
    var serviceBType = compilation.GetTypeSymbol("Test.ServiceB");

    // Register one as singleton, one as scoped
    var regA = _manifest.AddService(serviceAType, ServiceScope.Singleton);
    var regB = _manifest.AddService(serviceBType, ServiceScope.Scoped);

    // Check dependencies to build the constructor resolutions
    _manifest.CheckConstructorDependencies(regA, compilation);
    _manifest.CheckConstructorDependencies(regB, compilation);

    // Act & Assert - should not throw because Lazy<T> breaks the cycle
    Assert.DoesNotThrow(() => _manifest.ValidateDependencyGraph());
  }

  [Test]
  public void ValidateDependencyGraph_WithLazyTransientCircularDependency_ThrowsInvalidOperationException() {
    // Create a class structure with circular dependency where both are transient
    const string code = """
                        using System;

                        namespace Test {
                          public class ServiceA {
                            public ServiceA(Lazy<ServiceB> serviceB) { }
                          }
                          
                          public class ServiceB {
                            public ServiceB(ServiceA serviceA) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var serviceAType = compilation.GetTypeSymbol("Test.ServiceA");
    var serviceBType = compilation.GetTypeSymbol("Test.ServiceB");

    // Register both as transient
    var regA = _manifest.AddService(serviceAType, ServiceScope.Transient);
    var regB = _manifest.AddService(serviceBType, ServiceScope.Transient);

    // Check dependencies to build the constructor resolutions
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(regB, compilation));
    var exception = Assert.Throws<InvalidOperationException>(() => _manifest.CheckConstructorDependencies(regA, compilation));

    // Verify the exception message contains the circular dependency information
    Assert.That(exception.Message, Does.Contain("Lazy transient cycle detected"));
    Assert.That(exception.Message, Does.Contain("ServiceA"));
    Assert.That(exception.Message, Does.Contain("ServiceB"));
  }

  [Test]
  public void ValidateDependencyGraph_WithLazyMixedCircularDependency_Succeeds() {
    // Create a more complex structure with multiple services and mixed lifetime scopes
    const string code = """
                        using System;

                        namespace Test {
                          public interface IService { }
                          
                          public class ServiceA : IService {
                            public ServiceA(Lazy<ServiceB> serviceB) { }
                          }
                          
                          public class ServiceB {
                            public ServiceB(IService service) { }
                          }
                          
                          public class ServiceC {
                            public ServiceC() { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var serviceAType = compilation.GetTypeSymbol("Test.ServiceA");
    var serviceBType = compilation.GetTypeSymbol("Test.ServiceB");
    var serviceCType = compilation.GetTypeSymbol("Test.ServiceC");
    var serviceInterfaceType = compilation.GetTypeSymbol("Test.IService");

    // Register the services with different lifetimes
    var regA = _manifest.AddService(serviceAType, ServiceScope.Singleton);
    var regB = _manifest.AddService(serviceBType, ServiceScope.Scoped);
    var regC = _manifest.AddService(serviceCType, ServiceScope.Transient);

    // Register interface implementations
    _manifest.AddService(serviceInterfaceType, ServiceScope.Singleton, serviceAType);

    // Check dependencies to build the constructor resolutions
    _manifest.CheckConstructorDependencies(regA, compilation);
    _manifest.CheckConstructorDependencies(regB, compilation);
    _manifest.CheckConstructorDependencies(regC, compilation);

    // Act & Assert - should not throw because Lazy<T> breaks the cycle
    Assert.DoesNotThrow(() => _manifest.ValidateDependencyGraph());
  }

  [Test]
  public void CheckConstructorDependencies_WithGenericFactoryMethod_SpecializesCorrectly() {
    // Create a class structure with a generic factory method
    const string code = """
                        using Retro.FastInject.Annotations;

                        namespace Test {
                          public interface IRepository<T> { }
                          
                          public class Repository<T> : IRepository<T> { }
                          
                          public static class RepositoryFactory {
                            [Factory]
                            public static IRepository<T> Create<T>() {
                              return new Repository<T>();
                            }
                          }
                          
                          public class StringConsumer {
                            public StringConsumer(IRepository<string> repo) { }
                          }
                          
                          public class IntConsumer {
                            public IntConsumer(IRepository<int> repo) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var stringConsumerType = compilation.GetTypeSymbol("Test.StringConsumer");
    var intConsumerType = compilation.GetTypeSymbol("Test.IntConsumer");
    var repoInterfaceType = compilation.GetTypeSymbol("Test.IRepository`1");
    var factoryMethod = compilation.GetMethodSymbol("Test.RepositoryFactory", "Create");

    // Register the factory method that can create specialized repositories
    _manifest.AddService(repoInterfaceType, ServiceScope.Singleton, associatedSymbol: factoryMethod);

    // Arrange
    var stringConsumerReg = new ServiceRegistration { Type = stringConsumerType };
    var intConsumerReg = new ServiceRegistration { Type = intConsumerType };

    // Act & Assert
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(stringConsumerReg, compilation));
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(intConsumerReg, compilation));

    // Verify the correct specializations were resolved
    var stringResolution = _manifest.GetAllConstructorResolutions()
        .FirstOrDefault(r => SymbolEqualityComparer.Default.Equals(r.Type, stringConsumerType));

    var intResolution = _manifest.GetAllConstructorResolutions()
        .FirstOrDefault(r => SymbolEqualityComparer.Default.Equals(r.Type, intConsumerType));

    Assert.That(stringResolution, Is.Not.Null);
    Assert.That(stringResolution.Parameters, Has.Count.EqualTo(1));
    Assert.Multiple(() => {
      Assert.That(stringResolution.Parameters[0].Parameter.Type.ToDisplayString(), Is.EqualTo("Test.IRepository<string>"));

      Assert.That(intResolution, Is.Not.Null);
    });
    Assert.That(intResolution.Parameters, Has.Count.EqualTo(1));
    Assert.That(intResolution.Parameters[0].Parameter.Type.ToDisplayString(), Is.EqualTo("Test.IRepository<int>"));
  }

  [Test]
  public void CheckConstructorDependencies_WithMultipleGenericFactoryMethods_ResolvesCorrectly() {
    // Create a class structure with multiple generic factory methods
    const string code = """
                        using Retro.FastInject.Annotations;
                        using Microsoft.Extensions.DependencyInjection;

                        namespace Test {
                          public interface IService<T> { }
                          
                          public class DefaultService<T> : IService<T> { }
                          
                          public class SpecializedStringService : IService<string> { }
                          
                          public static class ServiceFactory {
                            [Factory]
                            public static IService<T> CreateDefault<T>() {
                              return new DefaultService<T>();
                            }
                            
                            [Factory(Key = "specialized")]
                            public static IService<string> CreateStringService() {
                              return new SpecializedStringService();
                            }
                          }
                          
                          public class DefaultConsumer {
                            public DefaultConsumer(IService<int> intService) { }
                          }
                          
                          public class SpecializedConsumer {
                            public SpecializedConsumer([FromKeyedServices("specialized")] IService<string> stringService) { }
                          }
                          
                          public class RegularConsumer {
                            public RegularConsumer(IService<string> stringService) { }
                          }
                        }
                        """;

    var compilation = GeneratorTestHelpers.CreateCompilation(code, _references);
    var defaultConsumerType = compilation.GetTypeSymbol("Test.DefaultConsumer");
    var specializedConsumerType = compilation.GetTypeSymbol("Test.SpecializedConsumer");
    var regularConsumerType = compilation.GetTypeSymbol("Test.RegularConsumer");
    var serviceInterfaceType = compilation.GetTypeSymbol("Test.IService`1");
    var specializedInterfaceType = serviceInterfaceType.GetInstantiatedGeneric(compilation.GetSpecialType(SpecialType.System_String));
    var defaultFactoryMethod = compilation.GetMethodSymbol("Test.ServiceFactory", "CreateDefault");
    var specializedFactoryMethod = compilation.GetMethodSymbol("Test.ServiceFactory", "CreateStringService");

    // Register the factory methods
    _manifest.AddService(serviceInterfaceType, ServiceScope.Singleton, associatedSymbol: defaultFactoryMethod);
    _manifest.AddService(specializedInterfaceType, ServiceScope.Singleton, associatedSymbol: specializedFactoryMethod, key: "specialized");

    // Arrange
    var defaultConsumerReg = new ServiceRegistration { Type = defaultConsumerType };
    var specializedConsumerReg = new ServiceRegistration { Type = specializedConsumerType };
    var regularConsumerReg = new ServiceRegistration { Type = regularConsumerType };

    // Act & Assert
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(defaultConsumerReg, compilation));
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(specializedConsumerReg, compilation));
    Assert.DoesNotThrow(() => _manifest.CheckConstructorDependencies(regularConsumerReg, compilation));

    // Verify the correct specializations and factory methods were resolved
    var defaultResolution = _manifest.GetAllConstructorResolutions()
        .FirstOrDefault(r => SymbolEqualityComparer.Default.Equals(r.Type, defaultConsumerType));

    var specializedResolution = _manifest.GetAllConstructorResolutions()
        .FirstOrDefault(r => SymbolEqualityComparer.Default.Equals(r.Type, specializedConsumerType));

    var regularResolution = _manifest.GetAllConstructorResolutions()
        .FirstOrDefault(r => SymbolEqualityComparer.Default.Equals(r.Type, regularConsumerType));

    Assert.That(defaultResolution, Is.Not.Null);
    Assert.That(defaultResolution.Parameters, Has.Count.EqualTo(1));
    Assert.Multiple(() => {
      Assert.That(defaultResolution.Parameters[0].Parameter.Type.ToDisplayString(), Is.EqualTo("Test.IService<int>"));

      Assert.That(specializedResolution, Is.Not.Null);
    });
    Assert.That(specializedResolution.Parameters, Has.Count.EqualTo(1));
    Assert.Multiple(() => {
      Assert.That(specializedResolution.Parameters[0].Key, Is.EqualTo("specialized"));

      Assert.That(regularResolution, Is.Not.Null);
    });
    Assert.That(regularResolution.Parameters, Has.Count.EqualTo(1));
    Assert.That(regularResolution.Parameters[0].Parameter.Type.ToDisplayString(), Is.EqualTo("Test.IService<string>"));
  }
}