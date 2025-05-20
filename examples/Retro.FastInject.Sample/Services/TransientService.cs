using System;
using System.Threading.Tasks;

namespace Retro.FastInject.Sample.Services;

public sealed class TransientService(ValueService? valueService) : ITransientService, IDisposable, IAsyncDisposable {
  public void Dispose() {
    throw new NotImplementedException();
  }

  public ValueTask DisposeAsync() {
    throw new NotImplementedException();
  }
}