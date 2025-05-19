using System.Threading;
using Microsoft.CodeAnalysis;
using Retro.FastInject.Annotations;
namespace Retro.FastInject.ServiceHierarchy;

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

  /// Represents the symbol associated with the service being registered.
  /// This property can hold information about a specific method, property,
  /// field, or null if no explicit association exists.
  /// It is particularly useful for scenarios involving factory methods,
  /// initialization logic, or additional metadata.
  /// The `AssociatedSymbol` helps resolve how the service instance should be
  /// created or accessed during dependency injection.
  public ISymbol? AssociatedSymbol { get; init; }

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
      if (Key is not null) {
        suffix = $"_{Key}";
      } else if (IndexForType > 0) {
        suffix = $"_{IndexForType}";
      }

      return $"_{Type.Name}{suffix}";
    }
  }

  /// <summary>
  /// Generates the initialization statement for the service registration, which includes
  /// resolving service dependencies and handling lifetime-specific logic.
  /// </summary>
  /// <param name="parameters">A string containing the parameters required for the service instantiation or method invocation.</param>
  /// <returns>
  /// A string representing the initializing statement for the service, accounting for its
  /// associated lifetime, type, and any relevant symbols.
  /// </returns>
  public string GetInitializingStatement(string parameters) {
    var basicBody = AssociatedSymbol switch {
        IMethodSymbol method => $"{GetMethodInvocation(method)}({parameters})",
        IPropertySymbol or IFieldSymbol => $"{AssociatedSymbol.Name}",
        _ => $"new {Type.ToDisplayString()}({parameters})"
    };

    if (Lifetime == ServiceScope.Transient || AssociatedSymbol is IPropertySymbol or IFieldSymbol) return basicBody;
    
    if (Type.IsValueType) {
      return $"InitializationUtils.EnsureValueInitialized(ref {FieldName}, this, () => {basicBody})";
    }
      
    var functionName = $"{typeof(LazyInitializer).FullName}.{nameof(LazyInitializer.EnsureInitialized)}";
    return $"{functionName}(ref {FieldName}, () => {basicBody})";

  }

  private string GetMethodInvocation(IMethodSymbol method) {
    if (method.IsStatic) {
      return method.ToDisplayString();
    }
    
    return Lifetime == ServiceScope.Singleton ? $"_root.{method.Name}" : method.Name;
  }
}