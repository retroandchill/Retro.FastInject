using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
namespace Retro.FastInject.Tests.Utils;

public static class GeneratorTestHelpers {
  public static Compilation CreateCompilation(string source, params Type[] additionalTypes) {
    var assemblies = AppDomain.CurrentDomain.GetAssemblies()
        .Concat(additionalTypes.Select(t => t.Assembly))
        .Select(a => a.Location)
        .Where(a => !string.IsNullOrEmpty(a))
        .Select(l => MetadataReference.CreateFromFile(l));
    return CSharpCompilation.Create("compilation",
                                    [CSharpSyntaxTree.ParseText(source)],
                                    assemblies,
                                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
  }
}