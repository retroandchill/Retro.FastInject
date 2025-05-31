namespace Retro.FastInject.Core;

/// <summary>
/// Represents a service provider interface that retrieves services based on a specified key.
/// </summary>
/// <typeparam name="T">The type of the services provided by the keyed service provider.</typeparam>
public interface IKeyedServiceProvider<out T> {
  /// <summary>
  /// Retrieves a service of the specified type that is associated with the provided service key.
  /// </summary>
  /// <param name="serviceKey">The key used to identify and retrieve the service instance.</param>
  /// <returns>
  /// An instance of the service associated with the specified key, or <c>null</c> if no such service is found.
  /// </returns>
  T? GetKeyedService(string serviceKey);
  
}