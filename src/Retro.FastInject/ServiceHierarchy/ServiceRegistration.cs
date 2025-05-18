using Microsoft.CodeAnalysis;
using Retro.FastInject.Annotations;
namespace Retro.FastInject.ServiceHierarchy;

public record ServiceRegistration {
  public required ITypeSymbol Type { get; init; }
  public string? Key { get; init; }
  public ServiceScope Lifetime { get; init; }
  public ITypeSymbol? ImplementationType { get; init; }
  public int IndexForType { get; init; }

  public string FieldName => $"_{Type.Name}{(Key is not null ? $"_{Key}" : IndexForType > 0 ? $"_{IndexForType}" : "")}";
}