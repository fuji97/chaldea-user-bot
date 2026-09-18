using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Server.DbContext;
using Server.Exceptions;
using Server.Services;
using Telegram.Bot;
using Telegram.Bot.Advanced.Core.Dispatcher.Filters;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Server.TelegramController;

[ChatTypeFilter(ChatType.Private)]
public sealed class PrivateRegistrationController(
    ILogger<PrivateRegistrationController> logger,
    MasterQueryService masters,
    RayshiftSupportService rayshift)
    : ChaldeaController(logger) {
    [CommandFilter("add"), MessageTypeFilter(MessageType.Text)]
    public async Task Add() {
        logger.LogInformation("Ricevuto comando /add");
        if (MessageCommand.Parameters.Count < 1) {
            TelegramChat!.State = ConversationState.Nome;
            if (await SaveChangesAsync()) {
                await ReplyTextMessageAsync("Ok, inviami il nome che vuoi usare");
            }
        }
        else {
            await SetMasterNameAsync(MessageCommand.Message!);
        }
    }

    [ChatStateFilter(ConversationState.Nome), NoCommandFilter, MessageTypeFilter(MessageType.Text)]
    public async Task GetNome() {
        logger.LogInformation("Nome ricevuto da @{Username}: {Text}", TelegramChat?.Username, MessageCommand.Text);
        if (TelegramChat != null) {
            await SetMasterNameAsync(MessageCommand.Text!);
        }
    }

    private async Task SetMasterNameAsync(string rawName) {
        var name = rawName.Trim();
        if (await CheckNameAsync(name)) {
            TelegramChat!.State = ConversationState.FriendCode;
            TelegramChat[MasterConversation.NameKey] = name;
            if (await SaveChangesAsync()) {
                await ReplyTextMessageAsync("Ok, inviami il friend code in formato 123456789");
            }
        }
        else {
            await ReplyTextMessageAsync("Nome invalido o già in uso, sceglierne un altro");
        }
    }

    private async Task<bool> CheckNameAsync(string name) =>
        MasterConversation.IsValidMasterName(name) && !await masters.NameTakenByOwnerAsync(TelegramChat!.Id, name, CancellationToken);

    [ChatStateFilter(ConversationState.FriendCode), NoCommandFilter, MessageTypeFilter(MessageType.Text)]
    public async Task GetFriendCode() {
        logger.LogInformation("Ricevuto friend code: {FriendCode}", Update.Message!.Text);
        if (MasterConversation.IsValidFriendCode(Update.Message.Text)) {
            TelegramChat![MasterConversation.FriendCodeKey] = Update.Message.Text;
            TelegramChat.State = ConversationState.Server;
            if (await SaveChangesAsync()) {
                await ReplyTextMessageAsync(
                    $"E' stato impostato come friend code: '{TelegramHtml.Escape(Update.Message.Text)}'. Sarà possibile cambiarlo in seguito.\n" +
                    "Ora inviami il server di appartenenza del Master, 'JP' o 'US' (puoi usare la tastiera automatica)",
                    replyMarkup: new ReplyKeyboardMarkup {
                        Keyboard = [[new KeyboardButton("JP")], [new KeyboardButton("US")]]
                    });
            }
        }
        else {
            await ReplyTextMessageAsync("Il friend code non è valido, deve avere il seguente formato 123456789");
        }
    }

    [ChatStateFilter(ConversationState.Server), NoCommandFilter, MessageTypeFilter(MessageType.Text)]
    public async Task GetServer() {
        logger.LogInformation("Ricevuto server {Server}", Update.Message!.Text);
        switch (Update.Message.Text) {
            case "JP":
                TelegramChat![MasterConversation.ServerKey] = ((int) MasterServer.Jp).ToString();
                TelegramChat.State = ConversationState.SupportList;
                if (await SaveChangesAsync()) {
                    await ReplyTextMessageAsync(
                        "Server giapponese impostato, inviami lo screen dei tuoi support, /rayshift se vuoi ottenere automaticamente la support list da Rayshift.io o /skip se vuoi saltare questa fase",
                        replyMarkup: new ReplyKeyboardRemove());
                }

                break;
            case "US":
                TelegramChat![MasterConversation.ServerKey] = ((int) MasterServer.Na).ToString();
                TelegramChat.State = ConversationState.SupportList;
                if (await SaveChangesAsync()) {
                    await ReplyTextMessageAsync(
                        "Server americano impostato, inviami lo screen dei tuoi support, /rayshift se vuoi ottenere automaticamente la support list da Rayshift.io o /skip se vuoi saltare questa fase",
                        replyMarkup: new ReplyKeyboardRemove());
                }

                break;
            default:
                await ReplyTextMessageAsync("Server non valido, specificare 'JP' o 'US'");
                break;
        }
    }

    [ChatStateFilter(ConversationState.SupportList), CommandFilter("rayshift"), MessageTypeFilter(MessageType.Text)]
    public async Task SetupRayshift() {
        var friendCode = TelegramChat![MasterConversation.FriendCodeKey];
        if (!MasterConversation.TryGetServer(TelegramChat, out var server) || !MasterConversation.IsValidFriendCode(friendCode)) {
            await ResetInvalidDraftAsync();
            return;
        }

        await ReplyTextMessageAsync("Ok, provo ad impostare Rayshift.io come provider\nAttendere per favore...");
        var region = Server.Services.RayshiftSupportService.ServerToRegion(server);

        try {
            var response = await rayshift.RequestSupportListAsync(region, friendCode!, CancellationToken);
            var message = await ReplyTextMessageAsync(
                "Connessione avvenuta con successo!\nStiamo caricando la support list da Rayshift, attendere prego...");
            try {
                var supportListUrl = await rayshift.GetSupportImageUrlAsync(response, region, CancellationToken);
                try {
                    await ReplyPhotoAsync(new InputFileUrl(new Uri(supportListUrl)));
                    await BotData.Bot.EditMessageText(TelegramChat.Id, message.MessageId,
                        "Connessione avvenuta con successo! La seguente support list è ottenuta da Rayshift.io:",
                        cancellationToken: CancellationToken);
                }
                catch (ApiRequestException e) {
                    logger.LogError(e, "Exception thrown while sending support list");
                    await BotData.Bot.EditMessageText(TelegramChat.Id, message.MessageId,
                        "Connessione avvenuta con successo ma è fallito l'invio della support list da Rayshift.io. Questo potrebbe impedire il corretto invio delle support list. Per correggere potete provare ad aggiornare la lista manualmente da rayshift.io.",
                        cancellationToken: CancellationToken);
                }

                TelegramChat[MasterConversation.SupportPhotoKey] = null;
                TelegramChat[MasterConversation.UseRayshiftKey] = "true";
                TelegramChat.State = ConversationState.ServantList;

                if (await SaveChangesAsync()) {
                    await ReplyTextMessageAsync(
                        "È possibile disabilitare successivamente Rayshift.io tramite il comando /support_list <MASTER> o aggiornare la lista tramite il comando /update <MASTER>\n" +
                        "Ora inviami lo screen della lista dei tuoi servant o /skip se vuoi saltare questa fase");
                }
            }
            catch (ApiRequestException e) {
                logger.LogError(e, "Exception thrown while sending data to Telegram");
                throw;
            }
        }
        catch (Exception e) when (e is RayshiftUnavailableException or HttpRequestException or JsonException) {
            logger.LogError(e, "Error while requesting support lookup of {FriendCode}", friendCode);
            await ReplyTextMessageAsync("Errore nell'impostare Rayshift.io come provider.\n" +
                                        "Inviami lo screen dei tuoi support, /rayshift se vuoi riprovare la connessione a Rayshift.io o /skip se vuoi saltare questa fase");
        }
    }

    [ChatStateFilter(ConversationState.SupportList), CommandFilter("skip"), MessageTypeFilter(MessageType.Text)]
    public async Task SkipSupportList() {
        TelegramChat![MasterConversation.SupportPhotoKey] = null;
        TelegramChat.State = ConversationState.ServantList;
        if (await SaveChangesAsync()) {
            await ReplyTextMessageAsync("Hai saltato l'assegnazione della support list\nOra inviami lo screen della lista dei tuoi servant o /skip se vuoi saltare questa fase");
        }
    }

    [ChatStateFilter(ConversationState.SupportList), NoCommandFilter, MessageTypeFilter(MessageType.Photo)]
    public async Task SupportList() {
        if (Update.Message!.Photo is not { Length: > 0 }) {
            await ReplyTextMessageAsync("Non ho ricevuto una foto valida, riprova");
            return;
        }

        logger.LogInformation("Ricevuta foto");
        TelegramChat![MasterConversation.SupportPhotoKey] = Update.Message.Photo[0].FileId;
        TelegramChat.State = ConversationState.ServantList;
        if (await SaveChangesAsync()) {
            await ReplyTextMessageAsync("Ok, ora inviami lo screen della lista dei tuoi servant o /skip se vuoi saltare questa fase");
        }
    }

    [ChatStateFilter(ConversationState.ServantList), NoCommandFilter, MessageTypeFilter(MessageType.Photo)]
    public async Task ServantList() {
        if (Update.Message!.Photo is not { Length: > 0 }) {
            await ReplyTextMessageAsync("Non ho ricevuto una foto valida, riprova");
            return;
        }

        logger.LogInformation("Ricevuta foto, creazione Master e inserimento");
        TelegramChat![MasterConversation.ServantPhotoKey] = Update.Message.Photo[0].FileId;

        var master = await CreateMasterAsync();
        if (master != null) {
            await ReplyTextMessageAsync(
                "Ok, Master creato\n" +
                $"Ora lo puoi collegare alle varie chat con il comando /link {TelegramHtml.Escape(master.Name)}");
        }
    }

    [ChatStateFilter(ConversationState.ServantList), CommandFilter("skip"), MessageTypeFilter(MessageType.Text)]
    public async Task SkipServantList() {
        TelegramChat![MasterConversation.ServantPhotoKey] = null;

        var master = await CreateMasterAsync();
        if (master != null) {
            await ReplyTextMessageAsync(
                "Hai saltato l'assegnazione della servant list\n" +
                "Ok, Master creato\n" +
                $"Ora lo puoi collegare alle varie chat con il comando /link {TelegramHtml.Escape(master.Name)}");
        }
    }

    private async Task<Master> CreateMasterAsync() {
        var name = TelegramChat![MasterConversation.NameKey];
        var friendCode = TelegramChat[MasterConversation.FriendCodeKey];
        if (!MasterConversation.TryGetServer(TelegramChat, out var server)
            || string.IsNullOrEmpty(name)
            || !MasterConversation.IsValidFriendCode(friendCode)) {
            await ResetInvalidDraftAsync();
            return null;
        }

        var master = new Master(TelegramChat, name, friendCode!, server,
            TelegramChat[MasterConversation.SupportPhotoKey], TelegramChat[MasterConversation.ServantPhotoKey],
            TelegramChat[MasterConversation.UseRayshiftKey] == "true");
        TelegramContext.Add(master);
        TelegramChat.State = ConversationState.Idle;
        TelegramChat.Data.Clear();

        return await SaveChangesAsync() ? master : null;
    }

    private async Task ResetInvalidDraftAsync() {
        TelegramChat!.State = ConversationState.Idle;
        TelegramChat.Data.Clear();
        await SaveChangesAsync();
        await ReplyTextMessageAsync("I dati della registrazione non sono più validi, ricomincia con /add");
    }
}
