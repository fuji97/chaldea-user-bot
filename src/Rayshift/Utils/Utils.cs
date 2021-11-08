using System;
using Flurl;
using Rayshift.Models;

namespace Rayshift.Utils {
    public static class Utils {
        public static DateTime DateTimeFromTimestamp(long timestamp) {
            var date = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return date.Add(TimeSpan.FromSeconds(timestamp));
        }

        public static string StringRegion(Region region) {
            switch (region) {
                case Region.Jp:
                    return "jp";
                case Region.Na:
                    return "na";
            }

            return "";
        }

        public static string BuildImageUrl(Region region, string friendId, string guid, int decksToStack, int flags) {
            var url = Url.Combine(
                RayshiftClient.BaseAddress,
                RayshiftClient.ImagesPath,
                StringRegion(region),
                friendId,
                guid,
                decksToStack.ToString(),
                flags.ToString());
            return url + ".png";
        }
    }
}