using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Retro.FastInject.Utils;

/// <summary>
/// Provides extension methods for working with types represented as <see cref="ITypeSymbol"/>.
/// </summary>
public static class TypeExtensions {
  /// <summary>
  /// Checks if the type represented by the current <see cref="ITypeSymbol"/> is of the specified type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The type to compare against.</typeparam>
  /// <param name="type">The current type symbol to check.</param>
  /// <returns>
  /// True if the current type symbol is of the specified type; otherwise, false.
  /// </returns>
  public static bool IsOfType<T>(this ITypeSymbol type) {
    if (type.ToString() == typeof(T).FullName) {
      return true;
    }

    if (typeof(T).IsClass && type is { TypeKind: TypeKind.Class, BaseType: not null }) {
      return type.BaseType.IsOfType<T>();
    }

    if (typeof(T).IsInterface && type.TypeKind is TypeKind.Interface or TypeKind.Class) {
      return type.Interfaces
          .Any(i => i.IsOfType<T>());
    }

    return false;
  }

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

}