using System;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Retro.FastInject.Annotations;
using Retro.FastInject.Core.Tests;

namespace Retro.FastInject.Dynamic.Tests;

[ServiceProvider(AllowDynamicRegistrations = true)]
[Singleton<CompileTimeServiceA>]
[Singleton<CompileTimeServiceB>]
[Singleton<IndependentCompileTimeService>]
public sealed partial class HybridLazyServiceProvider;

// CompileTime Service A depends on Service B through Lazy<T> to break the circular dependency
public class CompileTimeServiceA(Lazy<CompileTimeServiceB> serviceB, [AllowDynamic] Lazy<RuntimeServiceA> runtimeServiceA) {

  public Lazy<CompileTimeServiceB> LazyServiceB { get; } = serviceB;
  public Lazy<RuntimeServiceA> LazyRuntimeServiceA { get; } = runtimeServiceA;

  public string GetValue() => "Value from CompileTimeServiceA";

  public string GetValueFromB() => LazyServiceB.Value.GetValue();

  public string GetValueFromRuntimeA() => LazyRuntimeServiceA.Value.GetValue();
}

// CompileTime Service B depends on Service A, creating a circular dependency
public class CompileTimeServiceB(CompileTimeServiceA serviceA, [AllowDynamic] Lazy<RuntimeServiceB> runtimeServiceB) {

  public Lazy<RuntimeServiceB> RuntimeServiceB { get; } = runtimeServiceB;

  public string GetValue() => "Value from CompileTimeServiceB";

  public string GetValueFromA() => serviceA.GetValue();

  public string GetValueFromRuntimeB() => RuntimeServiceB.Value.GetValue();
}

// A compile-time service without circular dependencies for comparison
public class IndependentCompileTimeService {
  public string GetValue() => "Independent compile-time service value";
}

// Runtime Service A - registered at runtime
public class RuntimeServiceA(Lazy<RuntimeServiceB> serviceB, Lazy<CompileTimeServiceA> compileTimeServiceA) {

  public string GetValue() => "Value from RuntimeServiceA";

  public string GetValueFromB() => serviceB.Value.GetValue();

  public string GetValueFromCompileTimeA() => compileTimeServiceA.Value.GetValue();
}

// Runtime Service B - registered at runtime
public class RuntimeServiceB(Lazy<RuntimeServiceA> serviceA, Lazy<CompileTimeServiceB> compileTimeServiceB) {

  public string GetValue() => "Value from RuntimeServiceB";

  public string GetValueFromA() => serviceA.Value.GetValue();

  public string GetValueFromCompileTimeB() => compileTimeServiceB.Value.GetValue();
}

[TestFixture]
public class HybridLazyInjectionTests {
  [Test]
  public void HybridProvider_WithLazyInjection_ResolvesCircularDependenciesBetweenCompileTimeServices() {
    // Arrange
    var services = new ServiceCollection();
    var provider = new HybridLazyServiceProvider(services);

    // Act
    var serviceA = provider.VerifyServiceResolved<CompileTimeServiceA>();

    // Assert
    Assert.That(serviceA, Is.Not.Null);

    // This would cause a StackOverflowException without lazy injection
    var valueFromB = serviceA.GetValueFromB();
    Assert.That(valueFromB, Is.EqualTo("Value from CompileTimeServiceB"));
  }

  [Test]
  public void HybridProvider_WithLazyInjection_ResolvesCircularDependenciesBetweenRuntimeServices() {
    // Arrange
    var services = new ServiceCollection();
    services.AddSingleton<RuntimeServiceA>();
    services.AddSingleton<RuntimeServiceB>();
    var provider = new HybridLazyServiceProvider(services);

    // Act
    var runtimeServiceA = provider.GetService<RuntimeServiceA>();

    // Assert
    Assert.That(runtimeServiceA, Is.Not.Null);

    // This would cause a StackOverflowException without lazy injection
    var valueFromB = runtimeServiceA.GetValueFromB();
    Assert.That(valueFromB, Is.EqualTo("Value from RuntimeServiceB"));
  }

  [Test]
  public void HybridProvider_WithLazyInjection_ResolvesCircularDependenciesBetweenCompileTimeAndRuntimeServices() {
    // Arrange
    var services = new ServiceCollection();
    services.AddSingleton<RuntimeServiceA>();
    services.AddSingleton<RuntimeServiceB>();
    var provider = new HybridLazyServiceProvider(services);

    // Act
    var compileTimeServiceA = provider.GetService<CompileTimeServiceA>();
    var runtimeServiceA = provider.GetService<RuntimeServiceA>();

    Assert.Multiple(() => {
      // Assert
      Assert.That(compileTimeServiceA, Is.Not.Null);
      Assert.That(runtimeServiceA, Is.Not.Null);
    });

    // Test circular dependencies between compile-time and runtime services
    var compileTimeValueFromRuntime = compileTimeServiceA.GetValueFromRuntimeA();
    Assert.That(compileTimeValueFromRuntime, Is.EqualTo("Value from RuntimeServiceA"));

    var runtimeValueFromCompileTime = runtimeServiceA.GetValueFromCompileTimeA();
    Assert.That(runtimeValueFromCompileTime, Is.EqualTo("Value from CompileTimeServiceA"));
  }

  [Test]
  public void HybridProvider_LazyInjection_DeferredInitialization() {
    // Arrange
    var services = new ServiceCollection();
    services.AddSingleton<RuntimeServiceA>();
    services.AddSingleton<RuntimeServiceB>();
    var provider = new HybridLazyServiceProvider(services);

    var compileTimeServiceA = provider.GetService<CompileTimeServiceA>();
    Assert.That(compileTimeServiceA, Is.Not.Null);

    // At this point, neither ServiceB nor RuntimeServiceA should be initialized yet

    // Act & Assert

    // CompileTimeServiceB is initialized only when accessing the Value property
    var compileTimeServiceB = compileTimeServiceA.LazyServiceB.Value;
    Assert.That(compileTimeServiceB, Is.Not.Null);

    // RuntimeServiceA is initialized only when accessing the Value property
    var runtimeServiceA = compileTimeServiceA.LazyRuntimeServiceA.Value;
    Assert.That(runtimeServiceA, Is.Not.Null);

    // Test deeply nested lazy resolution
    var runtimeServiceB = runtimeServiceA.GetValueFromB();
    Assert.That(runtimeServiceB, Is.EqualTo("Value from RuntimeServiceB"));

    // Complete the circular reference by going from RuntimeServiceB back to CompileTimeServiceB
    var runtimeB = compileTimeServiceB.RuntimeServiceB.Value;
    var circularValue = runtimeB.GetValueFromCompileTimeB();
    Assert.That(circularValue, Is.EqualTo("Value from CompileTimeServiceB"));
  }
}