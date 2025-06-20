﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Retro.FastInject.Annotations;
using Retro.FastInject.Comparers;
using Retro.FastInject.Model.Attributes;
using Retro.FastInject.Model.Detection;
using Retro.FastInject.Model.Manifest;
using Retro.FastInject.Utils;

namespace Retro.FastInject.Generation;

/// <summary>
/// Provides extension methods for analyzing and retrieving dependency injection service details
/// from Roslyn `ITypeSymbol` representations of classes or types.
/// </summary>
public static class DependencyExtensions {
  /// <summary>
  /// Retrieves a collection of services injected into the specified class using attributes,
  /// factory methods, or instance members. This method analyzes the class for services that
  /// are declared via attributes, factory methods, or instance-level fields/properties.
  /// </summary>
  /// <param name="classSymbol">The class symbol to analyze for injected services.</param>
  /// <returns>A collection of <see cref="ServiceDeclaration"/> objects representing the
  /// injected services and their associated metadata.</returns>
  public static ServiceDeclarationCollection GetInjectedServices(this ITypeSymbol classSymbol) {
    var allowDynamicServices = false;
    
    var alreadyImported = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

    // Get services from class attributes
    var attributeServices = classSymbol.GetAttributes()
        .SelectMany(x => {
          if (x.TryGetServiceProviderOverview(out var serviceProviderOverview)) {
            allowDynamicServices = allowDynamicServices || serviceProviderOverview.AllowDynamicRegistrations;
            return [];
          }
          
          if (x.TryGetDependencyOverview(out var dependencyOverview)) {
            return [GetServiceDeclaration(dependencyOverview)];
          }

          if (!x.TryGetImportOverview(out var importOverview)) return [];
          
          allowDynamicServices = allowDynamicServices || importOverview.AllowDynamicRegistrations;
          return alreadyImported.Add(importOverview.ModuleType) ? importOverview.ModuleType.GetInjectedServices() : [];
        });

    // Get services from factory methods
    var factoryServices = classSymbol.GetMembers()
        .OfType<IMethodSymbol>()
        .SelectMany(GetFactoryServices);

    // Get services from instance members
    var instanceServices = classSymbol.GetMembers()
        .Where(m => m is IFieldSymbol or IPropertySymbol)
        .SelectMany(GetInstanceServices);

    var services = attributeServices.Concat(factoryServices)
        .Concat(instanceServices)
        .ToImmutableArray();

    if (classSymbol is not INamedTypeSymbol namedClassSymbol) {
      throw new InvalidOperationException("Service provider must be a Named Type.");
    }
    return new ServiceDeclarationCollection(namedClassSymbol, services, allowDynamicServices);
  }

  private static ServiceDeclaration GetServiceDeclaration(DependencyOverview attributeInfo) {
    return new ServiceDeclaration(attributeInfo.Type is INamedTypeSymbol { IsUnboundGenericType: true} unbound 
                                      ? unbound.ConstructedFrom : attributeInfo.Type, attributeInfo.Scope, attributeInfo.Key);
  }

  /// <summary>
  /// Retrieves all superclasses and implemented interfaces of the specified type symbol,
  /// including duplicates that are resolved based on type argument correctness and uniqueness.
  /// </summary>
  /// <param name="type">The type symbol for which superclasses and interfaces are to be determined.</param>
  /// <returns>A collection of <see cref="ITypeSymbol"/> representing the superclasses and interfaces
  /// of the specified type, filtered for validity as type arguments and ensuring uniqueness.</returns>
  public static IEnumerable<ITypeSymbol> GetAllSuperclasses(this ITypeSymbol type) {
    return type.WalkUpInheritanceHierarchy()
        .Concat(type.AllInterfaces)
        .Distinct(TypeSymbolEqualityComparer.Instance)
        .Where(x => x.IsValidForTypeArgument());
  }


  private static IEnumerable<ITypeSymbol> WalkUpInheritanceHierarchy(this ITypeSymbol type) {
    yield return type;
    var currentType = type;
    while (currentType.BaseType is not null) {
      yield return currentType.BaseType;
      currentType = currentType.BaseType;
    }
  }

  /// <summary>
  /// Analyzes the provided collection of service declarations and organizes them into a dictionary
  /// where the keys are service types and the values are lists of associated service declarations.
  /// This method ensures the hierarchical relationship between service types is maintained by
  /// including all superclasses of a service type in the dictionary.
  /// </summary>
  /// <param name="services">The collection of service declarations to process.</param>
  /// <returns>A dictionary where the keys are <see cref="ITypeSymbol"/> objects representing service types,
  /// and the values are lists of <see cref="ServiceDeclaration"/> objects associated with each type.</returns>
  public static Dictionary<ITypeSymbol, List<ServiceDeclaration>> GetDependencies(
      this IEnumerable<ServiceDeclaration> services) {
    var result = new Dictionary<ITypeSymbol, List<ServiceDeclaration>>(SymbolEqualityComparer.Default);
    foreach (var declaration in services) {
      foreach (var type in declaration.Type.GetAllSuperclasses()) {
        if (!result.ContainsKey(type)) {
          result[type] = [];
        }

        result[type].Add(declaration);
      }
    }

    return result;
  }


  /// <summary>
  /// Determines whether the specified type is considered a "special injectable type."
  /// Special injectable types are predefined system types that are not meant to be
  /// directly treated as dependency injection targets, such as primitive types, arrays,
  /// delegates, and certain system-defined types.
  /// </summary>
  /// <param name="currentType">The type symbol to evaluate for being a special injectable type.</param>
  /// <returns>
  /// A boolean value indicating whether the given <paramref name="currentType"/> is a
  /// special injectable type. Returns <c>true</c> if the type matches predefined special
  /// types, otherwise <c>false</c>.
  /// </returns>
  public static bool IsSpecialInjectableType(this ITypeSymbol currentType) {
    return currentType.SpecialType is SpecialType.System_Object or SpecialType.System_ValueType or
        SpecialType.System_Enum or SpecialType.System_IDisposable or SpecialType.System_Array or
        SpecialType.System_Delegate or
        SpecialType.System_MulticastDelegate or
        SpecialType.System_Nullable_T or
        SpecialType.System_Void;
  }

  private static IEnumerable<ServiceDeclaration> GetFactoryServices(IMethodSymbol methodSymbol) {
    var factoryAttribute = methodSymbol.GetAttributes()
        .Select(a => a.TryGetFactoryOverview(out var info) ? info : null)
        .FirstOrDefault();
    if (factoryAttribute == null) {
      return [];
    }

    var returnType = methodSymbol.ReturnType;
    if (methodSymbol is {
            IsGenericMethod: true,
            ReturnType: INamedTypeSymbol { IsGenericType: true } generic
        } && generic.TypeArguments.All(x => x is ITypeParameterSymbol)) {
      returnType = generic.ConstructedFrom;
    }

    return [new ServiceDeclaration(returnType, factoryAttribute.Scope, factoryAttribute.Key, methodSymbol)];
  }

  private static IEnumerable<ServiceDeclaration> GetInstanceServices(ISymbol memberSymbol) {
    var instanceAttribute = memberSymbol.GetAttributes()
        .Select(a => a.TryGetInstanceOverview(out var info) ? info : null)
        .SingleOrDefault();
    if (instanceAttribute is null) {
      return [];
    }

    var memberType = memberSymbol switch {
        IFieldSymbol fieldSymbol => fieldSymbol.Type,
        IPropertySymbol propertySymbol => propertySymbol.Type,
        _ => null
    };

    if (memberType == null) {
      return [];
    }

    // Instance services are always Singleton scope
    return [new ServiceDeclaration(memberType, ServiceScope.Singleton, instanceAttribute.Key, memberSymbol)];
  }
}