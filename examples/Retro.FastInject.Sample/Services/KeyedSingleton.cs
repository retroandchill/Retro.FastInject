using System;

namespace Retro.FastInject.Sample.Services;

public sealed class KeyedSingleton : IKeyedSingleton, IDisposable {
  public void Dispose() {
    throw new NotImplementedException();
  }
}