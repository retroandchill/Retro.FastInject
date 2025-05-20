using Microsoft.CodeAnalysis;
using Retro.FastInject.Annotations;

namespace Retro.FastInject.ServiceHierarchy;

/// <summary>
/// Represents resolved dependency arguments used in the dependency injection system.
/// </summary>
/// <remarks>
/// This struct encapsulates the type information and service scope associated with a resolved dependency.
/// It is primarily used to define how a specific dependency is resolved within a service hierarchy.
/// </remarks>
public record struct ResolvedDependencyArguments(ITypeSymbol Type, ServiceScope Scope);