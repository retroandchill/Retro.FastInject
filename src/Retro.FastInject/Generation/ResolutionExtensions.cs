using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Retro.FastInject.Model.Manifest;
using System.Linq;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Retro.FastInject.Annotations;
using Retro.FastInject.Comparers;
using Retro.FastInject.Model.Attributes;
using Retro.FastInject.Utils;
using Retro.SourceGeneratorUtilities.Utilities.Attributes;

namespace Retro.FastInject.Generation;

/// <summary>
/// Provides extension methods for resolving and manipulating parameter resolution details
/// used in the dependency injection service hierarchy.
/// </summary>
public static class ResolutionExtensions {
  /// <summary>
  /// Checks if all dependencies in the constructor of the specified service registration can be resolved and records the resolution.
  /// </summary>
  /// <param name="serviceManifest">The service manifest to validate.</param>
  /// <param name="declaration">The service registration representing the type and its associated symbol to check.</param>
  /// <param name="compilation">The Roslyn compilation providing semantic analysis for the type and its dependencies.</param>
  /// <exception cref="InvalidOperationException">
  /// Thrown when dependencies cannot be resolved, or when the type has multiple public constructors, or if the type is not a named type.
  /// </exception>
  public static void CheckConstructorDependencies(this ServiceManifest serviceManifest, 
                                                  ServiceRegistration declaration, Compilation compilation) {
    var type = declaration.Type;
    if (type is not INamedTypeSymbol namedTypeSymbol) {
      throw new InvalidOperationException($"Type '{type.ToDisplayString()}' is not a named type.");
    }

    var constructor = declaration.AssociatedSymbol switch {
        IMethodSymbol method => method.ValidateFactoryMethod(declaration.ResolvedType),
        null => namedTypeSymbol.GetValidConstructor(),
        _ => null
    };
    if (constructor is null) {
      return;
    }

    var missingDependencies = new List<string>();

    // Create constructor resolution record
    var constructorResolution = new ConstructorResolution {
        Constructor = constructor,
        Type = type
    };

    foreach (var parameter in constructor.Parameters) {
      serviceManifest.ResolveParameterDependencies(parameter, declaration.Lifetime, constructorResolution, compilation, missingDependencies);
    }

    // Store the constructor resolution
    serviceManifest.AddConstructorResolution(constructorResolution);

    if (missingDependencies.Count > 0) {
      throw new InvalidOperationException(
          $"Cannot resolve the following dependencies for type '{type.ToDisplayString()}':\n" +
          $"- {string.Join("\n- ", missingDependencies)}");
    }
  }
  
  private static IMethodSymbol? GetValidConstructor(this INamedTypeSymbol type) {
    var publicConstructors = type.Constructors
        .Where(c => c.DeclaredAccessibility == Accessibility.Public)
        .ToArray();

    var explicitConstructors = publicConstructors
        .Where(x => !x.IsImplicitlyDeclared)
        .ToArray();

    if (explicitConstructors.Length > 0) {
      return explicitConstructors.Length switch {
          > 1 => throw new InvalidOperationException(
              $"Type '{type.ToDisplayString()}' has multiple public constructors. Only one public constructor is allowed for dependency injection."),
          _ => explicitConstructors[0]
      };
    }

    // Fallback to the first public constructor if there are no explicit constructors
    return publicConstructors.FirstOrDefault();
  }

  /// <summary>
  /// Validates if the provided factory method is suitable for creating the desired type
  /// and constructs the method if necessary for generic types.
  /// </summary>
  /// <param name="methodSymbol">The factory method symbol to validate.</param>
  /// <param name="desiredType">The type the factory method is expected to produce.</param>
  /// <returns>The validated method symbol, possibly constructed for generic types.</returns>
  /// <exception cref="InvalidOperationException">
  /// Thrown if the factory method does not return a named non-void type,
  /// or if the returned type does not match the desired type.
  /// </exception>
  public static IMethodSymbol ValidateFactoryMethod(this IMethodSymbol methodSymbol, ITypeSymbol desiredType) {
    if (methodSymbol.ReturnsVoid || methodSymbol.ReturnType is not INamedTypeSymbol) {
      throw new InvalidOperationException(
          $"Factory method '{methodSymbol.ToDisplayString()}' must return a named non-void type.");
    }

    if (methodSymbol is {
            IsGenericMethod: true,
            ReturnType: INamedTypeSymbol { IsGenericType: true } genericReturn
        } && desiredType is INamedTypeSymbol { IsGenericType: true} desiredGeneric 
          && TypeSymbolEqualityComparer.Instance.Equals(genericReturn, desiredGeneric.ConstructedFrom)) {
      return methodSymbol.Construct(desiredGeneric.TypeArguments.ToArray());
    }

    return methodSymbol;
  }
  
  private static void ResolveParameterDependencies(this ServiceManifest serviceManifest, 
                                                   IParameterSymbol parameter, 
                                                   ServiceScope serviceScope, 
                                                   ConstructorResolution constructorResolution,
                                                   Compilation compilation,
                                                   List<string> missingDependencies) {
    var (isNullable, paramType) = parameter.Type.CheckIfNullable();
    
    // Create parameter resolution
    var parameterResolution = new ParameterResolution {
        Parameter = parameter,
        ParameterType = paramType,
        IsNullable = isNullable,
        UseDynamic = serviceManifest.AllowDynamicResolution && parameter.HasAttribute<AllowDynamicAttribute>()
    };

    // Check for FromKeyedServices attribute
    var keyName = parameter.GetAttributes()
        .Select(a => a.TryGetFromKeyedServicesOverview(out var info) ? info : null)
        .Select(o => o?.Key)
        .FirstOrDefault();
    parameterResolution.Key = keyName;
  
    var canResolve = serviceManifest.CanResolve(keyName, paramType, parameterResolution, compilation, 
                                                out var selectedService);

    if (canResolve && parameterResolution.IsLazy && serviceScope == ServiceScope.Transient
        && selectedService!.Lifetime == ServiceScope.Transient) {
      canResolve = false;
      parameterResolution.CreatesLazyTransientCycle = true;
    }

    parameterResolution.SelectedService = selectedService;
    parameterResolution.DefaultValue = parameter.GetDefaultValueString();
    constructorResolution.Parameters.Add(parameterResolution);

    if (!canResolve && parameterResolution.UseDynamic) {
      return;
    }

    if (canResolve || isNullable || parameterResolution.DefaultValue is not null) return;
      
    AddMissingDependencyDetails(missingDependencies, paramType, keyName, parameterResolution);
  }
  private static void AddMissingDependencyDetails(List<string> missingDependencies, ITypeSymbol paramType, string? keyName, ParameterResolution parameterResolution) {
    // Add the missing dependency to the list with detailed information
    var dependency = new StringBuilder(paramType.ToDisplayString());
    if (keyName != null) {
      dependency.Append($" with key '{keyName}'");
    }

    // Add more specific error information
    if (parameterResolution.CreatesLazyTransientCycle) {
      dependency.Append(" (Lazy transient cycle detected)");
    } else if (parameterResolution.HasNoDeclaration) {
      dependency.Append(" (No service declaration found)");
    } else if (parameterResolution is { HasMultipleRegistrations: true, MultipleServices.Count: > 0 }) {
      dependency.Append($" (Multiple registrations found: {parameterResolution.MultipleServices.Count})");
      // Optionally add details about the multiple registrations
      foreach (var service in parameterResolution.MultipleServices) {
        var implType = service.ImplementationType?.ToDisplayString() ?? service.Type.ToDisplayString();
        dependency.Append($"\n  -- {implType}" + (service.Key != null ? $" with key '{service.Key}'" : ""));
      }
    }
  
    missingDependencies.Add(dependency.ToString());
  }

  private static bool CanResolve(this ServiceManifest serviceManifest, 
                                 string? keyName, 
                                 ITypeSymbol paramType, 
                                 ParameterResolution parameterResolution, 
                                 Compilation compilation, 
                                 [NotNullWhen(true)] out ServiceRegistration? selectedService) {
    // Check if the dependency can be resolved
    var canResolve = false;
    selectedService = null;

    if (serviceManifest.TryGetServices(paramType, out var registrations)) {
      try {
        selectedService = registrations.SingleOrDefault(r => keyName is null || r.Key == keyName);
      } catch (InvalidOperationException) {
        parameterResolution.HasMultipleRegistrations = true;
        parameterResolution.MultipleServices = registrations.ToList();
        selectedService = null;
      }

      if (selectedService is not null) {
        selectedService = serviceManifest.ResolveConcreteType(selectedService);
      }
      
      canResolve = selectedService != null;
    } else {
      // Check if the type is a collection type
      if (keyName is null && paramType is INamedTypeSymbol { IsGenericType: true } namedType 
                          && serviceManifest.CanResolveGenericType(namedType, compilation, parameterResolution,
                                                   keyName, ref selectedService)) {
        return true;
      }
      parameterResolution.HasNoDeclaration = true;
    }

    return canResolve;
  }

  private static bool CanResolveGenericType(this ServiceManifest serviceManifest, INamedTypeSymbol namedType, Compilation compilation, 
                                           ParameterResolution targetParameter,
                                           string? keyName,
                                           [NotNullWhen(true)] ref ServiceRegistration? selectedService) {
    if (namedType.IsGenericCollectionType()) {
      return serviceManifest.TryResolveServiceCollection(namedType, compilation, targetParameter.Parameter, 
                                                         out selectedService);
    }

    if (namedType.IsLazyType()) {
      return serviceManifest.TryResolveLazyService(namedType, compilation, targetParameter, keyName, out selectedService);
    }

    if (!serviceManifest.TryGetServices(namedType.ConstructedFrom, out var registrations)) {
      return false;
    }

    ServiceRegistration? unboundService = null;
    try {
      unboundService = registrations.SingleOrDefault(r => keyName is null || r.Key == keyName);
    } catch (InvalidOperationException) {
      targetParameter.HasMultipleRegistrations = true;
      targetParameter.MultipleServices = registrations.ToList();
    }

    if (unboundService is null) return false;

    var concreteUnbound = serviceManifest.ResolveConcreteType(unboundService);
    var implementationType = concreteUnbound.Type.GetInstantiatedGeneric(namedType.TypeArguments.ToArray());
      
    selectedService = serviceManifest.AddService(implementationType, unboundService.Lifetime, key: unboundService.Key, associatedSymbol: unboundService.AssociatedSymbol);
    foreach (var superclass in implementationType.GetAllSuperclasses()
                 .Where(x => !x.IsSpecialInjectableType() && !x.Equals(implementationType, SymbolEqualityComparer.Default))) {
      serviceManifest.AddService(superclass, unboundService.Lifetime, implementationType, unboundService.AssociatedSymbol, unboundService.Key);
      
      // We need to add this type to any collection declarations that may already exist
      var immutableArrayType = typeof(ImmutableArray<>).GetInstantiatedGeneric(compilation, superclass);
      if (!serviceManifest.TryGetServices(immutableArrayType, out var immutableArrayServices)) continue;

      foreach (var service in immutableArrayServices) {
        service.CollectedServices?.Add(selectedService);
      }
    }
    serviceManifest.CheckConstructorDependencies(selectedService, compilation);
      
    return true;
  }

  /// <summary>
  /// Determines whether the specified generic type is a generic collection type, such as IEnumerable&lt;T>,
  /// IReadOnlyCollection&lt;T>, IReadOnlyList&lt;T>, or ImmutableArray&lt;T>.
  /// </summary>
  /// <param name="genericType">The named type symbol representing the generic type to check.</param>
  /// <returns>
  /// True if the specified type is a generic collection type; otherwise, false.
  /// </returns>
  public static bool IsGenericCollectionType(this INamedTypeSymbol genericType) {
    return genericType.ConstructedFrom.ToDisplayString() is "System.Collections.Generic.IEnumerable<T>" or
        "System.Collections.Generic.IReadOnlyCollection<T>" or
        "System.Collections.Generic.IReadOnlyList<T>" or
        "System.Collections.Immutable.ImmutableArray<T>";
  }

  /// <summary>
  /// Determines if the specified generic type is a lazy type (e.g., System.Lazy&lt;T&gt;).
  /// </summary>
  /// <param name="genericType">The generic type to check.</param>
  /// <returns>
  /// True if the specified generic type represents a lazy type, otherwise false.
  /// </returns>
  public static bool IsLazyType(this INamedTypeSymbol genericType) {
    return genericType.ConstructedFrom.ToDisplayString() is "System.Lazy<T>";
  }
  
  private static bool TryResolveServiceCollection(this ServiceManifest serviceManifest, INamedTypeSymbol namedType, Compilation compilation, IParameterSymbol targetParameter, 
                                                  [NotNullWhen(true)] out ServiceRegistration? selectedService) {
    var elementType = namedType.TypeArguments[0];
    if (!serviceManifest.TryGetServices(elementType, out var elementServices)) {
      elementServices = [];
    }
    
    if (targetParameter.HasAttribute<RequireNonEmptyAttribute>() && elementServices.Count == 0) {
      selectedService = null;
      return false;
    }

    var immutableArrayType = typeof(ImmutableArray<>).GetInstantiatedGeneric(compilation, elementType);
    selectedService = serviceManifest.AddService(immutableArrayType, ServiceScope.Transient,
        collectedServices: elementServices
            .Select(serviceManifest.ResolveConcreteType)
            .Where(x => x.Type is not INamedTypeSymbol { IsGenericType: true } genericType 
                        || genericType.TypeArguments.All(y => y is not ITypeParameterSymbol))
            .ToList());
    var readOnlyListType = typeof(IReadOnlyList<>).GetInstantiatedGeneric(compilation, elementType);
    serviceManifest.AddService(readOnlyListType, ServiceScope.Transient, immutableArrayType);
    var readOnlyCollectionType = typeof(IReadOnlyCollection<>).GetInstantiatedGeneric(compilation, elementType);
    serviceManifest.AddService(readOnlyCollectionType, ServiceScope.Transient, immutableArrayType);
    var enumerableType = typeof(IEnumerable<>).GetInstantiatedGeneric(compilation, elementType);
    serviceManifest.AddService(enumerableType, ServiceScope.Transient, immutableArrayType);
    
    return true;
  }

  private static bool TryResolveLazyService(this ServiceManifest serviceManifest,
                                            INamedTypeSymbol namedType, 
                                            Compilation compilation, 
                                            ParameterResolution targetParameter,
                                            string? keyName,
                                            [NotNullWhen(true)] out ServiceRegistration? selectedService) {
    targetParameter.IsLazy = true;
    var elementType = namedType.TypeArguments[0];
    if (!serviceManifest.CanResolve(keyName, elementType, targetParameter, compilation, out var resolvedServiceRegistration)) {
      selectedService = null;
      return false;
    }
    
    selectedService = resolvedServiceRegistration;
    return true;
  }
  
  private static ServiceRegistration ResolveConcreteType(this ServiceManifest serviceManifest, 
                                                         ServiceRegistration declaration) {
    if (declaration.ImplementationType is null || !serviceManifest.TryGetServices(declaration.ImplementationType, 
                                                                               out var possibleImpls)) {
      return declaration;
    }
    
    try {
      var implService = possibleImpls
          .SingleOrDefault(x => declaration.Key is null || declaration.Key == x.Key);
      if (implService is not null) {
        return implService;
      }
    } catch (InvalidOperationException) {
      // In this case swallow the exception and keep the abstract service
      // as the selected service.
    }
    
    return declaration;
  }
}