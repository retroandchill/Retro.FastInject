namespace Retro.FastInject.Annotations;

/// <summary>
/// Specifies that the service has a singleton lifecycle in a dependency injection container.
/// </summary>
/// <remarks>
/// A service registered with the <c>Singleton</c> scope is instantiated only once and is shared
/// throughout the lifetime of the application. All requests for the service resolve to the
/// same, single instance.
/// </remarks>
public enum ServiceScope {
  /// <summary>
  /// A singleton service, meaning only one instance is created and shared throughout the lifetime of the application.
  /// </summary>
  Singleton,
  
  /// <summary>
  /// A scoped service, meaning an instance is shared within the scope of a request.
  /// </summary>
  Scoped,
  
  /// <summary>
  /// A transient service, meaning a new instance is created for each request for the service.
  /// </summary>
  Transient
}