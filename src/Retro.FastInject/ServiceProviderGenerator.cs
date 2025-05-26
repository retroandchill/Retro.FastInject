using System;
using System.Linq;
using HandlebarsDotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Retro.FastInject.Annotations;
using Retro.FastInject.Comparers;
using Retro.FastInject.Generation;
using Retro.FastInject.Model.Detection;
using Retro.FastInject.Model.Template;

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
  private const string DependencyInjection = "DependencyInjection";
  /// <inheritdoc />
  public void Initialize(IncrementalGeneratorInitializationContext context) {
    // Get all class declarations with [ServiceProvider] attribute
    var serviceProviderClasses = context.SyntaxProvider
        .CreateSyntaxProvider(
            predicate: static (s, _) => s is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
            transform: static (ctx, _) => {
              var classNode = (ClassDeclarationSyntax)ctx.Node;
              var symbol = ModelExtensions.GetDeclaredSymbol(ctx.SemanticModel, classNode);

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
    if (!classSymbol.DeclaringSyntaxReferences
            .Any(x => x.GetSyntax() is ClassDeclarationSyntax classDeclaration
                      && classDeclaration.Modifiers.Any(y => y.IsKind(SyntaxKind.PartialKeyword)))) {
      context.ReportDiagnostic(
          Diagnostic.Create(new DiagnosticDescriptor(
                                "FastInject001",
                                "Dependency Injection Error",
                                $"Class {classSymbol.Name} must be declared partial",
                                DependencyInjection,
                                DiagnosticSeverity.Error,
                                true
                            ),
                            classSymbol.Locations.FirstOrDefault()
          ));
      return;
    }
    var services = classSymbol.GetInjectedServices();
    ValidateConstructors(services, context);
    var manifest = services.GenerateManifest();

    // First, resolve all constructor dependencies for all service implementations
    var explicitServices = manifest.GetAllServices().ToList();
    foreach (var service in explicitServices) {
      try {
        manifest.CheckConstructorDependencies(service, compilation);
      } catch (InvalidOperationException ex) {
        context.ReportDiagnostic(Diagnostic.Create(
                                     new DiagnosticDescriptor(
                                         "FastInject002",
                                         "Dependency Injection Error",
                                         ex.Message,
                                         DependencyInjection,
                                         DiagnosticSeverity.Error,
                                         true
                                     ),
                                     classSymbol.Locations.FirstOrDefault()
                                 ));
      }
    }
    
    // After resolving all dependencies, validate the dependency graph for cycles
    try {
      manifest.ValidateDependencyGraph();
    } catch (InvalidOperationException ex) {
      context.ReportDiagnostic(Diagnostic.Create(
                                 new DiagnosticDescriptor(
                                     "FastInject003",
                                     "Circular Dependency Error",
                                     ex.Message,
                                     DependencyInjection,
                                     DiagnosticSeverity.Error,
                                     true
                                 ),
                                 classSymbol.Locations.FirstOrDefault()
                             ));
    }
    
    

    // Prepare constructor resolution information for template
    var constructorResolutions = manifest.GetAllConstructorResolutions()
        .ToDictionary(x => x.Type, x => x.Parameters, TypeSymbolEqualityComparer.Instance);

    var regularServices = manifest.GetAllServices()
        .GroupBy(x => x.Type, TypeSymbolEqualityComparer.Instance)
        .Select(x => new {
            ServiceType = x.Key.ToDisplayString(),
            IsCollection = x.Key is INamedTypeSymbol { IsGenericType: true } generic && generic.IsGenericCollectionType(),
            AppendCollections = services.AllowDynamicServices,
            Options = x.Select(y => ServiceInjection.FromResolution(y, 
                    constructorResolutions.TryGetValue(x.Key, out var parameters) ? parameters : []))
                .ToList()
        })
        .ToList();
    
    var keyedServices = regularServices
        .Select(x => x with { 
            Options = x.Options
                    .Where(y => y.Key is not null)
                    .ToList() 
        })
        .Where(x => x.Options.Count > 0)
        .ToList();
    
    var templateParams = new {
        Namespace = classSymbol.ContainingNamespace.ToDisplayString(),
        ClassName = classSymbol.Name,
        WithDynamicServices = services.AllowDynamicServices,
        Constructors = services.ContainerType.Constructors
            .Select(x => new {
              IsExplicit = !x.IsImplicitlyDeclared,  
              Params = x.Parameters.Select((p, i) => new {
                  Type = p.Type.ToDisplayString(),
                  p.Name,
                  IsLast = i == x.Parameters.Length - 1
              })
              .ToList()
            })
            .ToList(),
        RegularServices = regularServices,
        KeyedServices = keyedServices,
        Singletons = manifest.GetServicesByLifetime(ServiceScope.Singleton)
            .Where(x => x.ImplementationType is null && x.AssociatedSymbol is not IFieldSymbol and not IPropertySymbol)
            .Select(x => new {
                Type = x.Type.ToDisplayString(),
                Name = x.FieldName,
                x.IsDisposable,
                x.IsAsyncDisposable
            })
            .ToList(),
        Scoped = manifest.GetServicesByLifetime(ServiceScope.Scoped)
            .Where(x => x.ImplementationType is null)
            .Select(x => new {
                Type = x.Type.ToDisplayString(),
                Name = x.FieldName,
                x.IsDisposable,
                x.IsAsyncDisposable
            }),
    };

    var handlebars = Handlebars.Create();
    handlebars.Configuration.TextEncoder = null;

    // Register partial templates
    handlebars.RegisterTemplate("DisposableManagement", SourceTemplates.DisposableManagementTemplate);
    handlebars.RegisterTemplate("ServiceResolution", SourceTemplates.ServiceResolutionTemplate);
    handlebars.RegisterTemplate("ServiceTypeResolution", SourceTemplates.ServiceTypeResolutionTemplate);
    handlebars.RegisterTemplate("KeyedServiceSwitch", SourceTemplates.KeyedServiceSwitchTemplate);
    handlebars.RegisterTemplate("RegularServiceGetters", SourceTemplates.RegularServiceGettersTemplate);
    handlebars.RegisterTemplate("InitializingStatement", SourceTemplates.InitializingStatementTemplate);
    handlebars.RegisterTemplate("GetInitializingStatement", SourceTemplates.GetInitializingStatementTemplate);
    handlebars.RegisterTemplate("ParameterResolution", SourceTemplates.ParameterResolutionTemplate);
    handlebars.RegisterTemplate("ParametersTemplateHelper", SourceTemplates.ParametersHelperTemplate);

    handlebars.RegisterHelper("withIndent", (writer, options, _, parameters) => {
      var indent = parameters[0] as string ?? "";

      // Capture the block content
      var content = options.Template();

      // Split the content into lines
      var lines = content.Split('\n');

      // Add indentation to each line except empty lines
      var indentedLines = lines.Select(line =>
                                           string.IsNullOrWhiteSpace(line) ? line : indent + line);

      // Join the lines back together
      writer.WriteSafeString(string.Join("\n", indentedLines));
    });


    var template = handlebars.Compile(SourceTemplates.ServiceProviderTemplate);

    var templateResult = template(templateParams);
    context.AddSource($"{classSymbol.Name}.g.cs", templateResult);

    Console.WriteLine(manifest.ToString());
  }
  
  private static void ValidateConstructors(in ServiceDeclarationCollection declaration, SourceProductionContext context) {
    if (!declaration.AllowDynamicServices) {
      return;
    }
    
    var publicConstructors = declaration.ContainerType.Constructors
        .Where(c => c.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal && !c.IsImplicitlyDeclared);
    
    if (publicConstructors.Any()) {
      context.ReportDiagnostic(
          Diagnostic.Create(
              new DiagnosticDescriptor(
                  "FastInject004",
                  "Invalid Constructor Accessibility",
                  "Service provider constructors must be private or protected. Public constructors are not allowed.",
                  DependencyInjection,
                  DiagnosticSeverity.Error,
                  true
              ),
              declaration.ContainerType.Locations.FirstOrDefault()
          )
      );
    }
  }

}