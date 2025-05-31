using System;
using Microsoft.Extensions.DependencyInjection;
using Retro.ReadOnlyParams.Annotations;
namespace Retro.FastInject.Dynamic;

/// <summary>
/// Represents a service retriever that dynamically resolves services using a keyed or non-keyed approach
/// from a provided <see cref="IServiceProvider"/>.
/// </summary>
/// <typeparam name="T">
/// The type of the service to retrieve. This type must be non-nullable.
/// </typeparam>
public sealed class ServiceRetriever<T>([ReadOnly] IServiceProvider serviceProvider, 
                                        [ReadOnly] object? serviceKey) where T : notnull {

  /// <summary>
  /// Retrieves an instance of the service of type <typeparamref name="T"/>.
  /// If a service key is provided, it attempts to resolve the service using that key.
  /// Otherwise, it resolves the service without a key.
  /// </summary>
  /// <returns>
  /// The resolved service instance of type <typeparamref name="T"/>.
  /// </returns>
  public T GetService() {
    return serviceKey is not null ? serviceProvider.GetRequiredKeyedService<T>(serviceKey) : serviceProvider.GetRequiredService<T>();
  }
  
}