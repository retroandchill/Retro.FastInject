using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Retro.FastInject.Annotations;
using Retro.FastInject.Utils;

namespace Retro.FastInject.ServiceHierarchy;

/// <summary>
/// Represents a service injection that is used to hold details about a service
/// registration and its associated parameters, used during dependency injection.
/// </summary>
public class ServiceInjection(ServiceRegistration registration, string parameters) {
  /// <summary>
  /// Gets the display string representing the type of the service associated with this injection.
  /// </summary>
  /// <remarks>
  /// This property retrieves the type of the service as a string, derived from the type
  /// specified in the associated <see cref="ServiceRegistration"/>. The type information is
  /// formatted to a display-friendly representation via the <c>ToDisplayString</c> method.
  /// </remarks>
  public string ServiceType { get; } = registration.Type.ToDisplayString();

  /// <summary>
  /// Gets the name of the service associated with this injection.
  /// </summary>
  /// <remarks>
  /// This property retrieves the unqualified name of the service type represented by
  /// the associated <see cref="ServiceRegistration"/>. The name is derived from the
  /// <c>Name</c> property of the <see cref="ITypeSymbol"/> specified during the registration
  /// process.
  /// </remarks>
  public string ServiceName { get; } = registration.Type.GetSanitizedTypeName();

  /// <summary>
  /// Gets the name of the field used for storing the service during dependency injection.
  /// </summary>
  /// <remarks>
  /// This property retrieves the field name associated with the service from the
  /// corresponding <see cref="ServiceRegistration"/>. The field name is dynamically generated
  /// based on the type, key, or type index provided during registration.
  /// </remarks>
  public string FieldName { get; } = registration.FieldName;

  /// <summary>
  /// Indicates whether the service is derived from another service's implementation type.
  /// </summary>
  /// <remarks>
  /// This property returns <c>true</c> if the service registration specifies an
  /// implementation type. In such cases, the service is considered to be based on or
  /// wrapping another service. Otherwise, it returns <c>false</c>, suggesting the service
  /// is defined directly without referencing another implementation type.
  /// </remarks>
  public bool FromOtherService { get; } = registration.ImplementationType is not null;

  /// <summary>
  /// Gets the display string representing the type of an alternative service implementation, if one is specified.
  /// </summary>
  /// <remarks>
  /// This property retrieves the type of the implementation when the service is resolved from another service.
  /// If the service has a specified <c>ImplementationType</c>, the type is formatted to a display-friendly
  /// representation using the <c>ToDisplayString</c> method. Otherwise, it returns <c>null</c>.
  /// </remarks>
  public string? OtherType { get; } = registration.ImplementationType?.ToDisplayString();

  /// <summary>
  /// Indicates whether the service is registered with a singleton lifecycle.
  /// </summary>
  /// <remarks>
  /// This property evaluates to <c>true</c> if the service registration specifies a
  /// singleton scope, and there is no specified implementation type.
  /// A singleton service is instantiated once and shared for the duration of the application lifecycle.
  /// </remarks>
  public bool IsSingleton { get; } =
    registration.ImplementationType is null && registration.CollectedServices is null 
                                            && registration.Lifetime == ServiceScope.Singleton;

  /// <summary>
  /// Gets a value indicating whether the service has a scoped lifecycle in the dependency injection container.
  /// </summary>
  /// <remarks>
  /// This property evaluates to <c>true</c> if the service's <see cref="ServiceScope"/> is defined as <c>Scoped</c> in its
  /// registration and no specific implementation type is associated with it. Scoped services are instantiated once per
  /// request and are shared within the scope of that request.
  /// </remarks>
  public bool IsScoped { get; } =
    registration.ImplementationType is null && registration.CollectedServices is null 
                                            && registration.Lifetime == ServiceScope.Scoped;

  /// <summary>
  /// Indicates whether the service associated with this injection has a transient lifecycle.
  /// </summary>
  /// <remarks>
  /// This property determines if the service's lifetime is configured as <see cref="ServiceScope.Transient"/>
  /// in the associated <see cref="ServiceRegistration"/>. A transient service is created each time it is requested,
  /// providing a new instance on every injection.
  /// </remarks>
  public bool IsTransient { get; } =
    registration.ImplementationType is null && registration.CollectedServices is null 
                                            && registration.Lifetime == ServiceScope.Transient;

  /// <summary>
  /// Indicates whether this service represents a collection of other services.
  /// </summary>
  /// <remarks>
  /// This property evaluates to true if the service is a collection containing multiple
  /// service registrations, represented by the <c>CollectedServices</c> property. It is determined
  /// by checking that the <c>ImplementationType</c> is null and that <c>CollectedServices</c> is not null.
  /// </remarks>
  public bool IsCollection { get; } = registration.ImplementationType is null && registration.CollectedServices is not null;

  /// <summary>
  /// Gets a collection of services associated with this registration if the service
  /// represents a collection of multiple services.
  /// </summary>
  /// <remarks>
  /// This property provides a list of collected services derived from the associated
  /// <see cref="ServiceRegistration.CollectedServices"/>. Each service in the collection
  /// is encapsulated within a <see cref="CollectedService"/> instance, allowing access
  /// to additional details such as service type, index, and primary/last service indicators.
  /// If no collected services are associated, this property returns an empty list.
  /// </remarks>
  public List<CollectedService> CollectedServices { get; } = registration.CollectedServices
      ?.Select((x, i) => new CollectedService(x, i == registration.CollectedServices.Count))
      .ToList() ?? [];

  /// <summary>
  /// Gets the unique identifier (key) associated with the service registration.
  /// </summary>
  /// <remarks>
  /// This property retrieves the key of the service from the associated <see cref="ServiceRegistration"/>.
  /// The key is used to distinguish between multiple registrations of the same service type, allowing
  /// more granular control over dependency injection scenarios.
  /// </remarks>
  public string? Key { get; } = registration.Key;

  /// <summary>
  /// Gets the initializing statement for the service, representing the code required
  /// to instantiate or retrieve the service during dependency injection.
  /// </summary>
  /// <remarks>
  /// This property generates a string containing the initialization logic for the service.
  /// If the service has a specific associated symbol (e.g., a method, property, or field),
  /// the output is dynamically determined based on that symbol.
  /// For transient services or services instantiated via method/property/field,
  /// the statement generates the direct initialization logic.
  /// For singleton or scoped services, it wraps initialization logic using
  /// <see cref="System.Threading.LazyInitializer.EnsureInitialized"/> to ensure thread-safe
  /// initialization and caching of the service instance.
  /// </remarks>
  public string InitializingStatement { get; } = registration.GetInitializingStatement(parameters);

  /// <summary>
  /// Gets the initialization statement for scoped or transient service lifetime, specifically formatted
  /// to handle these lifetimes differently during service injection.
  /// </summary>
  /// <remarks>
  /// This property retrieves the initialization logic for services registered with a scoped or transient
  /// lifetime in dependency injection. This differs from the default initialization logic by including
  /// additional handling for scoped and transient lifetimes when creating instances.
  /// </remarks>
  public string ScopedTransientInitializer { get; } = registration.GetInitializingStatement(parameters, true);
  
  /// <summary>
  /// Indicates whether the service associated with this injection is disposable.
  /// </summary>
  /// <remarks>
  /// This property determines if the injected service implements <see cref="System.IDisposable"/>.
  /// The value is derived from the associated <see cref="ServiceRegistration"/> and reflects the
  /// disposable nature of the service's lifecycle, which may require explicit resource cleanup.
  /// </remarks>
  public bool IsDisposable { get; } = registration.IsDisposable;

  /// <summary>
  /// Gets a value indicating whether the service associated with this injection
  /// is capable of asynchronous disposal.
  /// </summary>
  /// <remarks>
  /// This property determines if the service implements the <c>IAsyncDisposable</c> interface,
  /// allowing it to be disposed of asynchronously. The information is derived from the
  /// <see cref="ServiceRegistration"/> associated with this injection.
  /// </remarks>
  public bool IsAsyncDisposable { get; } = registration.IsAsyncDisposable;

  /// <summary>
  /// Gets a value that indicates whether the service is both disposable and asynchronously disposable.
  /// </summary>
  /// <remarks>
  /// This property evaluates to <c>true</c> if the service associated with the current
  /// registration implements both <c>IDisposable</c> and <c>IAsyncDisposable</c> interfaces.
  /// It combines the values of the <see cref="ServiceRegistration.IsDisposable"/> and
  /// <see cref="ServiceRegistration.IsAsyncDisposable"/> properties to determine if the service
  /// requires proper cleanup through both synchronous and asynchronous disposal mechanisms.
  /// </remarks>
  public bool DoubleDisposable { get; } = registration.IsDisposable && registration.IsAsyncDisposable;

  /// <summary>
  /// Gets or sets the index of this service injection within its associated collection or system.
  /// </summary>
  /// <remarks>
  /// This property represents the positional identifier of the service injection in the context
  /// of the dependency injection framework. It is used for tracking or ordering service registrations
  /// where applicable.
  /// </remarks>
  public int Index { get; } = registration.IndexForType;

  /// <summary>
  /// Determines whether the current service injection is the primary service for its type.
  /// </summary>
  /// <remarks>
  /// This property evaluates whether the current service instance is the primary one
  /// among all registered services of the same type, based on its index. A service is
  /// considered primary if its <see cref="ServiceRegistration.IndexForType"/> is zero.
  /// </remarks>
  public bool IsPrimary => Index == 0;
}