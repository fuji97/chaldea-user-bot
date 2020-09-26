using System.Collections.Generic;

namespace Rayshift.Utils {
    public static class StringListExtensions {
        public static string JoinStrings(this IEnumerable<string> strings, string separator) {
            return string.Join(separator, strings);
        }
    }
}