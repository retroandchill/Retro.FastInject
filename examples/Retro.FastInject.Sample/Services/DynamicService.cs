using System;
namespace Retro.FastInject.Sample.Services;

public sealed class DynamicService(ITransientService transient) : IDynamicService, IDisposable {

  public void Dispose() {
    // Do nothing
  }
}