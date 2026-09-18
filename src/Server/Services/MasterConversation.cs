using System.Globalization;
using System.Text.RegularExpressions;
using Server.DbContext;
using Telegram.Bot.Advanced.DbContexts;

namespace Server.Services;

public static partial class MasterConversation {
    public const string NameKey = "nome";
    public const string FriendCodeKey = "friend_code";
    public const string ServerKey = "server";
    public const string SupportPhotoKey = "support_photo";
    public const string ServantPhotoKey = "servant_photo";
    public const string UseRayshiftKey = "use_rayshift";
    public const string EditSupportListKey = "edit_support_list";
    public const string EditServantListKey = "edit_servant_list";

    public static bool IsValidFriendCode(string value) => value != null && FriendCodeRegex().IsMatch(value);

    public static bool IsValidMasterName(string value) {
        if (value == null) {
            return false;
        }

        var trimmed = value.Trim();
        return trimmed.Length is > 0 and <= 64;
    }

    public static bool TryGetServer(TelegramChat chat, out MasterServer server) {
        server = default;
        var raw = chat[ServerKey];
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            || !System.Enum.IsDefined(typeof(MasterServer), value)) {
            return false;
        }

        server = (MasterServer) value;
        return true;
    }

    public static bool TryGetEditedMasterId(TelegramChat chat, string key, out int masterId) =>
        int.TryParse(chat[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out masterId);

    [GeneratedRegex("^[0-9]{9}$")]
    private static partial Regex FriendCodeRegex();
}
