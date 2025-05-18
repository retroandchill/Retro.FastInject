using System;
namespace Retro.FastInject.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public class ImportAttribute(Type moduleType) : Attribute {
  
  public Type ModuleType { get; } = moduleType;
  
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public class ImportAttribute<TModule>() : ImportAttribute(typeof(TModule));
