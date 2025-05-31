
namespace Retro.FastInject.Core.Exceptions;

/// <summary>
/// Exception type that is triggered due to a failure in resolving server dependencies.
/// </summary>
/// <param name="message">The message for the exception</param>
/// <param name="cause">The underlying cause of the exception</param>
public class DependencyResolutionException(string? message = null, Exception? cause = null) : Exception(message, cause);