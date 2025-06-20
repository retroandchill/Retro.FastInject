using Microsoft.CodeAnalysis;
using Retro.FastInject.Annotations;
using Retro.SourceGeneratorUtilities.Utilities.Attributes;

namespace Retro.FastInject.Model.Attributes;

/// <summary>
/// Represents an overview of a dependency that is registered with a singleton lifetime scope.
/// This class associates a type symbol with the <see cref="ServiceScope.Singleton"/> scope
/// and serves as metadata for registering services as singletons in a dependency injection system.
/// </summary>
/// <remarks>
/// It leverages the <see cref="SingletonAttribute"/> to indicate that the associated type
/// should be treated as a singleton when registered with a dependency injection container.
/// It extends the <see cref="DependencyOverview"/> class with the specific scope of a singleton.
/// </remarks>
/// <param name="Type">
/// The symbol representation of the type being registered as a singleton.
/// </param>
[AttributeInfoType<SingletonAttribute>]
public record SingletonOverview(ITypeSymbol Type) : DependencyOverview(Type, ServiceScope.Singleton);

/// <summary>
/// Represents an overview of a dependency that is registered with a singleton lifetime scope,
/// specifically for generic service types with one type parameter.
/// This class provides metadata for registering one-parameter generic services as singletons
/// in a dependency injection system.
/// </summary>
/// <remarks>
/// It extends <see cref="SingletonOverview"/> to specialize the processing of generic
/// type definitions for singleton registrations. The associated <see cref="SingletonAttribute{TService}"/>
/// indicates the generic type to be registered as a singleton.
/// </remarks>
/// <param name="Type">
/// The symbol representation of the one-parameter generic type being registered as a singleton.
/// </param>
[AttributeInfoType(typeof(SingletonAttribute<>))]
public record SingletonOneParamOverview(ITypeSymbol Type) : SingletonOverview(Type);