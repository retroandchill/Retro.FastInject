using Microsoft.CodeAnalysis;
using Retro.FastInject.Annotations;

namespace Retro.FastInject.ServiceHierarchy;

public record ServiceDeclaration(ITypeSymbol Type, ServiceScope Lifetime, string? Key);