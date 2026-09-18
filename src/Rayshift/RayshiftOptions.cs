using System;

namespace Rayshift;

public sealed class RayshiftOptions {
    public string? ApiKey { get; init; }
    public int MaxLookupRequests { get; init; } = 15;
    public TimeSpan RequestsInterval { get; init; } = TimeSpan.FromSeconds(2);
}
