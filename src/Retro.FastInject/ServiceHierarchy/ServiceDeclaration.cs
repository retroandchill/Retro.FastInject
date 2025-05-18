using Microsoft.CodeAnalysis;
using Retro.FastInject.Annotations;

namespace Retro.FastInject.ServiceHierarchy;

public record struct ServiceDeclaration(ITypeSymbol Type, ServiceScope Lifetime, string? Key);