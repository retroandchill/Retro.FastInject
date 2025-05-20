using Microsoft.CodeAnalysis;

namespace Retro.FastInject.Utils;

/// <summary>
/// Represents metadata about the nullability of a type and its underlying type.
/// </summary>
/// <remarks>
/// This struct is used to encapsulate the nullability state of a type and provide
/// information about the underlying type if it is nullable.
/// </remarks>
public readonly record struct NullableData(bool IsNullable, ITypeSymbol UnderlyingType);