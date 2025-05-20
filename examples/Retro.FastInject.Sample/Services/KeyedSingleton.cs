using System;

namespace Retro.FastInject.Sample.Services;

public sealed class KeyedSingleton(IOptionalService? optionalService) : IKeyedSingleton, IDisposable {
  public void Dispose() {
    throw new NotImplementedException();
  }
}