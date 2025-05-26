using System;
namespace Retro.FastInject.Sample.Services;

public sealed class DynamicService(ITransientService transient) : IDynamicService, IDisposable {

  public ITransientService Transient { get; } = transient;
  
  public void Dispose() {
    // Do nothing
  }
}