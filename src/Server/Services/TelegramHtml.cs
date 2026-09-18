namespace Server.Services;

public static class TelegramHtml {
    public static string Escape(string value) {
        if (string.IsNullOrEmpty(value)) {
            return string.Empty;
        }

        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}
