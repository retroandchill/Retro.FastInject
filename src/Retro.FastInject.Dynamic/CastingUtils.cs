using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
namespace Retro.FastInject.Dynamic;

/// <summary>
/// Provides utility methods for casting and creating immutable arrays.
/// </summary>
public static class CastingUtils {
  private static readonly MethodInfo EnumerableCast = typeof(Enumerable).GetMethod(nameof(Enumerable.Cast))!;
  private static readonly MethodInfo ImmutableArrayToImmutableArray = typeof(ImmutableArray).GetMethods()
      .Single(x => x is {
          Name: "ToImmutableArray",
          IsGenericMethodDefinition: true
      } && x.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>));

  /// <summary>
  /// Creates an immutable empty array for the specified element type.
  /// </summary>
  /// <param name="elementType">The type of the elements for which the empty array is created.</param>
  /// <returns>An immutable empty array of the specified element type.</returns>
  public static object? CreateEmptyArray(this Type elementType) {
    var emptyArray = Array.CreateInstance(elementType, 0);
    var toImmutableArrayMethod = ImmutableArrayToImmutableArray.MakeGenericMethod(elementType);
    return toImmutableArrayMethod.Invoke(null, [emptyArray]);
  }

  /// <summary>
  /// Casts an enumerable to a specified element type.
  /// </summary>
  /// <param name="enumerable">The enumerable to be cast to the specified element type.</param>
  /// <param name="elementType">The type to which the elements of the enumerable are cast.</param>
  /// <returns>A new enumerable with elements cast to the specified type.</returns>
  public static object CastEnumerable<T>(this IEnumerable<T> enumerable, Type elementType) {
    var castMethod = EnumerableCast.MakeGenericMethod(elementType);
    var castServices = castMethod.Invoke(null, [enumerable]);
    ArgumentNullException.ThrowIfNull(castServices);
    return castServices;
  }

  /// <summary>
  /// Casts an immutable array to a specified element type.
  /// </summary>
  /// <param name="reflectionObject">The immutable array object to be cast.</param>
  /// <param name="elementType">The type to which the elements of the immutable array are cast.</param>
  /// <returns>A new immutable array with elements cast to the specified type.</returns>
  public static object CastImmutableArray(this object reflectionObject, Type elementType) {
    var castMethod = ImmutableArrayToImmutableArray.MakeGenericMethod(elementType);
    var castServices = castMethod.Invoke(null, [reflectionObject]);
    ArgumentNullException.ThrowIfNull(castServices);
    return castServices;
  }
}