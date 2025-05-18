using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Retro.FastInject.Annotations;
using Retro.FastInject.Utils;

namespace Retro.FastInject.ServiceHierarchy;

internal static class DependencyExtensions {

  public static IEnumerable<ServiceDeclaration> GetInjectedServices(this INamedTypeSymbol classSymbol) {
    return classSymbol.GetAttributes()
        .Where(x => x.IsOfAttributeType<DependencyAttribute>())
        .Select(GetServiceDeclaration);
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
  
}