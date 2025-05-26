using Retro.FastInject.Annotations;

namespace Retro.FastInject.Core.Tests;

[ServiceProvider]
[Singleton<ServiceA>]
[Singleton<ServiceB>]
[Singleton<IndependentService>]
public sealed partial class LazyServiceProvider;

// Service A depends on Service B through Lazy<T> to break the circular dependency
public class ServiceA(Lazy<ServiceB> serviceB) {

  public Lazy<ServiceB> LazyServiceB { get; } = serviceB;

  public string GetValue() => "Value from Service A";

  public string GetValueFromB() => LazyServiceB.Value.GetValue();
}

// Service B depends on Service A, creating a circular dependency
public class ServiceB(ServiceA serviceA) {

  public string GetValue() => "Value from Service B";

  public string GetValueFromA() => serviceA.GetValue();
}

// A service without circular dependencies for comparison
public class IndependentService {
  public string GetValue() => "Independent service value";
}

[TestFixture]
public class LazyInjectionTests {
  [Test]
  public void GetService_WithLazyInjection_ResolvesCircularDependency() {
    // Arrange
    var provider = new LazyServiceProvider();

    // Act
    var serviceA = provider.VerifyServiceResolved<ServiceA>();

    // Assert
    Assert.That(serviceA, Is.Not.Null);

    // This would cause a StackOverflowException without lazy injection
    var valueFromB = serviceA.GetValueFromB();
    Assert.That(valueFromB, Is.EqualTo("Value from Service B"));
  }

  [Test]
  public void GetService_WithCircularDependency_LazyValueResolvesProperly() {
    // Arrange
    var provider = new LazyServiceProvider();

    // Act
    var serviceA = provider.VerifyServiceResolved<ServiceA>();

    // ServiceB shouldn't be instantiated until Lazy<T>.Value is accessed
    var serviceB = serviceA.LazyServiceB.Value;

    // Assert
    Assert.That(serviceB, Is.Not.Null);
    Assert.That(serviceB.GetValueFromA(), Is.EqualTo("Value from Service A"));
  }

  [Test]
  public void GetService_LazyInjection_DeferredInitialization() {
    // Arrange
    var provider = new LazyServiceProvider();
    var serviceA = provider.VerifyServiceResolved<ServiceA>();

    // At this point, ServiceB should not be initialized yet
    Assert.That(serviceA.LazyServiceB.IsValueCreated, Is.False);;

    // Act & Assert

    // ServiceB is initialized only when accessing the Value property
    var serviceB = serviceA.LazyServiceB.Value;
    Assert.That(serviceB, Is.Not.Null);

    // Verify circular reference works properly
    var valueFromA = serviceB.GetValueFromA();
    Assert.That(valueFromA, Is.EqualTo("Value from Service A"));
  }
}