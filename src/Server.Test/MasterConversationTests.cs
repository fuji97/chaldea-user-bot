using NUnit.Framework;
using Server.DbContext;
using Server.Services;
using Telegram.Bot.Advanced.DbContexts;

namespace Server.Test;

[TestFixture]
public class MasterConversationTests {
    [TestCase("123456789", true)]
    [TestCase("12345", false)]
    [TestCase("1234567890", false)]
    [TestCase("123456789a", false)]
    [TestCase(" 123456789", false)]
    [TestCase(null, false)]
    public void IsValidFriendCode_ValidatesNineDigitCodesOnly(string? value, bool expected) {
        Assert.That(MasterConversation.IsValidFriendCode(value!), Is.EqualTo(expected));
    }

    [Test]
    public void TryGetServer_ValidDraft_ReturnsTrueAndParsedServer() {
        var chat = new TelegramChat(1);
        chat[MasterConversation.ServerKey] = ((int) MasterServer.Jp).ToString();

        var result = MasterConversation.TryGetServer(chat, out var server);

        Assert.That(result, Is.True);
        Assert.That(server, Is.EqualTo(MasterServer.Jp));
    }

    [Test]
    public void TryGetServer_MissingDraft_ReturnsFalse() {
        var chat = new TelegramChat(1);

        var result = MasterConversation.TryGetServer(chat, out _);

        Assert.That(result, Is.False);
    }

    [Test]
    public void TryGetServer_NonNumericDraft_ReturnsFalse() {
        var chat = new TelegramChat(1);
        chat[MasterConversation.ServerKey] = "not-a-number";

        var result = MasterConversation.TryGetServer(chat, out _);

        Assert.That(result, Is.False);
    }

    [Test]
    public void TryGetServer_OutOfRangeDraft_ReturnsFalse() {
        var chat = new TelegramChat(1);
        chat[MasterConversation.ServerKey] = "42";

        var result = MasterConversation.TryGetServer(chat, out _);

        Assert.That(result, Is.False);
    }

    [Test]
    public void TryGetEditedMasterId_ValidDraft_ReturnsTrueAndId() {
        var chat = new TelegramChat(1);
        chat[MasterConversation.EditSupportListKey] = "42";

        var result = MasterConversation.TryGetEditedMasterId(chat, MasterConversation.EditSupportListKey, out var id);

        Assert.That(result, Is.True);
        Assert.That(id, Is.EqualTo(42));
    }

    [Test]
    public void TryGetEditedMasterId_MissingDraft_ReturnsFalse() {
        var chat = new TelegramChat(1);

        var result = MasterConversation.TryGetEditedMasterId(chat, MasterConversation.EditSupportListKey, out _);

        Assert.That(result, Is.False);
    }

    [Test]
    public void TryGetEditedMasterId_NonNumericDraft_ReturnsFalse() {
        var chat = new TelegramChat(1);
        chat[MasterConversation.EditSupportListKey] = "abc";

        var result = MasterConversation.TryGetEditedMasterId(chat, MasterConversation.EditSupportListKey, out _);

        Assert.That(result, Is.False);
    }
}
