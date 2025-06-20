using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Retro.FastInject.Utils;

/// <summary>
/// Provides extension methods for working with types represented as <see cref="ITypeSymbol"/>.
/// </summary>
public static class TypeExtensions {
  /// <summary>
  /// Determines whether the specified interface type is valid for use as a type argument in the current context.
  /// </summary>
  /// <param name="interfaceType">The interface type represented as an <see cref="ITypeSymbol"/> to validate.</param>
  /// <returns>
  /// True if the interface type is valid for use as a type argument; otherwise, false.
  /// </returns>
  public static bool IsValidForTypeArgument(this ITypeSymbol interfaceType) {
    if (interfaceType is not INamedTypeSymbol namedType) return true;

    if (IsProblematicInterface(namedType)) {
      return false;
    }

    // Check if the interface has any static members
    var staticMembers = namedType.GetMembers().Where(m => m.IsStatic).ToList();
    if (!staticMembers.Any()) return true;

    foreach (var member in staticMembers) {
      // Check for unimplemented static members
      if (!HasSpecificImplementation(member)) return false;

      // Check for ambiguous implementations in generic interfaces
      if (namedType.IsGenericType && MightHaveAmbiguousImplementation(member)) {
        return false;
      }
    }

    return true;
  }

  private static bool MightHaveAmbiguousImplementation(ISymbol member) {
    switch (member) {
      case IMethodSymbol methodSymbol:
        // Check if the method uses type parameters in its signature
        return HasTypeParametersInSignature(methodSymbol);

      case IPropertySymbol propertySymbol:
        // Check if the property type involves type parameters
        return HasTypeParametersInProperty(propertySymbol);

      default:
        return false;
    }
  }

  private static bool HasTypeParametersInSignature(IMethodSymbol method) {
    // Check return type
    return ContainsTypeParameters(method.ReturnType) ||
           // Check parameters
           method.Parameters.Any(p => ContainsTypeParameters(p.Type));
  }

  private static bool HasTypeParametersInProperty(IPropertySymbol property) {
    return ContainsTypeParameters(property.Type);
  }

  private static bool ContainsTypeParameters(ITypeSymbol type) {
    return type switch {
        ITypeParameterSymbol => true,
        INamedTypeSymbol namedType => namedType.TypeArguments.Any(ContainsTypeParameters),
        _ => false
    };
  }

  private static bool HasSpecificImplementation(ISymbol member) {
    // For properties
    if (member is IPropertySymbol propertySymbol) {
      return !propertySymbol.IsAbstract;
    }

    // For methods
    if (member is IMethodSymbol methodSymbol) {
      return !methodSymbol.IsAbstract;
    }

    return true;
  }


  private static bool IsProblematicInterface(INamedTypeSymbol type) {
    var fullName = type.ToDisplayString();

    // System.Numerics interfaces
    var numericInterfaces = new[] {
        "System.Numerics.INumber<",
        "System.Numerics.IBinaryFloatingPointIeee754<",
        "System.Numerics.IBinaryNumber<",
        "System.Numerics.ISignedNumber<",
        "System.Numerics.IFloatingPoint<",
        "System.Numerics.IFloatingPointIeee754<",
        "System.Numerics.IExponentalFunctions<",
        "System.Numerics.IPowerFunctions<",
        "System.Numerics.ILogarithmicFunctions<",
        "System.Numerics.ITrigonometricFunctions<",
        "System.Numerics.IHyperbolicFunctions<",
        "System.Numerics.IRootFunctions<",
        "System.Numerics.IModulusOperators<",
        "System.Numerics.IUnaryPlusOperators<",
        "System.Numerics.IUnaryNegationOperators<",
        "System.Numerics.IIncrementOperators<",
        "System.Numerics.IDecrementOperators<"
    };

    // Basic BCL interfaces
    var bclInterfaces = new[] {
        "System.ISpanFormattable",
        "System.IFormattable",
        "System.IUtf8SpanFormattable",
        "System.IComparable",
        "System.IComparable<",
        "System.IConvertible",
        "System.IEquatable<",
        "System.ISpanParsable<",
        "System.IParsable<",
        "System.IUtf8SpanParsable<",

        // Collections and related
        "System.Collections.Generic.IEnumerable<",
        "System.Collections.Generic.IAsyncEnumerable<",
        "System.Collections.Generic.ICollection<",
        "System.Collections.Generic.IList<",
        "System.Collections.Generic.ISet<",
        "System.Collections.Generic.IDictionary<",
        "System.Collections.Generic.IReadOnlyCollection<",
        "System.Collections.Generic.IReadOnlyList<",
        "System.Collections.Generic.IReadOnlySet<",
        "System.Collections.Generic.IReadOnlyDictionary<",

        // Additional common interfaces
        "System.IObservable<",
        "System.IObserver<",
        "System.IProgress<",
        "System.IDisposable",
        "System.IAsyncDisposable",
        "System.ICloneable",

        // Comparison interfaces
        "System.IComparer<",
        "System.Collections.Generic.IEqualityComparer<",

        // Additional numeric interfaces
        "System.IAdditionOperators<",
        "System.ISubtractionOperators<",
        "System.IMultiplyOperators<",
        "System.IDivisionOperators<",
        "System.IAdditionOperators<",
        "System.IAdditiveIdentity<",
        "System.IMultiplicativeIdentity<",

        // Pattern interfaces
        "System.IAsyncPattern",
        "System.IValueTaskSource<",
        "System.IValueTaskSource"
    };

    return numericInterfaces.Concat(bclInterfaces)
        .Any(pi => fullName.StartsWith(pi, StringComparison.Ordinal));
  }

  /// <summary>
  /// Determines whether the specified <see cref="ITypeSymbol"/> represents a nullable type
  /// and provides information about its underlying type.
  /// </summary>
  /// <param name="type">The type symbol to check for nullability.</param>
  /// <returns>
  /// A <see cref="NullableData"/> instance containing information about the nullability
  /// of the type and its underlying type, if applicable.
  /// </returns>
  public static NullableData CheckIfNullable(this ITypeSymbol type) {
    if (type is INamedTypeSymbol { ConstructedFrom.SpecialType: SpecialType.System_Nullable_T } namedType) {
      return new NullableData(true, namedType.TypeArguments[0]);
    }

    return type.NullableAnnotation == NullableAnnotation.Annotated ? new NullableData(true, type.WithNullableAnnotation(NullableAnnotation.None)) : new NullableData(false, type);
  }

  /// <summary>
  /// Constructs a generic type by instantiating it with the specified <paramref name="elementTypes"/>.
  /// </summary>
  /// <param name="elementTypes">The types to be used as the generic type argument for the constructed type.</param>
  /// <param name="compilation">The current Roslyn <see cref="Compilation"/> instance used to access type metadata.</param>
  /// <param name="type">The generic type definition to be instantiated, provided as a <see cref="Type"/>.</param>
  /// <returns>
  /// An <see cref="INamedTypeSymbol"/> representing the constructed generic type.
  /// </returns>
  /// <exception cref="InvalidOperationException">
  /// Thrown if the <paramref name="type"/> does not have a <see cref="Type.FullName"/> or if the metadata for the specified type cannot be found in the <paramref name="compilation"/>.
  /// </exception>
  public static INamedTypeSymbol GetInstantiatedGeneric(this Type type, Compilation compilation, params ITypeSymbol[] elementTypes) {
    // Get the ImmutableArray<T> generic type definition from the compilation
    if (type.FullName is null) {
      throw new InvalidOperationException("Cannot operate on anonymous types.");
    }
    
    var genericType = compilation.GetTypeByMetadataName(type.FullName);
    
    if (genericType == null)
      throw new InvalidOperationException($"Could not find {type.FullName} type");
    
    // Construct the generic type with the provided element type(s)
    return genericType.Construct(elementTypes);
  }

  /// <summary>
  /// Constructs an instantiated generic type from the provided unbound generic type and type arguments.
  /// </summary>
  /// <param name="type">The unbound generic type represented by an <see cref="ITypeSymbol"/>.</param>
  /// <param name="elementTypes">An array of <see cref="ITypeSymbol"/> representing the generic type arguments.</param>
  /// <returns>
  /// An <see cref="INamedTypeSymbol"/> representing the constructed generic type with the provided type arguments.
  /// </returns>
  /// <exception cref="InvalidOperationException">
  /// Thrown if the provided type is not an unbound generic type or if the type cannot be constructed with the provided type arguments.
  /// </exception>
  public static INamedTypeSymbol GetInstantiatedGeneric(this ITypeSymbol type, params ITypeSymbol[] elementTypes) {
    if (type is not INamedTypeSymbol namedType) {
      throw new InvalidOperationException($"Type '{type.ToDisplayString()}' is not an unbound generic.");
    }
    
    // Construct the generic type with the provided element type(s)
    return namedType.Construct(elementTypes);
  }

  /// <summary>
  /// Generates a sanitized string representation of the given <see cref="ITypeSymbol"/>.
  /// For generic types, the result includes a concatenated format of the type's name and its type arguments.
  /// </summary>
  /// <param name="type">The <see cref="ITypeSymbol"/> to generate a sanitized name for.</param>
  /// <returns>
  /// A sanitized string representation of the type, formatted as the type name. For generic types,
  /// the type arguments are included as an underscore-separated list.
  /// </returns>
  public static string GetSanitizedTypeName(this ITypeSymbol type) {
    return type is not INamedTypeSymbol { IsGenericType: true } namedType ? type.Name 
        : $"{type.Name}_{namedType.TypeArguments
            .Select(x => x.GetSanitizedTypeName())
            .Joining("_")}";

  }
  
}