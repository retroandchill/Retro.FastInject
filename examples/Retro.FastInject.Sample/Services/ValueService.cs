
using System.Collections.Generic;

namespace Retro.FastInject.Sample.Services;

public struct ValueService(IEnumerable<IKeyedSingleton> keyedSingletons) {
  
}