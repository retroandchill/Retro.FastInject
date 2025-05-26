using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Retro.FastInject.Sample.Services;

public sealed class TransientService(ValueService? valueService, [FromKeyedServices("other")] IKeyedSingleton keyedSingletons) : ITransientService, IDisposable, IAsyncDisposable {
  public void Dispose() {
    // Do nothing
  }

  public ValueTask DisposeAsync() {
    return default;
  }
}