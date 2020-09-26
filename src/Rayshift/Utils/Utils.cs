using System;

namespace Rayshift.Utils {
    public static class Utils {
        public static DateTime DateTimeFromTimestamp(long timestamp) {
            var date = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return date.Add(TimeSpan.FromSeconds(timestamp));
        }
    }
}