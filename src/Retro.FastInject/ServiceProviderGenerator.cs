using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using HandlebarsDotNet;
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
    var manifest = classSymbol.GenerateManifest();

    var templateParams = new {
        Namespace = classSymbol.ContainingNamespace.ToDisplayString(),
        ClassName = classSymbol.Name,
        RegularServices = manifest.GetUnnamedServices()
            .Select(x => new ServiceInjection(x))
            .ToList(),
        KeyedServices = manifest.GetKeyedServices()
            .GroupBy(x => x.Type, TypeSymbolEqualityComparer.Instance)
            .Select(x => new {
                ServiceType = x.Key.ToDisplayString(),
                FromOtherService = manifest.TryGetIndirectService(x.Key, out var implementationType),
                OtherType = implementationType?.ToDisplayString(),
                Options = x.Select(y => new ServiceInjection(y)).ToList()
            })
            .ToList(),
        Singletons = manifest.GetServicesByLifetime(ServiceScope.Singleton)
            .Where(x => x.ImplementationType is null)
            .Select(x => new {
                Type = x.Type.ToDisplayString(),
                Name = x.FieldName
            }),
        Scoped = manifest.GetServicesByLifetime(ServiceScope.Scoped)
            .Where(x => x.ImplementationType is null)
            .Select(x => new {
                Type = x.Type.ToDisplayString(),
                Name = x.FieldName
            }),
    };

    var template = Handlebars.Compile(SourceTemplates.ServiceProviderTemplate);
    
    var templateResult = template(templateParams);
    context.AddSource("ServiceProvider.g.cs", templateResult);
    
    Console.WriteLine(manifest.ToString());
  }

  

}