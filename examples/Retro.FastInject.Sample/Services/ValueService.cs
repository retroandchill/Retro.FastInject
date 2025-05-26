
using System;
using System.Collections.Generic;

namespace Retro.FastInject.Sample.Services;

public struct ValueService(Lazy<ITransientService> transientService, IEnumerable<IKeyedSingleton> keyedSingletons) {
  
}