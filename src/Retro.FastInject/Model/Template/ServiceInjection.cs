using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Retro.FastInject.Annotations;
using Retro.FastInject.Generation;
using Retro.FastInject.Model.Manifest;
using Retro.FastInject.Utils;

namespace Retro.FastInject.Model.Template;

/// <summary>
/// Represents a service injection that is used to hold details about a service
/// registration and its associated parameters, used during dependency injection.
/// </summary>
public record ServiceInjection {

  /// <summary>
  /// Gets the display string representing the type of the service associated with this injection.
  /// </summary>
  /// <remarks>
  /// This property retrieves the type of the service as a string, derived from the type
  /// specified in the associated <see cref="ServiceRegistration"/>. The type information is
  /// formatted to a display-friendly representation via the <c>ToDisplayString</c> method.
  /// </remarks>
  public required string ServiceType { get; init; }

  /// <summary>
  /// Gets the name of the service associated with this injection.
  /// </summary>
  /// <remarks>
  /// This property retrieves the unqualified name of the service type represented by
  /// the associated <see cref="ServiceRegistration"/>. The name is derived from the
  /// <c>Name</c> property of the <see cref="ITypeSymbol"/> specified during the registration
  /// process.
  /// </remarks>
  public required string ServiceName { get; init; }

  /// <summary>
  /// Gets the name of the field used for storing the service during dependency injection.
  /// </summary>
  /// <remarks>
  /// This property retrieves the field name associated with the service from the
  /// corresponding <see cref="ServiceRegistration"/>. The field name is dynamically generated
  /// based on the type, key, or type index provided during registration.
  /// </remarks>
  public required string FieldName { get; init; }

  /// <summary>
  /// Indicates whether the service is derived from another service's implementation type.
  /// </summary>
  /// <remarks>
  /// This property returns <c>true</c> if the service registration specifies an
  /// implementation type. In such cases, the service is considered to be based on or
  /// wrapping another service. Otherwise, it returns <c>false</c>, suggesting the service
  /// is defined directly without referencing another implementation type.
  /// </remarks>
  public bool FromOtherService => OtherType is not null;

  /// <summary>
  /// Gets the display string representing the type of an alternative service implementation, if one is specified.
  /// </summary>
  /// <remarks>
  /// This property retrieves the type of the implementation when the service is resolved from another service.
  /// If the service has a specified <c>ImplementationType</c>, the type is formatted to a display-friendly
  /// representation using the <c>ToDisplayString</c> method. Otherwise, it returns <c>null</c>.
  /// </remarks>
  public required string? OtherType { get; init; }

  /// <summary>
  /// Indicates whether the service is registered with a singleton lifecycle.
  /// </summary>
  /// <remarks>
  /// This property evaluates to <c>true</c> if the service registration specifies a
  /// singleton scope, and there is no specified implementation type.
  /// A singleton service is instantiated once and shared for the duration of the application lifecycle.
  /// </remarks>
  public required bool IsSingleton { get; init; }

  /// <summary>
  /// Gets a value indicating whether the service has a scoped lifecycle in the dependency injection container.
  /// </summary>
  /// <remarks>
  /// This property evaluates to <c>true</c> if the service's <see cref="ServiceScope"/> is defined as <c>Scoped</c> in its
  /// registration and no specific implementation type is associated with it. Scoped services are instantiated once per
  /// request and are shared within the scope of that request.
  /// </remarks>
  public required bool IsScoped { get; init; }

  /// <summary>
  /// Indicates whether the service associated with this injection has a transient lifecycle.
  /// </summary>
  /// <remarks>
  /// This property determines if the service's lifetime is configured as <see cref="ServiceScope.Transient"/>
  /// in the associated <see cref="ServiceRegistration"/>. A transient service is created each time it is requested,
  /// providing a new instance on every injection.
  /// </remarks>
  public required bool IsTransient { get; init; }

  /// <summary>
  /// Indicates whether this service represents a collection of other services.
  /// </summary>
  /// <remarks>
  /// This property evaluates to true if the service is a collection containing multiple
  /// service registrations, represented by the <c>CollectedServices</c> property. It is determined
  /// by checking that the <c>ImplementationType</c> is null and that <c>CollectedServices</c> is not null.
  /// </remarks>
  public required bool IsCollection { get; init; }

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
  public required List<CollectedService> CollectedServices { get; init; }

  /// <summary>
  /// Gets the unique identifier (key) associated with the service registration.
  /// </summary>
  /// <remarks>
  /// This property retrieves the key of the service from the associated <see cref="ServiceRegistration"/>.
  /// The key is used to distinguish between multiple registrations of the same service type, allowing
  /// more granular control over dependency injection scenarios.
  /// </remarks>
  public required string? Key { get; init; }
  
  /// <summary>
  /// Indicates whether the service associated with this injection is disposable.
  /// </summary>
  /// <remarks>
  /// This property determines if the injected service implements <see cref="System.IDisposable"/>.
  /// The value is derived from the associated <see cref="ServiceRegistration"/> and reflects the
  /// disposable nature of the service's lifecycle, which may require explicit resource cleanup.
  /// </remarks>
  public required bool IsDisposable { get; init; }

  /// <summary>
  /// Gets a value indicating whether the service associated with this injection
  /// is capable of asynchronous disposal.
  /// </summary>
  /// <remarks>
  /// This property determines if the service implements the <c>IAsyncDisposable</c> interface,
  /// allowing it to be disposed of asynchronously. The information is derived from the
  /// <see cref="ServiceRegistration"/> associated with this injection.
  /// </remarks>
  public required bool IsAsyncDisposable { get; init; }

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
  public bool DoubleDisposable => IsDisposable && IsAsyncDisposable;

  /// <summary>
  /// Gets or sets the index of this service injection within its associated collection or system.
  /// </summary>
  /// <remarks>
  /// This property represents the positional identifier of the service injection in the context
  /// of the dependency injection framework. It is used for tracking or ordering service registrations
  /// where applicable.
  /// </remarks>
  public required int Index { get; init; }

  /// <summary>
  /// Determines whether the current service injection is the primary service for its type.
  /// </summary>
  /// <remarks>
  /// This property evaluates whether the current service instance is the primary one
  /// among all registered services of the same type, based on its index. A service is
  /// considered primary if its <see cref="ServiceRegistration.IndexForType"/> is zero.
  /// </remarks>
  public bool IsPrimary => Index == 0;

  /// <summary>
  /// Gets or sets a value indicating whether the service is a value type.
  /// </summary>
  public required bool IsValueType { get; init; }

  /// <summary>
  /// Gets or sets a value indicating whether the service has an associated method.
  /// </summary>
  public required bool HasAssociatedMethod { get; init; }

  /// <summary>
  /// Gets or sets a value indicating whether the service has an associated property or field.
  /// </summary>
  public required bool HasAssociatedPropertyOrField { get; init; }

  /// <summary>
  /// Gets or sets a value indicating whether the associated method is static.
  /// </summary>
  public required bool IsMethodStatic { get; init; }

  /// <summary>
  /// Gets or sets the name of the associated symbol.
  /// </summary>
  public required string? AssociatedSymbolName { get; init; }

  /// <summary>
  /// Gets or sets the full name of the associated method, including its containing type.
  /// Used for static method invocation.
  /// </summary>
  public required string? AssociatedMethodFullName { get; init; }
  
  
  /// <summary>
  /// Gets the list of parameter resolutions required for this service.
  /// </summary>
  /// <remarks>
  /// This property contains information about how each parameter required by the service
  /// should be resolved during dependency injection.
  /// </remarks>
  public required List<ParameterInjection> Parameters { get; init; }

  /// <summary>
  /// Defines the display format used to generate a string representation of method names
  /// for associated symbols within a service injection context.
  /// </summary>
  /// <remarks>
  /// This format specifies the options applied when converting method symbols into strings,
  /// such as including or excluding member-specific details. It is primarily used to format
  /// method names for display or serialization purposes within dependency injection configurations.
  /// </remarks>
  private static readonly SymbolDisplayFormat MethodNameFormat = new(
      memberOptions: SymbolDisplayMemberOptions.None,
      genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
      typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

  /// <summary>
  /// Creates a new <see cref="ServiceInjection"/> instance based on the specified
  /// <see cref="ServiceRegistration"/> and a collection of parameter resolutions.
  /// </summary>
  /// <param name="registration">
  /// The service registration that provides details such as the type, lifetime, and other metadata
  /// needed for creating the service injection.
  /// </param>
  /// <param name="parameters">
  /// A collection of parameter resolutions that define the dependencies the service injector
  /// needs to resolve.
  /// </param>
  /// <returns>
  /// A <see cref="ServiceInjection"/> instance containing metadata and configurations
  /// derived from the given service registration and parameter resolutions.
  /// </returns>
  public static ServiceInjection FromResolution(ServiceRegistration registration,
                                                IReadOnlyCollection<ParameterResolution> parameters) {
    var isStandardService = registration.ImplementationType is null && registration.CollectedServices is null;
    var associatedMethod = registration.AssociatedSymbol is IMethodSymbol method ? method.ValidateFactoryMethod(registration.Type) : null;
    
    return new ServiceInjection {
        ServiceType = registration.Type.ToDisplayString(),
        ServiceName = registration.Type.GetSanitizedTypeName(),
        FieldName = registration.FieldName,
        OtherType = registration.ImplementationType?.ToDisplayString(),
        IsSingleton = isStandardService && registration.Lifetime == ServiceScope.Singleton,
        IsScoped = isStandardService && registration.Lifetime == ServiceScope.Scoped,
        IsTransient = isStandardService && registration.Lifetime == ServiceScope.Transient,
        IsCollection = registration.ImplementationType is null && registration.CollectedServices is not null,
        CollectedServices = registration.CollectedServices
            ?.Select((x, i) => 
                         new CollectedService(x, i == registration.CollectedServices.Count))
            .ToList() ?? [],
        Key = registration.Key,
        IsDisposable = registration.IsDisposable,
        IsAsyncDisposable = registration.IsAsyncDisposable,
        Index = registration.IndexForType,
        IsValueType = registration.ResolvedType.IsValueType,
        HasAssociatedMethod = associatedMethod is not null,
        HasAssociatedPropertyOrField = registration.AssociatedSymbol is IFieldSymbol or IPropertySymbol,
        IsMethodStatic = associatedMethod is not null && associatedMethod.IsStatic,
        AssociatedSymbolName = registration.AssociatedSymbol?.Name,
        AssociatedMethodFullName = associatedMethod?.ToDisplayString(MethodNameFormat),
        Parameters = parameters
            .Select((p, i) => ParameterInjection.FromResolution(p, i == parameters.Count - 1))
            .ToList()
    };
  }
}