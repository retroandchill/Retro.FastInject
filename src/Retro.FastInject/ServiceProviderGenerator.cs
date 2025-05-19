using System;
using System.Linq;
using HandlebarsDotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Retro.FastInject.Annotations;
using Retro.FastInject.ServiceHierarchy;
using Retro.FastInject.Utils;
namespace Retro.FastInject;

/// <summary>
/// Generates code for classes marked with the [ServiceProvider] attribute.
/// </summary>
/// <remarks>
/// The <see cref="ServiceProviderGenerator"/> is a Roslyn incremental generator
/// that identifies classes decorated with the <c>[ServiceProvider]</c> attribute
/// and generates corresponding provider-specific source code during compilation.
/// </remarks>
[Generator]
public class ServiceProviderGenerator : IIncrementalGenerator {
  /// <inheritdoc />
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

    // Validate constructor dependencies for all service implementations
    foreach (var service in manifest.GetUnnamedServices().Concat(manifest.GetKeyedServices())) {
      try {
        manifest.CheckConstructorDependencies(service);
      } catch (InvalidOperationException ex) {
        context.ReportDiagnostic(Diagnostic.Create(
          new DiagnosticDescriptor(
            "FastInject001",
            "Dependency Injection Error",
            ex.Message,
            "DependencyInjection",
            DiagnosticSeverity.Error,
            true
          ),
          classSymbol.Locations.FirstOrDefault()
        ));
      }
    }
  
    // Prepare constructor resolution information for template
    var constructorResolutions = manifest.GetAllConstructorResolutions()
        .Select(cr => new {
            cr.Type,
            Parameters = cr.Parameters.Select(p => {
              var type = p.ParameterType.ToDisplayString();
              return p.Key is not null ? $"((IKeyedServiceProvider<{type}>) this).GetKeyedService({p.Key})" : $"((IServiceProvider<{type}>) this).GetService()";
            }).Joining(", ")
        })
        .ToDictionary(x => x.Type, x => x.Parameters, TypeSymbolEqualityComparer.Instance);
  
    var templateParams = new {
        Namespace = classSymbol.ContainingNamespace.ToDisplayString(),
        ClassName = classSymbol.Name,
        RegularServices = manifest.GetUnnamedServices()
            .Select(x => new ServiceInjection(x, constructorResolutions.TryGetValue(x.Type, out var parameters) ? parameters : ""))
            .ToList(),
        KeyedServices = manifest.GetKeyedServices()
            .GroupBy(x => x.Type, TypeSymbolEqualityComparer.Instance)
            .Select(x => new {
                ServiceType = x.Key.ToDisplayString(),
                FromOtherService = manifest.TryGetIndirectService(x.Key, out var implementationType),
                OtherType = implementationType?.ToDisplayString(),
                Options = x.Select(y => new ServiceInjection(y, constructorResolutions.TryGetValue(x.Key, out var parameters) ? parameters : "")).ToList()
            })
            .ToList(),
        Singletons = manifest.GetServicesByLifetime(ServiceScope.Singleton)
            .Where(x => x.ImplementationType is null && x.AssociatedSymbol is not IFieldSymbol and not IPropertySymbol)
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

    var handlebars = Handlebars.Create();
    handlebars.Configuration.TextEncoder = null;
    var template = handlebars.Compile(SourceTemplates.ServiceProviderTemplate);
    
    var templateResult = template(templateParams);
    context.AddSource("ServiceProvider.g.cs", templateResult);
    
    Console.WriteLine(manifest.ToString());
  }

  

}