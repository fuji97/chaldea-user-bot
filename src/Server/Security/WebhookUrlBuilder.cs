using System;

namespace Server.Security;

public static class WebhookUrlBuilder {
    public static Uri Build(Uri baseUri, string basePath, string endpoint) {
        var normalizedBase = baseUri.AbsoluteUri.EndsWith('/') ? baseUri.AbsoluteUri : baseUri.AbsoluteUri + "/";
        var relativePath = $"{basePath.Trim('/')}/{Uri.EscapeDataString(endpoint)}";
        return new Uri(new Uri(normalizedBase), relativePath);
    }
}
