using Microsoft.Extensions.DependencyInjection;
using Retro.FastInject.Annotations;

namespace Retro.FastInject.Core.Tests;

[ServiceProvider]
[Singleton<GenericService<int>>]
[Singleton<GenericService<string>>]
[Singleton(typeof(GenericRepository<>))]
[Singleton<ServiceWithGenericDependencies>]
public partial class TestGenericServiceProvider;

public class GenericService<T> {
  public T? DefaultValue { get; private set; }

  public void SetValue(T value) {
    DefaultValue = value;
  }
}

public interface IRepository<T> {
  void Add(T item);
  T? Get(int id);
}

public class GenericRepository<T> : IRepository<T> {
  private T? _item;

  public void Add(T item) {
    _item = item;
  }

  public T? Get(int id) {
    return _item;
  }
}

public class ServiceWithGenericDependencies(
    GenericService<int> intService,
    GenericService<string> stringService,
    IRepository<int> intRepository) {
  public GenericService<int> IntService { get; } = intService;
  public GenericService<string> StringService { get; } = stringService;
  public IRepository<int> IntRepository { get; } = intRepository;

}

/// <summary>
/// Tests for generic service injection in compile-time service providers.
/// </summary>
[TestFixture]
public class GenericServiceTests {

  [Test]
  public void GetService_GenericServiceWithConcreteType_ReturnsInstance() {
    // Arrange
    var provider = new TestGenericServiceProvider();

    // Act
    var intService = provider.VerifyServiceResolved<GenericService<int>>();
    var stringService = provider.VerifyServiceResolved<GenericService<string>>();

    // Test that they are different instances for different type parameters
    intService.SetValue(42);
    stringService.SetValue("test");

    Assert.Multiple(() => {
      Assert.That(intService.DefaultValue, Is.EqualTo(42));
      Assert.That(stringService.DefaultValue, Is.EqualTo("test"));
    });
  }

  [Test]
  public void GetService_OpenGenericRegistration_ResolvesClosedGenerics_WhenDeclared() {
    // Arrange
    var provider = new TestGenericServiceProvider();

    // Act
    var intRepository = provider.GetService<IRepository<int>>();
    var stringRepository = provider.GetService<IRepository<string>>();

    Assert.Multiple(() => {
      // Assert
      Assert.That(intRepository, Is.Not.Null);
      Assert.That(stringRepository, Is.Null);
    });

    // Test that they are different instances for different type parameters
    intRepository.Add(42);

    Assert.That(intRepository.Get(1), Is.EqualTo(42));
  }

  [Test]
  public void GetService_ServiceWithGenericDependencies_InjectsAllDependencies() {
    // Arrange
    var provider = new TestGenericServiceProvider();

    // Act
    var service = provider.VerifyServiceResolved<ServiceWithGenericDependencies>();

    Assert.Multiple(() => {
      // Assert
      Assert.That(service.IntService, Is.Not.Null);
      Assert.That(service.StringService, Is.Not.Null);
      Assert.That(service.IntRepository, Is.Not.Null);
    });

    // Test each dependency works correctly
    service.IntService.SetValue(42);
    service.StringService.SetValue("test");
    service.IntRepository.Add(123);

    Assert.Multiple(() => {
      Assert.That(service.IntService.DefaultValue, Is.EqualTo(42));
      Assert.That(service.StringService.DefaultValue, Is.EqualTo("test"));
      Assert.That(service.IntRepository.Get(1), Is.EqualTo(123));
    });
  }

  [Test]
  public void GetService_ConcreteGenericRegistrations_ReturnsSameInstance() {
    // Arrange
    var provider = new TestGenericServiceProvider();

    // Act
    var intService1 = provider.GetService<GenericService<int>>();
    var intService2 = provider.GetService<GenericService<int>>();

    Assert.Multiple(() => {
      // Assert
      Assert.That(intService1, Is.Not.Null);
      Assert.That(intService2, Is.Not.Null);
    });
    Assert.That(intService1, Is.SameAs(intService2), "Singleton generic services should return the same instance");
  }
}