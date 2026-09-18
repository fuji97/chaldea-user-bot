using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using Rayshift.Utils;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Server.DbContext;
using Server.Exceptions;
using Server.Services;
using Telegram.Bot;
using Telegram.Bot.Advanced.Core.Dispatcher.Filters;
using Telegram.Bot.Advanced.Core.Tools;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Server.TelegramController;

[ChatTypeFilter(ChatType.Private)]
public sealed class PrivateMasterController(
    ILogger<PrivateMasterController> logger,
    MasterQueryService masters,
    MasterDisplayService display,
    RayshiftSupportService rayshift,
    MasterNotificationService notifications)
    : ChaldeaController(logger) {
    [CommandFilter("list"), MessageTypeFilter(MessageType.Text)]
    public async Task ListMasters() {
        logger.LogInformation("Ricevuto comando /list in privato");
        var names = await masters.ListOwnedNamesAsync(TelegramChat!.Id, CancellationToken);
        await ReplyTextMessageAsync(
            "<b>Lista dei tuoi Master:</b>\n" + string.Join("\n", names.Select(TelegramHtml.Escape)),
            parseMode: ParseMode.Html);
    }

    [CommandFilter("remove")]
    public async Task RemoveMaster() {
        if (MessageCommand.Parameters.Count < 1) {
            await ReplyTextMessageAsync("Devi passarmi il nome del master che vuoi cancellare");
            return;
        }

        var name = MessageCommand.Parameters.JoinStrings(" ");
        var master = await masters.GetOwnedAsync(TelegramChat!.Id, name, CancellationToken);
        if (master == null) {
            await ReplyTextMessageAsync($"Nessun Master trovato con il nome {TelegramHtml.Escape(name)}");
            return;
        }

        masters.Delete(master);
        if (await SaveChangesAsync()) {
            await ReplyTextMessageAsync("Master cancellato correttamente");
        }
    }

    [CommandFilter("master"), ChatTypeFilter(ChatType.Private)]
    public async Task ShowMasterPrivate() {
        if (MessageCommand.Parameters.Count < 1) {
            logger.LogDebug("Ricevuto comando /master senza parametri");
            await ReplyTextMessageAsync("Devi passarmi il nome del master che vuoi mostrare");
            return;
        }

        var name = MessageCommand.Parameters.JoinStrings(" ");
        var master = await masters.GetOwnedWithUserAsync(Update.Message!.Chat.Id, name, CancellationToken);
        if (master == null) {
            await ReplyTextMessageAsync($"Nessun Master trovato con il nome {TelegramHtml.Escape(name)}");
            return;
        }

        await display.ShowAsync(BotData.Bot, TelegramChat!, master, CancellationToken);

        var settingsText = "Impostazioni:\n\n" + $"Rayshift: {(master.UseRayshift ? "abilitato" : "disabilitato")}";
        await ReplyTextMessageAsync(settingsText, replyMarkup: BuildSettingsKeyboard(master));
    }

    #region Aggiornamento servant list

    [CommandFilter("servant_list")]
    public async Task UpdateServantList() {
        if (MessageCommand.Parameters.Count < 1) {
            await ReplyTextMessageAsync("Devi inviarmi il nome del Master da modificare nel formato:\n/servant_list <nome>");
            return;
        }

        var name = MessageCommand.Message!;
        var master = await masters.GetOwnedAsync(TelegramChat!.Id, name, CancellationToken);
        if (master == null) {
            await ReplyTextMessageAsync($"Nessun Master trovato con il nome {TelegramHtml.Escape(name)}");
            return;
        }

        await GotoEditServantList(master);
    }

    [ChatStateFilter(ConversationState.UpdatingServantList), MessageTypeFilter(MessageType.Photo)]
    public async Task SetUpdatedServantList() {
        var master = await GetEditedMasterOrResetAsync(MasterConversation.EditServantListKey);
        if (master == null) {
            return;
        }

        if (Update.Message!.Photo is not { Length: > 0 }) {
            await ReplyTextMessageAsync("Non ho ricevuto una foto valida, riprova");
            return;
        }

        logger.LogDebug("Impostando l'immagine {FileId} come servant list del Master {Master}", Update.Message.Photo[0].FileId, master.Name);
        master.ServantList = Update.Message.Photo[0].FileId;
        TelegramChat!.State = ConversationState.Idle;
        if (await SaveChangesAsync()) {
            await ReplyTextMessageAsync("Aggiornamento della lista dei servant completato correttamente");
            await notifications.SendServantListUpdateAsync(BotData.Bot, master.Id,
                $"<i>La Servant list del Master {TelegramHtml.Escape(master.Name)} è stata aggiornata</i>", CancellationToken);
        }
    }

    [ChatStateFilter(ConversationState.UpdatingServantList), CommandFilter("skip")]
    public async Task SetUpdatedServantListEmpty() {
        var master = await GetEditedMasterOrResetAsync(MasterConversation.EditServantListKey);
        if (master == null) {
            return;
        }

        master.ServantList = null;
        logger.LogDebug("Impostato null come servant list del Master {Master}", master.Name);
        TelegramChat!.State = ConversationState.Idle;
        if (await SaveChangesAsync()) {
            await ReplyTextMessageAsync("Lista dei servant rimossa correttamente");
        }
    }

    #endregion

    #region Aggiornamento support list

    [CommandFilter("support_list")]
    public async Task UpdateSupportList() {
        if (MessageCommand.Parameters.Count < 1) {
            await ReplyTextMessageAsync("Devi inviarmi il nome del Master da modificare nel formato:\n/support_list <nome>");
            return;
        }

        var name = MessageCommand.Message!;
        var master = await masters.GetOwnedAsync(TelegramChat!.Id, name, CancellationToken);
        if (master == null) {
            await ReplyTextMessageAsync($"Nessun Master trovato con il nome {TelegramHtml.Escape(name)}");
            return;
        }

        await GotoEditSupportList(master);
    }

    [ChatStateFilter(ConversationState.UpdatingSupportList), MessageTypeFilter(MessageType.Photo)]
    public async Task SetUpdatedSupportList() {
        var master = await GetEditedMasterOrResetAsync(MasterConversation.EditSupportListKey);
        if (master == null) {
            return;
        }

        if (Update.Message!.Photo is not { Length: > 0 }) {
            await ReplyTextMessageAsync("Non ho ricevuto una foto valida, riprova");
            return;
        }

        logger.LogDebug("Impostando l'immagine {FileId} come support list del Master {Master}", Update.Message.Photo[0].FileId, master.Name);
        master.SupportList = Update.Message.Photo[0].FileId;
        master.UseRayshift = false;
        TelegramChat!.State = ConversationState.Idle;
        if (await SaveChangesAsync()) {
            await ReplyTextMessageAsync("Aggiornamento della lista dei support avvenuto correttamente");
            await notifications.SendSupportListUpdateAsync(BotData.Bot, master.Id,
                $"<i>La Support list del Master {TelegramHtml.Escape(master.Name)} è stata aggiornata</i>", CancellationToken);
        }
    }

    [ChatStateFilter(ConversationState.UpdatingSupportList), CommandFilter("rayshift")]
    public async Task SetUpdatedSupportListRayshift() {
        var master = await GetEditedMasterOrResetAsync(MasterConversation.EditSupportListKey);
        if (master == null) {
            return;
        }

        if (master.UseRayshift) {
            TelegramChat!.State = ConversationState.Idle;
            await SaveChangesAsync();
            await ReplyTextMessageAsync("Rayshift è già impostato per questo master. Se vuoi aggiornare la support list usa il comando /update <MASTER>");
            return;
        }

        var waitingMessage = await ReplyTextMessageAsync("Ok, provo ad impostare Rayshift.io come provider\nAttendere per favore...");

        try {
            var response = await rayshift.EnableAsync(master, CancellationToken);
            TelegramChat!.State = ConversationState.Idle;
            if (await SaveChangesAsync()) {
                await SendRayshiftSupportPhotoAsync(master, response, waitingMessage.MessageId);
                await ReplyTextMessageAsync("È possibile disabilitare successivamente Rayshift.io tramite il comando /support_list <MASTER> o aggiornare la lista tramite il comando /update <MASTER>\n");
                await notifications.SendSupportListUpdateAsync(BotData.Bot, master.Id,
                    $"<i>Il Master {TelegramHtml.Escape(master.Name)} ha abilitato Rayshift</i>", CancellationToken);
            }
        }
        catch (Exception e) when (e is RayshiftUnavailableException or HttpRequestException or JsonException) {
            logger.LogError(e, "Error while requesting support lookup of {FriendCode}", master.FriendCode);
            await ReplyTextMessageAsync("Errore nell'impostare Rayshift.io come provider.\n" +
                                        "Inviami lo screen dei tuoi support, /rayshift se vuoi riprovare la connessione a Rayshift.io o /skip se vuoi saltare questa fase");
        }
    }

    [ChatStateFilter(ConversationState.UpdatingSupportList), CommandFilter("skip")]
    public async Task SetUpdatedSupportListEmpty() {
        var master = await GetEditedMasterOrResetAsync(MasterConversation.EditSupportListKey);
        if (master == null) {
            return;
        }

        logger.LogDebug("Impostato null come support list del Master {Master}", master.Name);
        master.SupportList = null;
        master.UseRayshift = false;
        TelegramChat!.State = ConversationState.Idle;
        if (await SaveChangesAsync()) {
            await ReplyTextMessageAsync("Lista dei support rimossa correttamente");
            await notifications.SendSupportListUpdateAsync(BotData.Bot, master.Id,
                $"<i>La Support list del Master {TelegramHtml.Escape(master.Name)} è stata rimossa</i>", CancellationToken);
        }
    }

    [ChatStateFilter(ConversationState.Idle), CommandFilter("update")]
    public async Task UpdateRayshiftSupportList() {
        if (MessageCommand.Parameters.Count < 1) {
            await ReplyTextMessageAsync("Devi inviarmi il nome del Master da modificare nel formato:\n/update <nome>");
            return;
        }

        var name = MessageCommand.Message!;
        var master = await masters.GetOwnedAsync(TelegramChat!.Id, name, CancellationToken);
        if (master == null) {
            await ReplyTextMessageAsync($"Nessun Master trovato con il nome {TelegramHtml.Escape(name)}");
            return;
        }

        if (master.UseRayshift) {
            await UpdateRayshiftAsync(master);
        }
        else {
            await ReplyTextMessageAsync("Questo Master non ha abilitato Rayshift.\n" +
                                        $"È possibile abilitarlo tramite il comando /support_list {TelegramHtml.Escape(master.Name)}");
        }
    }

    #endregion

    #region Inline Callback

    [CallbackCommandFilter(InlineKeyboardCommands.UpdateSupportList)]
    public async Task InlineUpdateSupportList() {
        await BotData.Bot.AnswerCallbackQuery(Update.CallbackQuery!.Id, cancellationToken: CancellationToken);

        var master = await GetMasterFromCallbackAsync(masters);
        if (master == null) {
            await ReplyTextMessageAsync("Il master è errato o non è disponibile");
            return;
        }

        if (master.UseRayshift) {
            await UpdateRayshiftAsync(master);
        }
        else {
            await GotoEditSupportList(master);
        }
    }

    [CallbackCommandFilter(InlineKeyboardCommands.UpdateServantList)]
    public async Task InlineUpdateServantList() {
        await BotData.Bot.AnswerCallbackQuery(Update.CallbackQuery!.Id, cancellationToken: CancellationToken);

        var master = await GetMasterFromCallbackAsync(masters);
        if (master == null) {
            await ReplyTextMessageAsync("Il master è errato o non è disponibile");
            return;
        }

        await GotoEditServantList(master);
    }

    [CallbackCommandFilter(InlineKeyboardCommands.EnableRayshift)]
    public async Task InlineEnableRayshift() {
        await BotData.Bot.AnswerCallbackQuery(Update.CallbackQuery!.Id, cancellationToken: CancellationToken);

        var master = await GetMasterFromCallbackAsync(masters);
        if (master == null) {
            await ReplyTextMessageAsync("Il master è errato o non è disponibile");
            return;
        }

        if (master.UseRayshift) {
            await ReplyTextMessageAsync("Rayshift è già abilitato per questo Master");
            return;
        }

        var waitingMessage = await ReplyTextMessageAsync("Ok, provo ad impostare Rayshift.io come provider\nAttendere per favore...");

        try {
            var response = await rayshift.EnableAsync(master, CancellationToken);
            if (await SaveChangesAsync()) {
                await SendRayshiftSupportPhotoAsync(master, response, waitingMessage.MessageId);
                await ReplyTextMessageAsync("È possibile disabilitare successivamente Rayshift.io tramite il comando /support_list <MASTER> o aggiornare la lista tramite il comando /update <MASTER>\n");
                await notifications.SendSupportListUpdateAsync(BotData.Bot, master.Id,
                    $"<i>Il Master {TelegramHtml.Escape(master.Name)} ha abilitato Rayshift</i>", CancellationToken);
            }
        }
        catch (Exception e) when (e is RayshiftUnavailableException or HttpRequestException or JsonException) {
            logger.LogError(e, "Error while requesting support lookup of {FriendCode}", master.FriendCode);
            await ReplyTextMessageAsync("Errore nell'impostare Rayshift.io come provider.");
        }
    }

    [CallbackCommandFilter(InlineKeyboardCommands.DisableRayshift)]
    public async Task InlineDisableRayshift() {
        await BotData.Bot.AnswerCallbackQuery(Update.CallbackQuery!.Id, cancellationToken: CancellationToken);

        var master = await GetMasterFromCallbackAsync(masters);
        if (master == null) {
            await ReplyTextMessageAsync("Il master è errato o non è disponibile");
            return;
        }

        if (!master.UseRayshift) {
            await ReplyTextMessageAsync("Rayshift non è abilitato per questo Master");
            return;
        }

        master.UseRayshift = false;
        if (await SaveChangesAsync()) {
            await ReplyTextMessageAsync(
                "Rayshift è stato disabilitato.\n" +
                $"Ora sei senza support list, se vuoi impostare un immagine come support list usa il comando /support_list {TelegramHtml.Escape(master.Name)}\n");
        }
    }

    [CallbackCommandFilter(InlineKeyboardCommands.DeleteMaster)]
    public async Task InlineDeleteMaster() {
        await BotData.Bot.AnswerCallbackQuery(Update.CallbackQuery!.Id, cancellationToken: CancellationToken);

        var master = await GetMasterFromCallbackAsync(masters);
        if (master == null) {
            await ReplyTextMessageAsync("Il master è errato o non è disponibile");
            return;
        }

        masters.Delete(master);
        if (await SaveChangesAsync()) {
            await ReplyTextMessageAsync($"Il Master {TelegramHtml.Escape(master.Name)} è stato cancellato correttamente");
        }
    }

    #endregion

    private async Task SendRayshiftSupportPhotoAsync(Master master, Rayshift.Models.ApiMaster response, int waitingMessageId) {
        try {
            var supportUrl = await rayshift.GetSupportImageUrlAsync(response, RayshiftSupportService.ServerToRegion(master.Server), CancellationToken);
            await ReplyPhotoAsync(new InputFileUrl(new Uri(supportUrl)));
            await BotData.Bot.EditMessageText(TelegramChat!.Id, waitingMessageId,
                "Connessione avvenuta con successo! La seguente support list è ottenuta da Rayshift.io:", cancellationToken: CancellationToken);
        }
        catch (ApiRequestException e) {
            logger.LogError(e, "Exception thrown while sending support list");
            await BotData.Bot.EditMessageText(TelegramChat!.Id, waitingMessageId,
                "Connessione avvenuta con successo ma è fallito l'invio della support list da Rayshift.io. Questo potrebbe impedire il corretto invio delle support list. Per correggere potete provare ad aggiornare la lista manualmente da rayshift.io.",
                cancellationToken: CancellationToken);
        }
    }

    private async Task UpdateRayshiftAsync(Master master) {
        var waitingMessage = await ReplyTextMessageAsync("Aggiornamento della support list, attendere...");

        try {
            await rayshift.RefreshAsync(master, CancellationToken);
            await BotData.Bot.EditMessageText(TelegramChat!.Id, waitingMessage.MessageId, "Aggiornamento completato", cancellationToken: CancellationToken);
            await notifications.SendSupportListUpdateAsync(BotData.Bot, master.Id,
                $"<i>La Support list del Master {TelegramHtml.Escape(master.Name)} è stata aggiornata (via Rayshift)</i>", CancellationToken);
        }
        catch (Exception e) when (e is RayshiftUnavailableException or HttpRequestException or JsonException) {
            logger.LogError(e, "Error while requesting support lookup of {FriendCode}", master.FriendCode);
            await BotData.Bot.EditMessageText(TelegramChat!.Id, waitingMessage.MessageId, "Errore durante l'ottenimento della nuova support list.", cancellationToken: CancellationToken);
        }
    }

    private async Task<Master> GetEditedMasterOrResetAsync(string draftKey) {
        if (!MasterConversation.TryGetEditedMasterId(TelegramChat!, draftKey, out var masterId)) {
            TelegramChat!.State = ConversationState.Idle;
            await SaveChangesAsync();
            await ReplyTextMessageAsync("Il Master scelto non è più disponibile");
            return null;
        }

        var master = await masters.GetOwnedByIdAsync(masterId, TelegramChat!.Id, CancellationToken);
        if (master == null) {
            TelegramChat.State = ConversationState.Idle;
            await SaveChangesAsync();
            await ReplyTextMessageAsync("Il Master scelto non è più disponibile");
        }

        return master;
    }

    private InlineKeyboardMarkup BuildSettingsKeyboard(Master master) {
        var data = new Dictionary<string, string> { { "master_id", master.Id.ToString(CultureInfo.InvariantCulture) } };

        var updateSupportList = new InlineKeyboardButton {
            Text = master.UseRayshift ? "Aggiorna Support List" : "Cambia Support List",
            CallbackData = new InlineDataWrapper(InlineKeyboardCommands.UpdateSupportList, data).ToString()
        };

        var updateServantList = new InlineKeyboardButton {
            Text = "Cambia Servant List",
            CallbackData = new InlineDataWrapper(InlineKeyboardCommands.UpdateServantList, data).ToString()
        };

        var toggleRayshift = master.UseRayshift
            ? new InlineKeyboardButton {
                Text = "Disabilita Rayshift",
                CallbackData = new InlineDataWrapper(InlineKeyboardCommands.DisableRayshift, data).ToString()
            }
            : new InlineKeyboardButton {
                Text = "Abilita Rayshift",
                CallbackData = new InlineDataWrapper(InlineKeyboardCommands.EnableRayshift, data).ToString()
            };

        var deleteMaster = new InlineKeyboardButton {
            Text = "Elimina Master",
            CallbackData = new InlineDataWrapper(InlineKeyboardCommands.DeleteMaster, data).ToString()
        };

        return new InlineKeyboardMarkup(new[] {
            new[] { updateSupportList, updateServantList },
            new[] { toggleRayshift },
            new[] { deleteMaster }
        });
    }

    private async Task GotoEditSupportList(Master master) {
        TelegramChat![MasterConversation.EditSupportListKey] = master.Id.ToString(CultureInfo.InvariantCulture);
        TelegramChat.State = ConversationState.UpdatingSupportList;
        if (await SaveChangesAsync()) {
            await ReplyTextMessageAsync("Inviami la nuova foto, /rayshift se vuoi impostare Rayshift.io come provider o /skip se vuoi rimuoverla");
        }
    }

    private async Task GotoEditServantList(Master master) {
        TelegramChat![MasterConversation.EditServantListKey] = master.Id.ToString(CultureInfo.InvariantCulture);
        TelegramChat.State = ConversationState.UpdatingServantList;
        if (await SaveChangesAsync()) {
            await ReplyTextMessageAsync("Inviami la nuova foto o /skip che rimuoverla");
        }
    }

    private static class InlineKeyboardCommands {
        public const string UpdateSupportList = "UpdateSupportList";
        public const string UpdateServantList = "UpdateServantList";
        public const string DeleteMaster = "DeleteMaster";
        public const string EnableRayshift = "EnableRayshift";
        public const string DisableRayshift = "DisableRayshift";
    }
}
