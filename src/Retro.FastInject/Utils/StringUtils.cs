using System.Collections.Generic;
namespace Retro.FastInject.Utils;

public static class StringUtils {

  public static string Joining(this IEnumerable<string> strings, string separator) {
    return string.Join(separator, strings);
  }
  
  
}