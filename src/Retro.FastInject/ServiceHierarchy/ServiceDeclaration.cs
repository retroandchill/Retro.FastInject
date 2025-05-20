using Dunet;
using Microsoft.CodeAnalysis;
using Retro.FastInject.Annotations;

namespace Retro.FastInject.ServiceHierarchy;

/// <summary>
/// Represents the declaration of a service within the dependency injection hierarchy.
/// This encapsulates metadata associated with a specific service type, its lifetime scope,
/// an optional key for uniquely identifying instances, and potentially an associated symbol.
/// </summary>
public record ServiceDeclaration(
    ITypeSymbol Type,
    ServiceScope Lifetime,
    string? Key,
    ISymbol? AssociatedSymbol = null);