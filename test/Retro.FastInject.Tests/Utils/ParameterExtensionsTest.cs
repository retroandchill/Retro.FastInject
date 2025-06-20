using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Retro.FastInject.Utils;
using static Retro.FastInject.Tests.Utils.GeneratorTestHelpers;

namespace Retro.FastInject.Tests.Utils;

[TestFixture]
public class ParameterExtensionsTests {
  private Compilation _compilation;

  [OneTimeSetUp]
  public void OneTimeSetup() {
    const string source = """
                          namespace TestNamespace
                          {
                              public enum TestEnum { One = 1, Two = 2 }
                              
                              public struct TestStruct { }
                              
                              public class TestClass
                              {
                                  public void MethodWithDefaultParams(
                                      string strParam = "test",
                                      string emptyStr = "",
                                      string quotedStr = "test\"quote",
                                      char charParam = 'a',
                                      bool boolParam = true,
                                      decimal decimalParam = 123.45m,
                                      float floatParam = 123.45f,
                                      double doubleParam = 123.45d,
                                      long longParam = 123L,
                                      TestEnum enumParam = TestEnum.One,
                                      TestStruct intParam = default,
                                      string? nullableParam = null,
                                      TestEnum enumParam = (TestEnum)123)
                                  {
                                  }
                              }
                          }
                          """;

    _compilation = CreateCompilation(source);
  }

  [Test]
  public void GetDefaultValueString_NoExplicitDefaultValue_ReturnsNull() {
    // Arrange
    var methodSymbol = _compilation.GetMethodSymbol("TestNamespace.TestClass", "MethodWithDefaultParams");
    var paramWithoutDefault = methodSymbol.Parameters[0];

    // Clear the default value
    var nullDefaultParamSymbol = paramWithoutDefault.WithHasDefaultValue(false);

    // Act
    var result = nullDefaultParamSymbol.GetDefaultValueString();

    // Assert
    Assert.That(result, Is.Null);
  }

  [Test]
  public void GetDefaultValueString_NullDefaultValue_ReturnsNullString() {
    // Arrange
    var methodSymbol = _compilation.GetMethodSymbol("TestNamespace.TestClass", "MethodWithDefaultParams");
    var nullableParam = methodSymbol.Parameters[11]; // nullableParam

    // Act
    var result = nullableParam.GetDefaultValueString();

    // Assert
    Assert.That(result, Is.EqualTo("null"));
  }

  [Test]
  public void GetDefaultValueString_StringValues() {
    // Arrange
    var methodSymbol = _compilation.GetMethodSymbol("TestNamespace.TestClass", "MethodWithDefaultParams");

    // Act & Assert
    Assert.Multiple(() => {
      // Normal string
      Assert.That(methodSymbol.Parameters[0].GetDefaultValueString(), Is.EqualTo("\"test\""));
      // Empty string
      Assert.That(methodSymbol.Parameters[1].GetDefaultValueString(), Is.EqualTo("\"\""));
      // String with quotes
      Assert.That(methodSymbol.Parameters[2].GetDefaultValueString(), Is.EqualTo("\"test\\\"quote\""));
    });
  }

  [Test]
  public void GetDefaultValueString_CharValue() {
    // Arrange
    var methodSymbol = _compilation.GetMethodSymbol("TestNamespace.TestClass", "MethodWithDefaultParams");
    var charParam = methodSymbol.Parameters[3]; // charParam

    // Act
    var result = charParam.GetDefaultValueString();

    // Assert
    Assert.That(result, Is.EqualTo("'a'"));
  }

  [Test]
  public void GetDefaultValueString_BooleanValue() {
    // Arrange
    var methodSymbol = _compilation.GetMethodSymbol("TestNamespace.TestClass", "MethodWithDefaultParams");
    var boolParam = methodSymbol.Parameters[4]; // boolParam

    // Act
    var result = boolParam.GetDefaultValueString();

    // Assert
    Assert.That(result, Is.EqualTo("true"));
  }

  [Test]
  public void GetDefaultValueString_DecimalValue() {
    // Arrange
    var methodSymbol = _compilation.GetMethodSymbol("TestNamespace.TestClass", "MethodWithDefaultParams");
    var decimalParam = methodSymbol.Parameters[5]; // decimalParam

    // Act
    var result = decimalParam.GetDefaultValueString();

    // Assert
    Assert.That(result, Is.EqualTo("123.45m"));
  }

  [Test]
  public void GetDefaultValueString_FloatValue() {
    // Arrange
    var methodSymbol = _compilation.GetMethodSymbol("TestNamespace.TestClass", "MethodWithDefaultParams");
    var floatParam = methodSymbol.Parameters[6]; // floatParam

    // Act
    var result = floatParam.GetDefaultValueString();

    // Assert
    Assert.That(result, Is.EqualTo("123.45f"));
  }

  [Test]
  public void GetDefaultValueString_DoubleValue() {
    // Arrange
    var methodSymbol = _compilation.GetMethodSymbol("TestNamespace.TestClass", "MethodWithDefaultParams");
    var doubleParam = methodSymbol.Parameters[7]; // doubleParam

    // Act
    var result = doubleParam.GetDefaultValueString();

    // Assert
    Assert.That(result, Is.EqualTo("123.45d"));
  }

  [Test]
  public void GetDefaultValueString_LongValue() {
    // Arrange
    var methodSymbol = _compilation.GetMethodSymbol("TestNamespace.TestClass", "MethodWithDefaultParams");
    var longParam = methodSymbol.Parameters[8]; // longParam

    // Act
    var result = longParam.GetDefaultValueString();

    // Assert
    Assert.That(result, Is.EqualTo("123L"));
  }

  [Test]
  public void GetDefaultValueString_EnumValue() {
    // Arrange
    var methodSymbol = _compilation.GetMethodSymbol("TestNamespace.TestClass", "MethodWithDefaultParams");
    var enumParam = methodSymbol.Parameters[9]; // enumParam

    // Act
    var result = enumParam.GetDefaultValueString();

    // Assert
    Assert.That(result, Is.EqualTo("TestNamespace.TestEnum.One"));
  }

  [Test]
  public void GetDefaultValueString_EnumValueOutOfBounds() {
    // Arrange
    var methodSymbol = _compilation.GetMethodSymbol("TestNamespace.TestClass", "MethodWithDefaultParams");
    var enumParam = methodSymbol.Parameters[12]; // enumParam

    // Act
    var result = enumParam.GetDefaultValueString();

    // Assert
    Assert.That(result, Is.EqualTo("(TestNamespace.TestEnum)123"));
  }

  [Test]
  public void GetDefaultValueString_ValueType_Default() {
    // Arrange
    var methodSymbol = _compilation.GetMethodSymbol("TestNamespace.TestClass", "MethodWithDefaultParams");
    var intParam = methodSymbol.Parameters[10]; // intParam

    // Act
    var result = intParam.GetDefaultValueString();

    // Assert
    Assert.That(result, Is.EqualTo("default(TestNamespace.TestStruct)"));
  }
}

// Extension method to help with testing
internal static class TestExtensions {
  public static IParameterSymbol WithHasDefaultValue(this IParameterSymbol parameter, bool hasDefaultValue) {
    return new TestParameterSymbol(parameter, hasDefaultValue);
  }
}

// Simple wrapper to override HasDefaultValue
internal class TestParameterSymbol(IParameterSymbol original, bool hasDefaultValue) : IParameterSymbol {
  public int Ordinal => original.Ordinal;

  public bool HasExplicitDefaultValue => hasDefaultValue;

  // Delegate all other members to the original parameter
  public RefKind RefKind => original.RefKind;
  public ScopedKind ScopedKind => original.ScopedKind;

  public bool IsParams => original.IsParams;
  public bool IsParamsArray => original.IsParamsArray;
  public bool IsParamsCollection => original.IsParamsCollection;

  public bool IsOptional => original.IsOptional;

  public bool IsThis => original.IsThis;

  public bool IsDiscard => original.IsDiscard;

  public ITypeSymbol Type => original.Type;
  public NullableAnnotation NullableAnnotation => original.NullableAnnotation;

  public ImmutableArray<CustomModifier> CustomModifiers => original.CustomModifiers;

  public ImmutableArray<CustomModifier> RefCustomModifiers => original.RefCustomModifiers;

  public object? ExplicitDefaultValue => original.ExplicitDefaultValue;

  public IParameterSymbol OriginalDefinition => original.OriginalDefinition;

  // ... implement other interface members by delegating to _original
  public bool Equals(ISymbol? other) {
    return original.Equals(other);
  }

  public ImmutableArray<AttributeData> GetAttributes() {
    return original.GetAttributes();
  }

  public void Accept(SymbolVisitor visitor) {
    original.Accept(visitor);
  }

  public TResult? Accept<TResult>(SymbolVisitor<TResult> visitor) {
    return original.Accept(visitor);
  }

  public TResult Accept<TArgument, TResult>(SymbolVisitor<TArgument, TResult> visitor, TArgument argument) {
    return original.Accept(visitor, argument);
  }

  public string? GetDocumentationCommentId() {
    return original.GetDocumentationCommentId();
  }

  public string? GetDocumentationCommentXml(CultureInfo? preferredCulture = null, bool expandIncludes = false,
                                            CancellationToken cancellationToken = new CancellationToken()) {
    return original.GetDocumentationCommentXml(preferredCulture, expandIncludes, cancellationToken);
  }

  public string ToDisplayString(SymbolDisplayFormat? format = null) {
    return original.ToDisplayString(format);
  }

  public ImmutableArray<SymbolDisplayPart> ToDisplayParts(SymbolDisplayFormat? format = null) {
    return original.ToDisplayParts(format);
  }

  public string ToMinimalDisplayString(SemanticModel semanticModel, int position, SymbolDisplayFormat? format = null) {
    return original.ToMinimalDisplayString(semanticModel, position, format);
  }

  public ImmutableArray<SymbolDisplayPart> ToMinimalDisplayParts(SemanticModel semanticModel, int position,
                                                                 SymbolDisplayFormat? format = null) {
    return original.ToMinimalDisplayParts(semanticModel, position, format);
  }

  public bool Equals([NotNullWhen(true)] ISymbol? other, SymbolEqualityComparer equalityComparer) {
    return original.Equals(other, equalityComparer);
  }

  public SymbolKind Kind => original.Kind;

  public string Language => original.Language;

  public string Name => original.Name;

  public string MetadataName => original.MetadataName;

  public int MetadataToken => original.MetadataToken;

  public ISymbol ContainingSymbol => original.ContainingSymbol;

  public IAssemblySymbol ContainingAssembly => original.ContainingAssembly;

  public IModuleSymbol ContainingModule => original.ContainingModule;

  public INamedTypeSymbol ContainingType => original.ContainingType;

  public INamespaceSymbol ContainingNamespace => original.ContainingNamespace;

  public bool IsDefinition => original.IsDefinition;

  public bool IsStatic => original.IsStatic;

  public bool IsVirtual => original.IsVirtual;

  public bool IsOverride => original.IsOverride;

  public bool IsAbstract => original.IsAbstract;

  public bool IsSealed => original.IsSealed;

  public bool IsExtern => original.IsExtern;

  public bool IsImplicitlyDeclared => original.IsImplicitlyDeclared;

  public bool CanBeReferencedByName => original.CanBeReferencedByName;

  public ImmutableArray<Location> Locations => original.Locations;

  public ImmutableArray<SyntaxReference> DeclaringSyntaxReferences => original.DeclaringSyntaxReferences;

  public Accessibility DeclaredAccessibility => original.DeclaredAccessibility;

  ISymbol ISymbol.OriginalDefinition => ((ISymbol)original).OriginalDefinition;

  public bool HasUnsupportedMetadata => original.HasUnsupportedMetadata;
}