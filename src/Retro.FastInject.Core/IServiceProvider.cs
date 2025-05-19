namespace Retro.FastInject.Core;

/// <summary>
/// Represents a service provider interface for retrieving service instances.
/// This interface defines a contract to resolve and retrieve instances of a specific service type.
/// </summary>
/// <typeparam name="T">
/// The type of service that this provider will supply.
/// </typeparam>
public interface IServiceProvider<out T> {
  /// <summary>
  /// Retrieves a service of the specified type from the service provider.
  /// </summary>
  /// <returns>
  /// The service object of type T if found; otherwise, null.
  /// </returns>
  T GetService();
  
}