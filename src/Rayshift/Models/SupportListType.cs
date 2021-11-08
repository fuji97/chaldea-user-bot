using System;

namespace Rayshift.Models {
    [Flags]
    public enum SupportLists : int {
        None = 0,
        Normal1 = 1,
        Normal2 = 2,
        Normal3 = 4,
        Event1 = 8,
        Event2 = 16,
        Event3 = 32
    }
}