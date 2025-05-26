using System.Collections.Immutable;
namespace Retro.FastInject.Core;

/// <summary>
/// Utility class for service-related operations within a service provider.
/// </summary>
public static class ServiceUtils {

  /// <summary>
  /// Retrieves a service of type <typeparamref name="T"/> from the specified <see cref="IServiceProvider"/>.
  /// If the service is not available, returns the provided default value.
  /// </summary>
  /// <typeparam name="T">The type of service to retrieve from the service provider.</typeparam>
  /// <param name="serviceProvider">The <see cref="IServiceProvider"/> to retrieve the service from.</param>
  /// <param name="defaultValue">The default value to return if the service is not found in the service provider.</param>
  /// <returns>The service of type <typeparamref name="T"/> if it exists; otherwise, the specified default value.</returns>
  public static T GetService<T>(this IServiceProvider serviceProvider, T defaultValue) {
    var service = serviceProvider.GetService(typeof(T));
    if (service is not null) {
      return (T) service;
    }

    return defaultValue;
  }
  
}