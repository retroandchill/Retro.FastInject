
using System.Collections.Generic;

namespace Retro.FastInject.Sample.Services;

public struct ValueService(ITransientService transientService, IEnumerable<IKeyedSingleton> keyedSingletons) {
  
}