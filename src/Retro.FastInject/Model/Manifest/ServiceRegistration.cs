using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Retro.FastInject.Annotations;
using Retro.FastInject.Utils;

namespace Retro.FastInject.Model.Manifest;

/// <summary>
/// Represents a service registration within the dependency injection framework.
/// </summary>
/// <remarks>
/// A service registration encompasses details about the service type, its lifetime,
/// optional implementation type, keys for distinguishing registrations, and associated
/// metadata for resolving dependencies during runtime.
/// </remarks>
public record ServiceRegistration {
  /// Represents the type of the service being registered.
  /// This property is required and is used to define the type of the service
  /// in the dependency injection container.
  /// The value must be an implementation of the `ITypeSymbol` interface,
  /// which represents a type from the Roslyn compiler's type system.
  /// This property is crucial for identifying the type of the service
  /// and resolving it during dependency injection.
  public required ITypeSymbol Type { get; init; }

  /// Represents an optional key used to uniquely identify a specific service registration.
  /// This property allows multiple registrations of the same service type to be distinguished
  /// by using a unique key during resolution. If null, the registration can be resolved without
  /// specifying a key. When provided, services must explicitly request the key to resolve this
  /// particular registration.
  public string? Key { get; init; }

  /// Defines the lifetime of the service being registered within the dependency injection framework.
  /// The lifetime determines how and when the service instance is created and reused during its lifecycle.
  /// This property must be one of the defined values in the `ServiceScope` enumeration, which includes:
  /// Singleton, Scoped, and Transient.
  /// The specified value impacts the behavior of the service resolution, such as whether
  /// a single instance is shared across the application or new instances are created as needed.
  public ServiceScope Lifetime { get; init; }

  /// Specifies the concrete implementation type to be used when resolving the service.
  /// This property is optional and allows you to explicitly define the type that implements the service.
  /// If set to null, the system assumes that the service will act as its own implementation type.
  /// The value must implement `ITypeSymbol`, representing the Roslyn type system abstraction.
  /// It is particularly useful when the service type is an interface or an abstract class
  /// and requires a concrete implementation to be resolved.
  public ITypeSymbol? ImplementationType { get; init; }

  /// Represents the resolved type of the service registration.
  /// This property determines the type to be used during service resolution.
  /// If an `ImplementationType` is provided, it is used as the resolved type;
  /// otherwise, the `Type` property is used.
  /// This property ensures that the correct type is employed when resolving
  /// services in the dependency injection framework.
  public ITypeSymbol ResolvedType => ImplementationType ?? Type;

  /// Represents the symbol associated with the service being registered.
  /// This property can hold information about a specific method, property,
  /// field, or null if no explicit association exists.
  /// It is particularly useful for scenarios involving factory methods,
  /// initialization logic, or additional metadata.
  /// The `AssociatedSymbol` helps resolve how the service instance should be
  /// created or accessed during dependency injection.
  public ISymbol? AssociatedSymbol { get; init; }

  /// Represents a list of services that are part of a service collection registration.
  /// This property is utilized when registering multiple services as a collection,
  /// allowing aggregation of individual service registrations under a single parent.
  /// Each element in the list corresponds to a specific service registration.
  /// If the service registration does not represent a collection, this property may be null.
  public List<ServiceRegistration>? CollectedServices { get; init; }

  /// Represents the index of the specific type registration within the list of registrations for the same service type.
  /// This property is automatically assigned when a service is registered and reflects the order in which
  /// the registrations for the given type were added.
  /// It is primarily used for distinguishing multiple registrations of the same type, allowing unique identification
  /// of each registration in scenarios where multiple service implementations are provided for a single service type.
  /// The value is zero-based and increments sequentially for each additional registration of the same type.
  public int IndexForType { get; init; }

  /// Represents the unique field name associated with the service registration.
  /// This property generates a field name based on the type name of the service,
  /// combined with an optional key or index. The purpose of this property is to
  /// ensure that each service is associated with a unique, deterministic field name,
  /// facilitating proper resolution and management within the dependency injection system.
  public string FieldName {
    get {
      var suffix = "";
      if (IndexForType > 0) {
        suffix = $"_{IndexForType}";
      }

      return $"_{Type.GetSanitizedTypeName()}{suffix}";
    }
  }

  /// Indicates whether the registered service implements the `IDisposable` interface.
  /// This property is determined based on whether the service type or its implementation
  /// explicitly implements `IDisposable`. It allows the dependency injection system to
  /// identify services that require disposal and handle them appropriately during the
  /// cleanup process.
  public bool IsDisposable { get; init; }

  /// Indicates whether the service being registered implements the `System.IAsyncDisposable` interface.
  /// This property is used to determine if the service requires asynchronous disposal during its lifecycle.
  /// It ensures that services implementing `IAsyncDisposable` are properly disposed using an asynchronous
  /// pattern when they go out of scope in the dependency injection framework.
  public bool IsAsyncDisposable { get; init; }

}