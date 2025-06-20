using Microsoft.CodeAnalysis;
using Retro.FastInject.Annotations;
using Retro.SourceGeneratorUtilities.Utilities.Attributes;
namespace Retro.FastInject.Model.Attributes;

/// <summary>
/// Represents an overview of a transient dependency within the dependency injection system.
/// </summary>
/// <remarks>
/// This class provides metadata about types registered with transient scope,
/// specifically those attributed with a <see cref="TransientAttribute"/>.
/// </remarks>
/// <param name="Type">
/// The type symbol representing the service type associated with the transient dependency.
/// </param>
[AttributeInfoType<TransientAttribute>]
public record TransientOverview(ITypeSymbol Type) : DependencyOverview(Type, ServiceScope.Transient);

/// <summary>
/// Represents an overview of a transient dependency with one generic parameter within the dependency injection system.
/// </summary>
/// <remarks>
/// This record provides metadata about types registered with transient scope,
/// specifically those attributed with a generic <see cref="TransientAttribute{TService}"/>.
/// </remarks>
/// <param name="Type">
/// The type symbol representing the service type associated with the generic transient dependency.
/// </param>
[AttributeInfoType(typeof(TransientAttribute<>))]
public record TransientOneParamOverview(ITypeSymbol Type) : TransientOverview(Type);