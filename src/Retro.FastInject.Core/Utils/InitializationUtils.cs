
namespace Retro.FastInject.Core;

/// <summary>
/// Provides utility methods for initialization processes.
/// </summary>
public static class InitializationUtils {
  /// <summary>
  /// Ensures that the given value is initialized. If it is not initialized,
  /// the method initializes it using the provided initializer function within a
  /// thread-safe lock using the specified owner object.
  /// </summary>
  /// <typeparam name="T">The type of the value to initialize. Must be a struct type.</typeparam>
  /// <param name="value">A reference to the value to be checked and possibly initialized.</param>
  /// <param name="owner">An object used as the locking mechanism to ensure thread-safety.</param>
  /// <param name="initializer">A function that provides the initialization logic for the value.</param>
  /// <returns>Returns the initialized value of type <typeparamref name="T"/>.</returns>
  public static T EnsureValueInitialized<T>(ref T? value, object owner, Func<T> initializer) where T : struct {
    if (value.HasValue) return value.Value;
    
    lock (owner) {
      value ??= initializer();
    }

    return value.Value;
  }
}