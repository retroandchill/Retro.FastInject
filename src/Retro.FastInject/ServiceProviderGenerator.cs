using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Retro.FastInject.Annotations;
using Retro.FastInject.ServiceHierarchy;
using Retro.FastInject.Utils;
namespace Retro.FastInject;

[Generator]
public class ServiceProviderGenerator : IIncrementalGenerator {
  public void Initialize(IncrementalGeneratorInitializationContext context) {
    // Get all class declarations with [ServiceProvider] attribute
    var serviceProviderClasses = context.SyntaxProvider
        .CreateSyntaxProvider(
            predicate: static (s, _) => s is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
            transform: static (ctx, _) => {
              var classNode = (ClassDeclarationSyntax)ctx.Node;
              var symbol = ctx.SemanticModel.GetDeclaredSymbol(classNode);

              if (symbol == null) return null;

              // Check if the class has ServiceProviderAttribute
              var hasServiceProviderAttr = symbol.GetAttributes()
                  .Any(attr => attr.AttributeClass?.ToDisplayString() == typeof(ServiceProviderAttribute).FullName);

              return hasServiceProviderAttr ? symbol as INamedTypeSymbol : null;
            })
        .Where(static m => m != null);

    // Combine with compilation
    var compilationAndClasses = context.CompilationProvider
        .Combine(serviceProviderClasses.Collect());

    // Generate the source
    context.RegisterSourceOutput(compilationAndClasses, (spc, source) => {
      foreach (var classSymbol in source.Right) {
        Execute(source.Left, classSymbol!, spc);
      }
    });
  }

  private static void Execute(Compilation compilation, INamedTypeSymbol classSymbol, SourceProductionContext context) {
    var dependencies = classSymbol.GetInjectedServices()
        .ToList();

    // TODO: Generate the actual service provider implementation using the collected dependencies
    // This will be implemented in the next part
  }

  

}