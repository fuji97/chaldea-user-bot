using System.Linq;
using Rayshift.Utils;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Server.DbContext;
using Server.Services;
using Telegram.Bot;
using Telegram.Bot.Advanced.Core.Dispatcher.Filters;
using Telegram.Bot.Advanced.Core.Tools;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Server.TelegramController;

[ChatTypeFilter(ChatType.Group, ChatType.Supergroup)]
public sealed class GroupController(
    ILogger<GroupController> logger,
    MasterQueryService masters,
    MasterDisplayService display,
    ChatSettingsService chatSettingsService)
    : ChaldeaController(logger) {
    [CommandFilter("list"), MessageTypeFilter(MessageType.Text)]
    public async Task ListMastersInGroups() {
        logger.LogInformation("Ricevuto comando /list in un gruppo");
        var linked = await masters.ListLinkedAsync(TelegramChat!.Id, CancellationToken);
        var lines = linked.Select(m => $"{TelegramHtml.Escape(m.MasterName)} by <a href=\"tg://user?id={m.OwnerId}\">@{TelegramHtml.Escape(m.OwnerUsername)}</a>");
        await ReplyTextMessageAsync(
            "<b>Lista dei Master registrati:</b>\n" + string.Join("\n", lines),
            parseMode: ParseMode.Html,
            disableNotification: true);
    }

    [CommandFilter("link")]
    public async Task LinkMaster() {
        if (MessageCommand.Parameters.Count < 1) {
            await ReplyTextMessageAsync("Devi passarmi il nome del master che vuoi collegare");
            return;
        }

        var name = MessageCommand.Parameters.JoinStrings(" ");
        var master = await masters.GetOwnedAsync(Update.Message!.From!.Id, name, CancellationToken);
        if (master == null) {
            await ReplyTextMessageAsync($"Nessun Master trovato con il nome {TelegramHtml.Escape(name)}");
            return;
        }

        if (await masters.LinkAsync(master.Id, TelegramChat!.Id, CancellationToken)) {
            if (await SaveChangesAsync()) {
                await ReplyTextMessageAsync("Master collegato correttamente");
            }
        }
        else {
            await ReplyTextMessageAsync("Master già collegato");
        }
    }

    [CommandFilter("unlink")]
    public async Task UnlinkMaster() {
        if (MessageCommand.Parameters.Count < 1) {
            await ReplyTextMessageAsync("Devi passarmi il nome del master che vuoi scollegare");
            return;
        }

        var name = MessageCommand.Parameters.JoinStrings(" ");
        var link = await masters.GetLinkByNameAsync(TelegramChat!.Id, name, CancellationToken);
        if (link == null) {
            await ReplyTextMessageAsync($"Nessun Master collegato con il nome {TelegramHtml.Escape(name)}");
            return;
        }

        if (Update.Message!.From!.Id != link.Master.UserId && !await IsSenderAdminAsync()) {
            await ReplyTextMessageAsync("Non puoi scollegare questo utente");
            return;
        }

        masters.Unlink(link);
        if (await SaveChangesAsync()) {
            await ReplyTextMessageAsync("Master scollegato correttamente");
        }
    }

    [CommandFilter("master")]
    public async Task ShowMasterGroups() {
        if (MessageCommand.Parameters.Count < 1) {
            logger.LogDebug("Ricevuto comando /master senza parametri");
            await ReplyTextMessageAsync("Devi passarmi il nome del master che vuoi mostrare");
            return;
        }

        var name = MessageCommand.Parameters.JoinStrings(" ");
        var master = await masters.GetLinkedWithUserAsync(TelegramChat!.Id, name, CancellationToken);
        if (master == null) {
            await ReplyTextMessageAsync($"Nessun Master trovato con il nome {TelegramHtml.Escape(name)}");
            return;
        }

        await display.ShowAsync(BotData.Bot, TelegramChat, master, CancellationToken);
    }

    [CommandFilter("settings")]
    public async Task GroupSettings() {
        logger.LogInformation("Ricevuto comando /settings in un gruppo");
        if (!await IsSenderAdminAsync()) {
            return;
        }

        var settings = await chatSettingsService.GetOrCreateAsync(TelegramChat!.Id, CancellationToken);
        if (settings == null) {
            return;
        }

        await ReplyTextMessageAsync(BuildSettingsMessage(settings), replyMarkup: BuildSettingsKeyboard(settings));
    }

    #region Comandi inline

    [CallbackCommandFilter(InlineKeyboardCommands.EnableSupportListNotifications,
        InlineKeyboardCommands.DisableSupportListNotifications,
        InlineKeyboardCommands.EnableServantListNotifications,
        InlineKeyboardCommands.DisableServantListNotifications)]
    public async Task SettingsCallback() {
        await BotData.Bot.AnswerCallbackQuery(Update.CallbackQuery!.Id, cancellationToken: CancellationToken);

        var originalMessage = Update.CallbackQuery.Message!;
        if (!await IsUserAdminAsync(TelegramChat!.Id, Update.CallbackQuery.From.Id)) {
            return;
        }

        var settings = await chatSettingsService.GetOrCreateAsync(TelegramChat.Id, CancellationToken);
        if (settings == null) {
            return;
        }

        switch (InlineDataWrapper.ParseInlineData(Update.CallbackQuery.Data!).Command) {
            case InlineKeyboardCommands.EnableSupportListNotifications:
                settings.SupportListNotifications = true;
                break;
            case InlineKeyboardCommands.DisableSupportListNotifications:
                settings.SupportListNotifications = false;
                break;
            case InlineKeyboardCommands.EnableServantListNotifications:
                settings.ServantListNotifications = true;
                break;
            case InlineKeyboardCommands.DisableServantListNotifications:
                settings.ServantListNotifications = false;
                break;
        }

        if (await SaveChangesAsync()) {
            await BotData.Bot.EditMessageText(TelegramChat.Id, originalMessage.MessageId,
                BuildSettingsMessage(settings), replyMarkup: BuildSettingsKeyboard(settings), cancellationToken: CancellationToken);
        }
    }

    #endregion

    private string BuildSettingsMessage(ChatSettings settings) =>
        $"Impostazioni del gruppo {TelegramHtml.Escape(TelegramChat!.Title)}:\n\n" +
        "Notifiche aggiornamenti:\n" +
        $"Support list: {(settings.SupportListNotifications ? "abilitate" : "disabilitate")}\n" +
        $"Servant list: {(settings.ServantListNotifications ? "abilitate" : "disabilitate")}";

    private static InlineKeyboardMarkup BuildSettingsKeyboard(ChatSettings settings) {
        var supportListNotifications = new InlineKeyboardButton {
            Text = settings.SupportListNotifications ? "Disabilita notifiche support list" : "Abilita notifiche support list",
            CallbackData = new InlineDataWrapper(
                settings.SupportListNotifications ? InlineKeyboardCommands.DisableSupportListNotifications : InlineKeyboardCommands.EnableSupportListNotifications
            ).ToString()
        };

        var servantListNotifications = new InlineKeyboardButton {
            Text = settings.ServantListNotifications ? "Disabilita notifiche servant list" : "Abilita notifiche servant list",
            CallbackData = new InlineDataWrapper(
                settings.ServantListNotifications ? InlineKeyboardCommands.DisableServantListNotifications : InlineKeyboardCommands.EnableServantListNotifications
            ).ToString()
        };

        return new InlineKeyboardMarkup(new[] { supportListNotifications, servantListNotifications });
    }

    private static class InlineKeyboardCommands {
        public const string EnableSupportListNotifications = "EnableSupportListNotifications";
        public const string DisableSupportListNotifications = "DisableSupportListNotifications";
        public const string EnableServantListNotifications = "EnableServantListNotifications";
        public const string DisableServantListNotifications = "DisableServantListNotifications";
    }
}
