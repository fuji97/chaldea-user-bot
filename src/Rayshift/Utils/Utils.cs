using System;
using Flurl;
using Rayshift.Models;

namespace Rayshift.Utils;

public static class Utils {
    public static string StringRegion(Region region) {
        switch (region) {
            case Region.Jp:
                return "jp";
            case Region.Na:
                return "na";
            default:
                throw new ArgumentOutOfRangeException(nameof(region), region, "Unsupported Rayshift region.");
        }
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
