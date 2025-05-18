using Microsoft.CodeAnalysis;
using Retro.FastInject.Annotations;
namespace Retro.FastInject.ServiceHierarchy;

internal record struct ResolvedDependencyArguments(ITypeSymbol Type, ServiceScope Scope);