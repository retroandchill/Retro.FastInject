namespace Retro.FastInject.ServiceHierarchy;

/// <summary>
/// Represents a service collected during registration processing in a dependency injection setup.
/// </summary>
/// <remarks>
/// This class encapsulates the data of a specific service registration, including its type,
/// index, and its status as primary or the last registration in a sequence.
/// </remarks>
public class CollectedService(ServiceRegistration registration, bool isLast) {
  /// <summary>
  /// Gets the fully qualified type name of the service associated with this registration.
  /// </summary>
  /// <remarks>
  /// This property provides the type name of the service as a string, formatted for display.
  /// It is derived from the service type defined during the registration.
  /// </remarks>
  public string ServiceType { get; } = registration.Type.ToDisplayString();

  /// <summary>
  /// Gets the name of the service associated with this registration.
  /// </summary>
  /// <remarks>
  /// This property represents the simple name of the service type.
  /// It is derived from the type defined during the registration process and is typically used
  /// for identification or display purposes within the dependency injection system.
  /// </remarks>
  public string ServiceName { get; } = registration.Type.Name;

  /// <summary>
  /// Gets the index of the service in the sequence of registrations for its type.
  /// </summary>
  /// <remarks>
  /// The index represents the position of this specific service registration among
  /// other registrations of the same service type. An index of 0 indicates that this
  /// is the primary registration for the service.
  /// </remarks>
  public int Index { get; } = registration.IndexForType;

  /// <summary>
  /// Indicates whether the service registration is the primary registration for its type.
  /// </summary>
  /// <remarks>
  /// This property evaluates to true if the service holds the first or primary position among
  /// registrations for its specific type. The primary status is determined based on the
  /// registration's index being zero.
  /// </remarks>
  public bool IsPrimary => Index == 0;

  /// <summary>
  /// Indicates whether this service is the last one registered in the sequence for its type.
  /// </summary>
  /// <remarks>
  /// This property is used to determine if the current service registration is the final one
  /// in a set of collected services of the same type. It provides useful context for scenarios
  /// where the sequence or order of registrations matters during dependency resolution.
  /// </remarks>
  public bool IsLast { get; } = isLast;

}