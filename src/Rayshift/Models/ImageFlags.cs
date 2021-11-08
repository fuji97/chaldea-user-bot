using System;

namespace Rayshift.Models {
    [Flags]
    public enum ImageFlags : int {
        None = 0,
        Transparent = 1,
        TemplateOnly = 2,
        DisableCustomImages = 4,
        HighResolution = 8,
        NoCommandCodes = 16
    }
}