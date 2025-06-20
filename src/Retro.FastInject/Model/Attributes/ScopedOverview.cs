using Microsoft.CodeAnalysis;
using Retro.FastInject.Annotations;
using Retro.SourceGeneratorUtilities.Utilities.Attributes;
namespace Retro.FastInject.Model.Attributes;

/// <summary>
/// Represents an overview model specifically for scoped services in dependency injection frameworks.
/// </summary>
/// <remarks>
/// This record is part of the DependencyOverview hierarchy and provides metadata for services that are configured
/// with a scoped lifetime. Scoped services are created once per scope and reused within the same scope.
/// </remarks>
/// <param name="Type">
/// The type symbol representing the service type declared in the dependency container.
/// This indicates the interface or class registered with a scoped lifetime.
/// </param>
[AttributeInfoType<ScopedAttribute>]
public record ScopedOverview(ITypeSymbol Type) : DependencyOverview(Type, ServiceScope.Scoped);

/// <summary>
/// Represents an overview model specifically tailored for scoped services with a single generic parameter in dependency injection frameworks.
/// </summary>
/// <remarks>
/// This record is a specialization of <see cref="SingletonOverview"/> and provides metadata for services configured with a scoped lifetime.
/// It is associated with the generic version of the <c>ScopedAttribute</c>, which defines services by their generic type parameter.
/// Scoped services are created once per scope and reused within the same scope.
/// </remarks>
/// <param name="Type">
/// The type symbol representing the service type declared in the dependency container.
/// This indicates the generic service type registered with a scoped lifetime.
/// </param>
[AttributeInfoType(typeof(ScopedAttribute<>))]
public record ScopedOneParamOverview(ITypeSymbol Type) : ScopedOverview(Type);