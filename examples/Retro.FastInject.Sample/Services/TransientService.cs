using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Retro.FastInject.Sample.Services;

public sealed class TransientService(ValueService? valueService, [FromKeyedServices("other")] IKeyedSingleton keyedSingletons) : ITransientService, IDisposable, IAsyncDisposable {
  public void Dispose() {
    throw new NotImplementedException();
  }

  public ValueTask DisposeAsync() {
    throw new NotImplementedException();
  }
}