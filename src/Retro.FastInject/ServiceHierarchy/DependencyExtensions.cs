using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Retro.FastInject.Annotations;
using Retro.FastInject.Utils;

namespace Retro.FastInject.ServiceHierarchy;

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
  public static IEnumerable<ServiceDeclaration> GetInjectedServices(this ITypeSymbol classSymbol) {
    var alreadyImported = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

    // Get services from class attributes
    var attributeServices = classSymbol.GetAttributes()
        .SelectMany(x => {
          if (x.IsOfAttributeType<DependencyAttribute>()) {
            return [GetServiceDeclaration(x)];
          }

          if (!x.IsOfAttributeType<ImportAttribute>()) return [];

          var importedType = x.ImportedType();
          return alreadyImported.Add(importedType) ? importedType.GetInjectedServices() : [];
        });

    // Get services from factory methods
    var factoryServices = classSymbol.GetMembers()
        .OfType<IMethodSymbol>()
        .SelectMany(GetFactoryServices);

    // Get services from instance members
    var instanceServices = classSymbol.GetMembers()
        .Where(m => m is IFieldSymbol or IPropertySymbol)
        .SelectMany(GetInstanceServices);

    return attributeServices.Concat(factoryServices).Concat(instanceServices);
  }

  private static ITypeSymbol ImportedType(this AttributeData attribute) {
    if (attribute.AttributeClass is null) {
      throw new InvalidOperationException();
    }

    ITypeSymbol? importedType;
    if (attribute.AttributeClass.IsGenericType) {
      importedType = attribute.AttributeClass.TypeArguments[0];
    } else {
      importedType = attribute.ConstructorArguments[0].Value as ITypeSymbol;
    }

    if (importedType is null) {
      throw new InvalidOperationException();
    }

    return importedType;
  }

  private static ServiceDeclaration GetServiceDeclaration(AttributeData attribute) {
    var (injectedType, scope) = attribute.GetResolvedDependencyArguments();

    // Get the Key property value if it exists
    var key = attribute.NamedArguments
        .FirstOrDefault(kvp => kvp.Key == "Key")
        .Value.Value?.ToString();

    return new ServiceDeclaration(injectedType, scope, key);
  }

  private static ResolvedDependencyArguments GetResolvedDependencyArguments(this AttributeData attribute) {
    if (attribute.AttributeClass is null) {
      throw new InvalidOperationException();
    }

    ITypeSymbol? serviceType;
    if (attribute.AttributeClass.IsGenericType) {
      serviceType = attribute.AttributeClass.TypeArguments[0];
    } else {
      serviceType = attribute.ConstructorArguments[0].Value as ITypeSymbol;
    }

    if (serviceType is null) {
      throw new InvalidOperationException();
    }

    ServiceScope scope;
    if (attribute.IsOfAttributeType<SingletonAttribute>()) {
      scope = ServiceScope.Singleton;
    } else if (attribute.IsOfAttributeType<ScopedAttribute>()) {
      scope = ServiceScope.Scoped;
    } else if (attribute.IsOfAttributeType<TransientAttribute>()) {
      scope = ServiceScope.Transient;
    } else {
      scope = attribute.ConstructorArguments[1].Value is int s 
              && Enum.IsDefined(typeof(ServiceScope), s) 
          ? (ServiceScope) s : ServiceScope.Singleton;
    }

    return new ResolvedDependencyArguments(serviceType, scope);
  }

  private static IEnumerable<ITypeSymbol> GetAllSuperclasses(this ITypeSymbol type) {
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
    var factoryAttribute = methodSymbol.GetAttribute<FactoryAttribute>();
    if (factoryAttribute == null) {
      return [];
    }

    // Extract the service scope from the attribute
    ServiceScope scope = ServiceScope.Singleton; // Default is Singleton
    if (factoryAttribute.ConstructorArguments.Length > 0 &&
        factoryAttribute.ConstructorArguments[0].Value is int scopeValue) {
      scope = (ServiceScope)scopeValue;
    }

    // Extract the key (if any) from the named arguments
    var key = factoryAttribute.NamedArguments
        .FirstOrDefault(kvp => kvp.Key == "Key")
        .Value.Value?.ToString();

    return [new ServiceDeclaration(methodSymbol.ReturnType, scope, key, methodSymbol)];
  }

  private static IEnumerable<ServiceDeclaration> GetInstanceServices(ISymbol memberSymbol) {
    var instanceAttribute = memberSymbol.GetAttributes().SingleOrDefault(a => a.IsOfAttributeType<InstanceAttribute>());
    if (instanceAttribute is null) {
      return [];
    }

    // Extract the key (if any) from the named arguments
    var key = instanceAttribute.NamedArguments
        .FirstOrDefault(kvp => kvp.Key == "Key")
        .Value.Value?.ToString();

    var memberType = memberSymbol switch {
        IFieldSymbol fieldSymbol => fieldSymbol.Type,
        IPropertySymbol propertySymbol => propertySymbol.Type,
        _ => null
    };

    if (memberType == null) {
      return [];
    }

    // Instance services are always Singleton scope
    return [new ServiceDeclaration(memberType, ServiceScope.Singleton, key, memberSymbol)];
  }
}