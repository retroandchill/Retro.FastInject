using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Retro.ReadOnlyParams.Annotations;
namespace Retro.FastInject.Model.Detection;

/// <summary>
/// Represents a collection of service declarations associated with a specific container type.
/// </summary>
/// <remarks>
/// This struct implements <see cref="IReadOnlyList{ServiceDeclaration}"/> to provide enumeration and indexing
/// functionality over the collection of service declarations. It also stores metadata about the container type
/// and configuration settings related to dynamic service resolution.
/// </remarks>
/// <param name="containerType">The named type symbol representing the container type.</param>
/// <param name="serviceDeclarations">An immutable array of service declarations associated with the container.</param>
/// <param name="allowDynamicServices">A flag indicating whether dynamic service resolution is allowed.</param>
public readonly struct ServiceDeclarationCollection(INamedTypeSymbol containerType, 
                                                    [ReadOnly] ImmutableArray<ServiceDeclaration> serviceDeclarations,
                                                    bool allowDynamicServices) : IReadOnlyList<ServiceDeclaration> {
  
  /// <summary>
  /// Gets the named type symbol representing the container type for these service declarations.
  /// </summary>
  /// <value>
  /// An <see cref="INamedTypeSymbol"/> instance representing the container type.
  /// </value>
  public INamedTypeSymbol ContainerType { get; } = containerType;

  /// <summary>
  /// Gets a value indicating whether dynamic service resolution is allowed for this container.
  /// </summary>
  /// <value>
  /// <c>true</c> if dynamic service resolution is allowed; otherwise, <c>false</c>.
  /// </value>
  public bool AllowDynamicServices { get; } = allowDynamicServices;
  
  /// <inheritdoc />
  public int Count => serviceDeclarations.Length;
  
  /// <inheritdoc />
  public IEnumerator<ServiceDeclaration> GetEnumerator() {
    return ((IEnumerable<ServiceDeclaration>)serviceDeclarations).GetEnumerator();
  }
  
  /// <inheritdoc />
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  /// <inheritdoc />
  public ServiceDeclaration this[int index] => serviceDeclarations[index];
}