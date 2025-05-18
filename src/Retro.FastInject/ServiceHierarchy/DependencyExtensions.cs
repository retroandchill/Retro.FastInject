using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Retro.FastInject.Annotations;
using Retro.FastInject.Utils;

namespace Retro.FastInject.ServiceHierarchy;

internal static class DependencyExtensions {

  public static IEnumerable<ServiceDeclaration> GetInjectedServices(this ITypeSymbol classSymbol) {
    var alreadyImported = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
    return classSymbol.GetAttributes()
        .SelectMany(x => {
          if (x.IsOfAttributeType<DependencyAttribute>()) {
            return [GetServiceDeclaration(x)];
          }

          if (!x.IsOfAttributeType<ImportAttribute>()) return [];

          var importedType = x.ImportedType();
          return alreadyImported.Add(importedType) ? importedType.GetInjectedServices() : [];
        });
  }

  private static ITypeSymbol ImportedType(this AttributeData attribute) {
    if (attribute.AttributeClass is null) {
      throw new InvalidOperationException();
    }

    ITypeSymbol? importedType;
    if (attribute.AttributeClass.IsGenericType) {
      importedType = attribute.AttributeClass.TypeArguments[0] as ITypeSymbol;
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
      scope = attribute.ConstructorArguments[1].Value is ServiceScope s ? s : ServiceScope.Singleton;
    }
    
    return new ResolvedDependencyArguments(serviceType, scope);
  }
  
  private static IEnumerable<ITypeSymbol> GetAllSuperclasses(this ITypeSymbol type) {
    return type.WalkUpInheritanceHierarchy()
        .Concat(type.AllInterfaces)
        .Distinct(SymbolEqualityComparer.Default)
        .Cast<ITypeSymbol>();
  }
  
  private static IEnumerable<ITypeSymbol> WalkUpInheritanceHierarchy(this ITypeSymbol type) {
    yield return type;
    var currentType = type;
    while (currentType.BaseType is not null) {
      yield return currentType.BaseType;
      currentType = currentType.BaseType;
    }
  }

  public static Dictionary<ITypeSymbol, List<ServiceDeclaration>> GetDependencies(this IEnumerable<ServiceDeclaration> services) {
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

  public static bool IsSpecialInjectableType(this ITypeSymbol currentType) {
    return currentType.SpecialType == SpecialType.System_Object;
  }
  
}