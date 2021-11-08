using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Rayshift;
using Rayshift.Models;
using Rayshift.Utils;
using Server.DbContext;
using Telegram.Bot.Advanced.Core.Dispatcher.Filters;
using Telegram.Bot.Advanced.Core.Tools;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace Server.TelegramController {
    [ChatTypeFilter(ChatType.Private)]
    public class PrivateController : Controller {
        private readonly ILogger<InlineController> _logger;
        private readonly IRayshiftClient _rayshiftClient;

        public PrivateController(IMemoryCache cache, IConfiguration configuration, ILogger<InlineController> logger,
            IRayshiftClient rayshiftClient) : base(logger, cache, configuration) {
            _logger = logger;
            _rayshiftClient = rayshiftClient;
        }
        
        [CommandFilter("list"), MessageTypeFilter(MessageType.Text)]
        public async Task ListMasters() {
            _logger.LogInformation("Ricevuto comando /list in privato");
            var masters = TelegramContext.Masters.Where(m => m.UserId == TelegramChat.Id).Select(m => m.Name);
            await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                "<b>Lista dei tuoi Master:</b>\n" +
                string.Join("\n", masters),
                ParseMode.Html);
        }

        #region Creazione Master

        [CommandFilter("add"), MessageTypeFilter(MessageType.Text)]
        public async Task Add() {
            _logger.LogInformation("Ricevuto comando /add");
            if (MessageCommand.Parameters.Count < 1) {
                TelegramChat.State = ConversationState.Nome;
                if (await SaveChanges()) {
                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Ok, inviami il nome che vuoi usare");
                }
            }
            else {
                await SetMasterName(MessageCommand.Message);
            }
        }

        [ChatStateFilter(ConversationState.Nome), NoCommandFilter, MessageTypeFilter(MessageType.Text)]
        public async Task GetNome() {
            _logger.LogInformation($"Nome ricevuto da @{TelegramChat?.Username}: {MessageCommand.Text}");
            if (TelegramChat != null) {
                await SetMasterName(MessageCommand.Text);
            }
        }

        private async Task SetMasterName(string name) {
            if (CheckName(name)) {
                TelegramChat.State = ConversationState.FriendCode;
                TelegramChat["nome"] = name;
                if (await SaveChanges()) {
                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                        "Ok, inviami il friend code in formato 123456789");
                }
            }
            else {
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                    "Nome invalido o già in uso, sceglierne un altro");
            }
        }

        private bool CheckName(string messageText) {
            if (string.IsNullOrEmpty(messageText))
                return false;
            return !TelegramContext.Masters.Any(m => m.Name == messageText);
        }

        [ChatStateFilter(ConversationState.FriendCode), NoCommandFilter, MessageTypeFilter(MessageType.Text)]
        public async Task GetFriendCode() {
            _logger.LogInformation($"Ricevuto friend code: {Update.Message.Text}");
            if (Regex.IsMatch(Update.Message.Text, @"^\d{5}")) {
                TelegramChat["friend_code"] = Update.Message.Text;
                TelegramChat.State = ConversationState.Server;
                if (await SaveChanges()) {
                    await BotData.Bot.SendTextMessageAsync(
                        TelegramChat.Id,
                        $"E' stato impostato come friend code: '{Update.Message.Text}'. Sarà possibile cambiarlo in seguito.\n" +
                        $"Ora inviami il server di appartenenza del Master, 'JP' o 'US' (puoi usare la tastiera automatica)",
                        replyMarkup: new ReplyKeyboardMarkup() {
                            Keyboard = new[] {
                                new[] {new KeyboardButton ("JP")},
                                new[] {new KeyboardButton ("US")}
                            }
                        });
                }
            }
            else
            {
                await BotData.Bot.SendTextMessageAsync(
                    TelegramChat.Id,
                    "Il friend code non è valido, deve avere il seguente formato 123456789");
            }
        }

        [ChatStateFilter(ConversationState.Server), NoCommandFilter, MessageTypeFilter(MessageType.Text)]
        public async Task GetServer()
        {
            _logger.LogInformation($"Ricevuto server {Update.Message.Text}");
            switch (Update.Message.Text) {
                case "JP":
                    TelegramChat["server"] = ((int) MasterServer.Jp).ToString();
                    TelegramChat.State = ConversationState.SupportList;
                    if (await SaveChanges()) {
                        await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Server giapponese impostato, inviami lo screen dei tuoi support, /rayshift se vuoi ottenere automaticamente la support list da Rayshift.io o /skip se vuoi saltare questa fase",
                            replyMarkup: new ReplyKeyboardRemove());
                    }
 
                    break;
                case "US":
                    TelegramChat["server"] = ((int)MasterServer.Na).ToString();
                    TelegramChat.State = ConversationState.SupportList;
                    if (await SaveChanges()) {
                        await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Server americano impostato, inviami lo screen dei tuoi support, /rayshift se vuoi ottenere automaticamente la support list da Rayshift.io o /skip se vuoi saltare questa fase",
                            replyMarkup: new ReplyKeyboardRemove());
                    }
                    break;
                default:
                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Server non valido, specificare 'JP' o 'US'");
                    break;
            }
        }

        [ChatStateFilter(ConversationState.SupportList), CommandFilter("rayshift"), MessageTypeFilter(MessageType.Text)]
        public async Task SetupRayshift() {
            await ReplyTextMessageAsync("Ok, provo ad impostare Rayshift.io come provider\nAttendere per favore...");

            Region region = ServerToRegion((MasterServer) Int32.Parse(TelegramChat["server"]));

            using (var client = new RayshiftClient(Configuration["Rayshift:ApiKey"])) {
                try {
                    var result = await client.RequestSupportLookupAsync(region, TelegramChat["friend_code"]);
                    
                    if (result?.Response != null && result.Status == 200) {
                        TelegramChat["support_photo"] = null;
                        TelegramChat["use_rayshift"] = "true";
                        TelegramChat.State = ConversationState.ServantList;
                        if (await SaveChanges()) {
                            await ReplyTextMessageAsync(
                                "Connessione avvenuta con successo! La seguente support list è ottenuta da Rayshift.io:");
                            try {
                                await ReplyPhotoAsync(
                                    new InputOnlineFile(new Uri(result.Response.SupportList(region))));
                            } catch (ApiRequestException e) {
                                Console.WriteLine(result.Response.SupportList(region));
                                throw;
                            }
                            await ReplyTextMessageAsync(
                                "È possibile disabilitare successivamente Rayshift.io tramite il comando /support_list <MASTER> o aggiornare la lista tramite il comando /update <MASTER>\n" +
                                "Ora inviami lo screen della lista dei tuoi servant o /skip se vuoi saltare questa fase. Ora inviami lo screen della lista dei tuoi servant o /skip se vuoi saltare questa fase");
                        }
                    } else {
                        await ReplyTextMessageAsync("Errore nell'impostare Rayshift.io come provider.\n" +
                                                    "Inviami lo screen dei tuoi support, /rayshift se vuoi riprovare la connessione a Rayshift.io o /skip se vuoi saltare questa fase");
                    }
                } catch (Exception e) {
                    await ReplyTextMessageAsync("Errore nell'impostare Rayshift.io come provider.\n" +
                                                "Inviami lo screen dei tuoi support, /rayshift se vuoi riprovare la connessione a Rayshift.io o /skip se vuoi saltare questa fase");
                    _logger.LogError(e, "Error while requesting support lookup of {FriendCode}", TelegramChat["friend_code"]);
                }
                
                
            }
        }

        [ChatStateFilter(ConversationState.SupportList), CommandFilter("skip"), MessageTypeFilter(MessageType.Text)]
        public async Task SkipSupportList() {
            TelegramChat["support_photo"] = null;
            TelegramChat.State = ConversationState.ServantList;
            if (await SaveChanges()) {
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Hai saltato l'assegnazione della support list\nOra inviami lo screen della lista dei tuoi servant o /skip se vuoi saltare questa fase");
            }
        }

        [ChatStateFilter(ConversationState.SupportList), NoCommandFilter, MessageTypeFilter(MessageType.Photo)]
        public async Task SupportList()
        {
            _logger.LogInformation("Ricevuta foto");
            TelegramChat["support_photo"] = Update.Message.Photo[0].FileId;
            TelegramChat.State = ConversationState.ServantList;
            if (await SaveChanges()) {
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, $"Ok, ora inviami lo screen della lista dei tuoi servant o /skip se vuoi saltare questa fase");
            }
        }
        
        [ChatStateFilter(ConversationState.ServantList), NoCommandFilter, MessageTypeFilter(MessageType.Photo)]
        public async Task ServantList() {
            _logger.LogInformation("Ricevuta foto, creazione Master e inserimento");
            TelegramChat["servant_photo"] = Update.Message.Photo[0].FileId;
            
            var master = await CreateMaster();
            if (master != null) {
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                    $"Ok, Master creato\n" +
                    $"Ora lo puoi collegare alle varie chat con il comando /link {master.Name}");
            }
        }

        private async Task<Master> CreateMaster() {
            var master = new Master(TelegramChat, TelegramChat["nome"], TelegramChat["friend_code"],
                (MasterServer) Int32.Parse(TelegramChat["server"]), TelegramChat["support_photo"], TelegramChat["servant_photo"]
                , TelegramChat["use_rayshift"] == "true");
            TelegramContext.Add(master);
            TelegramChat.State = ConversationState.Idle;
            TelegramChat.Data.Clear();
            if (await SaveChanges()) {
                return master;
            }

            return null;
        }

        [ChatStateFilter(ConversationState.ServantList), CommandFilter("skip"), MessageTypeFilter(MessageType.Text)]
        public async Task SkipServantList() {
            TelegramChat["servant_photo"] = null;

            var master = await CreateMaster();
            if (master != null) {
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, 
                    "Hai saltato l'assegnazione della servant list\n" +
                    "Ok, Master creato\n" +
                    "Ora lo puoi collegare alle varie chat con il comando /link " + master.Name);
            }
        }
        
        #endregion

        [CommandFilter("remove")]
        public async Task RemoveMaster() {
            if (MessageCommand.Parameters.Count < 1) {
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                    "Devi passarmi il nome del master che vuoi cancellare");
            }
            else {
                var master = TelegramContext.Masters.FirstOrDefault(m =>
                    m.UserId == TelegramChat.Id && m.Name == MessageCommand.Parameters.JoinStrings(" "));

                if (master == null) {
                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                        "Nessun Master trovato con il nome " + MessageCommand.Parameters.JoinStrings(" "));
                }
                else {
                    foreach (var chat in TelegramContext.RegisteredChats.Where(c => c.MasterId == master.Id)) {
                        TelegramContext.RegisteredChats.Remove(chat);
                    }

                    TelegramContext.Masters.Remove(master);
                    if (await SaveChanges()) {
                        await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                            "Master cancellato correttamente");                    
                    }
                }
            }
        }
        
        [CommandFilter("master"), ChatTypeFilter(ChatType.Private)]
        public async Task ShowMasterPrivate() {
            if (MessageCommand.Parameters.Count < 1) {
                _logger.LogDebug("Ricevuto comando /master senza parametri");
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                    "Devi passarmi il nome del master che vuoi mostrare");
            }
            else {
                var master = TelegramContext.Masters
                    .Include(m => m.User)
                    .SingleOrDefault(m => m.Name == MessageCommand.Parameters.JoinStrings(" ") && 
                                          m.UserId == Update.Message.Chat.Id);
                if (master == null) {
                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                        "Nessun Master trovato con il nome " + MessageCommand.Parameters.JoinStrings(" "));
                }
                else {
                    await SendMaster(master);

                    var settingsText = "Impostazioni:\n\n" +
                                       $"Rayshift: {(master.UseRayshift ? "abilitato" : "disabilitato")}";
                    var settingsKeyboard = BuildSettingsKeyboard(master);
                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, settingsText,
                        replyMarkup: settingsKeyboard);
                }
            }
        }

        #region Aggiornamento servant list

        [CommandFilter("servant_list")]
        public async Task UpdateServantList() {
            if (MessageCommand.Parameters.Count < 1) {
                await ReplyTextMessageAsync(
                    "Devi inviarmi il nome del Master da modificare nel formato:\n/servant_list <nome>");
            }
            else {
                var master = await TelegramContext.Masters.FirstOrDefaultAsync(m => m.UserId == TelegramChat.Id && m.Name == MessageCommand.Message);
                if (master == null) {
                    await ReplyTextMessageAsync($"Nessun Master trovato con il nome {MessageCommand.Message}");
                }
                else {
                    await GotoEditServantList(master);
                }
            }
        }

        [ChatStateFilter(ConversationState.UpdatingServantList), MessageTypeFilter(MessageType.Photo)]
        public async Task SetUpdatedServantList() {
            var master = await TelegramContext.Masters
                .Include(m => m.RegisteredChats)
                .FirstOrDefaultAsync(m => m.Id == int.Parse(TelegramChat["edit_servant_list"]));

            if (master == null) {
                await ReplyTextMessageAsync("Il Master scelto non è più disponibile");
                TelegramChat.State = ConversationState.Idle;
                await SaveChanges();
                return;
            }

            _logger.LogDebug($"Impostando l'immagine {Update.Message.Photo[0].FileId} come servant list del Master {master.Name}");
            master.ServantList = Update.Message.Photo[0].FileId;
            TelegramChat.State = ConversationState.Idle;
            if (await SaveChanges()) {
                await ReplyTextMessageAsync("Aggiornamento della lista dei servant completato correttamente");
                await SendServantListUpdateNotifications(master, 
                    $"<i>La Servant list del Master {master.Name} è stata aggiornata</i>");
            }
        }

        [ChatStateFilter(ConversationState.UpdatingServantList), CommandFilter("skip")]
        public async Task SetUpdatedServantListEmpty() {
            var master = await TelegramContext.Masters.FindAsync(int.Parse(TelegramChat["edit_servant_list"]));

            if (master == null) {
                await ReplyTextMessageAsync("Il Master scelto non è più disponibile");
                TelegramChat.State = ConversationState.Idle;
                await SaveChanges();
                return;
            }

            master.ServantList = null;
            _logger.LogDebug($"Impostato null come servant list del Master {master.Name}");
            TelegramChat.State = ConversationState.Idle;
            if (await SaveChanges()) {
                await ReplyTextMessageAsync("Lista dei servant rimossa correttamente");
            }
        }

        #endregion

        #region Aggiornamento support list

        [CommandFilter("support_list")]
        public async Task UpdateSupportList() {
            if (MessageCommand.Parameters.Count < 1) {
                await ReplyTextMessageAsync(
                    "Devi inviarmi il nome del Master da modificare nel formato:\n/support_list <nome>");
            }
            else {
                var master = await TelegramContext.Masters.FirstOrDefaultAsync(m => m.UserId == TelegramChat.Id && m.Name == MessageCommand.Message);
                if (master == null) {
                    await ReplyTextMessageAsync($"Nessun Master trovato con il nome {MessageCommand.Message}");
                }
                else {
                    await GotoEditSupportList(master);
                }
            }
        }

        [ChatStateFilter(ConversationState.UpdatingSupportList), MessageTypeFilter(MessageType.Photo)]
        public async Task SetUpdatedSupportList() {
            var master = await TelegramContext.Masters
                .FirstOrDefaultAsync(m => m.Id == int.Parse(TelegramChat["edit_support_list"]));

            if (master == null) {
                await ReplyTextMessageAsync("Il Master scelto non è più disponibile");
                TelegramChat.State = ConversationState.Idle;
                await SaveChanges();
                return;
            }
            _logger.LogDebug($"Impostando l'immagine {Update.Message.Photo[0].FileId} come support list del Master {master.Name}");
            master.SupportList = Update.Message.Photo[0].FileId;
            master.UseRayshift = false;
            TelegramChat.State = ConversationState.Idle;
            if (await SaveChanges()) {
                await ReplyTextMessageAsync("Aggiornamento della lista dei support avvenuto correttamente");
                await SendSupportListUpdateNotifications(master,
                    $"<i>La Support list del Master {master.Name} è stata aggiornata</i>");
            }
        }

        [ChatStateFilter(ConversationState.UpdatingSupportList), CommandFilter("rayshift")]
        public async Task SetUpdatedSupportListRayshift() {
            var master = await TelegramContext.Masters.FindAsync(int.Parse(TelegramChat["edit_support_list"]));

            if (master == null) {
                await ReplyTextMessageAsync("Il Master scelto non è più disponibile");
                TelegramChat.State = ConversationState.Idle;
                await SaveChanges();
                return;
            }

            if (master.UseRayshift) {
                TelegramChat.State = ConversationState.Idle;
                await SaveChanges();
                await ReplyTextMessageAsync("Rayshift è già impostato per questo master. Se vuoi aggiornare la support list usa il comando /update <MASTER>");
                return;
            }

            var waitingMessage = await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Ok, provo ad impostare Rayshift.io come provider\nAttendere per favore...");

            var region = ServerToRegion(master.Server);

            try {
                var response = await SetupRayshift(region, master);

                if (response != null) {
                    TelegramChat.State = ConversationState.Idle;
                    if (await SaveChanges()) {
                        await BotData.Bot.EditMessageTextAsync(TelegramChat.Id, waitingMessage.MessageId,
                            "Connessione avvenuta con successo! La seguente support list è ottenuta da Rayshift.io:");
                        await ReplyPhotoAsync(
                            new InputOnlineFile(new Uri(response.Response!.SupportList(region))));
                        await ReplyTextMessageAsync(
                            "È possibile disabilitare successivamente Rayshift.io tramite il comando /support_list <MASTER> o aggiornare la lista tramite il comando /update <MASTER>\n");
                    
                        await SendSupportListUpdateNotifications(master,
                            $"<i>Il Master {master.Name} ha abilitato Rayshift</i>");
                    }
                }
                else {
                    await ReplyTextMessageAsync("Errore nell'impostare Rayshift.io come provider.\n" +
                                                "Inviami lo screen dei tuoi support, /rayshift se vuoi riprovare la connessione a Rayshift.io o /skip se vuoi saltare questa fase");
                }
            } catch (Exception e) {
                // TODO Merge errors
                _logger.LogError(e, "Error while requesting support lookup of {FriendCode}", master.FriendCode);
                await ReplyTextMessageAsync("Errore nell'impostare Rayshift.io come provider.\n" +
                                            "Inviami lo screen dei tuoi support, /rayshift se vuoi riprovare la connessione a Rayshift.io o /skip se vuoi saltare questa fase");
            }
        }


        [ChatStateFilter(ConversationState.UpdatingSupportList), CommandFilter("skip")]
        public async Task SetUpdatedSupportListEmpty() {
            var master = await TelegramContext.Masters.FindAsync(int.Parse(TelegramChat["edit_support_list"]));

            if (master == null) {
                await ReplyTextMessageAsync("Il Master scelto non è più disponibile");
                TelegramChat.State = ConversationState.Idle;
                await SaveChanges();
                return;
            }

            _logger.LogDebug($"Impostato null come support list del Master {master.Name}");
            master.SupportList = null;
            master.UseRayshift = false;
            TelegramChat.State = ConversationState.Idle;
            if (await SaveChanges()) {
                await ReplyTextMessageAsync("Lista dei support rimossa correttamente");
                await SendSupportListUpdateNotifications(master,
                    $"<i>La Support list del Master {master.Name} è stata rimossa</i>");
            }
        }

        [ChatStateFilter(ConversationState.Idle), CommandFilter("update")]
        public async Task UpdateRayshiftSupportList() {
            if (MessageCommand.Parameters.Count < 1) {
                await ReplyTextMessageAsync(
                    "Devi inviarmi il nome del Master da modificare nel formato:\n/servant_list <nome>");
            }
            else {
                var master = await TelegramContext.Masters.FirstOrDefaultAsync(m => m.UserId == TelegramChat.Id && m.Name == MessageCommand.Message);
                if (master == null) {
                    await ReplyTextMessageAsync($"Nessun Master trovato con il nome {MessageCommand.Message}");
                }
                else {
                    if (master.UseRayshift) {
                        await UpdateRayshift(master);
                    }
                    else {
                        await ReplyTextMessageAsync("Questo Master non ha abilitato Rayshift.\n" +
                                                    $"È possibile abilitarlo tramite il comando /support_list {master.Name}");
                    }
                    
                }
            }
        }

        #endregion

        #region Inline Callback

        [CallbackCommandFilter(InlineKeyboardCommands.UpdateSupportList)]
        public async Task InlineUpdateSupportList() {
            await BotData.Bot.AnswerCallbackQueryAsync(Update.CallbackQuery.Id);
            
            var master = await GetMasterFromCallbackData();
            if (master == null) {
                await ReplyTextMessageAsync("Il master è errato o non è disponibile");
                return;
            }

            if (master.UseRayshift) {
                await UpdateRayshift(master);
            }
            else {
                await GotoEditSupportList(master);
            }
        }

        [CallbackCommandFilter(InlineKeyboardCommands.UpdateServantList)]
        public async Task InlineUpdateServantList() {
            await BotData.Bot.AnswerCallbackQueryAsync(Update.CallbackQuery.Id);
            
            var master = await GetMasterFromCallbackData();
            if (master == null) {
                await ReplyTextMessageAsync("Il master è errato o non è disponibile");
                return;
            }

            await GotoEditServantList(master);
        }

        [CallbackCommandFilter(InlineKeyboardCommands.EnableRayshift)]
        public async Task InlineEnableRayshift() {
            await BotData.Bot.AnswerCallbackQueryAsync(Update.CallbackQuery.Id);
            
            var master = await GetMasterFromCallbackData();
            if (master == null) {
                // TODO Merge
                await ReplyTextMessageAsync("Il master è errato o non è disponibile");
                return;
            }

            if (master.UseRayshift) {
                await ReplyTextMessageAsync("Rayshift è già abilitato per questo Master");
            }
            else {
                var waitingMessage = await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Ok, provo ad impostare Rayshift.io come provider\nAttendere per favore...");

                var region = ServerToRegion(master.Server);

                try {
                    var response = await SetupRayshift(region, master);

                    if (response != null) {
                        if (await SaveChanges()) {
                            await BotData.Bot.EditMessageTextAsync(TelegramChat.Id, waitingMessage.MessageId,
                                "Connessione avvenuta con successo! La seguente support list è ottenuta da Rayshift.io:");
                            await ReplyPhotoAsync(
                                new InputOnlineFile(new Uri(response.Response!.SupportList(region))));
                            await ReplyTextMessageAsync(
                                "È possibile disabilitare successivamente Rayshift.io tramite il comando /support_list <MASTER> o aggiornare la lista tramite il comando /update <MASTER>\n");
                            await SendSupportListUpdateNotifications(master,
                                $"<i>Il Master {master.Name} ha abilitato Rayshift</i>");
                        }
                    }
                    else {
                        await ReplyTextMessageAsync("Errore nell'impostare Rayshift.io come provider.\n");
                    }
                } catch (Exception e) {
                    // TODO Merge errors
                    _logger.LogError(e, "Error while requesting support lookup of {FriendCode}", master.FriendCode);
                    await ReplyTextMessageAsync("Errore nell'impostare Rayshift.io come provider.");
                }
            }
        }

        [CallbackCommandFilter(InlineKeyboardCommands.DisableRayshift)]
        public async Task InlineDisableRayshift() {
            await BotData.Bot.AnswerCallbackQueryAsync(Update.CallbackQuery.Id);
            
            var master = await GetMasterFromCallbackData();
            if (master == null) {
                // TODO Merge
                await ReplyTextMessageAsync("Il master è errato o non è disponibile");
                return;
            }

            if (!master.UseRayshift) {
                await ReplyTextMessageAsync("Rayshift non è abilitato per questo Master");
            }
            else {
                master.UseRayshift = false;
                if (await SaveChanges()) {
                    await ReplyTextMessageAsync(
                        "Rayshift è stato disabilitato.\n" +
                        $"Ora sei senza support list, se vuoi impostare un immagine come support list usa il comando /support_list {master.Name}\n");
                }
            }
        }

        [CallbackCommandFilter(InlineKeyboardCommands.DeleteMaster)]
        public async Task InlineDeleteMaster() {
            await BotData.Bot.AnswerCallbackQueryAsync(Update.CallbackQuery.Id);
            
            var master = await GetMasterFromCallbackData();
            if (master == null) {
                // TODO Merge
                await ReplyTextMessageAsync("Il master è errato o non è disponibile");
                return;
            }

            TelegramContext.Masters.Remove(master);
            if (await SaveChanges()) {
                await ReplyTextMessageAsync($"Il Master {master.Name} è stato cancellato correttamente");
            }
        }

        #endregion

        private async Task SendSupportListUpdateNotifications(Master master, string text) {
            var chats = await GetRegisteredChatWithSettings(master);

            foreach (var chat in chats.Where(chat => chat.Settings.SupportListNotifications)) {
                try {
                    await BotData.Bot.SendTextMessageAsync(chat.Chat.ChatId, text, ParseMode.Html);
                }
                catch (ApiRequestException e) {
                    _logger.LogWarning(e.Message);
                }
            }
        }

        private async Task SendServantListUpdateNotifications(Master master, string text) {
            var chats = await GetRegisteredChatWithSettings(master);

            foreach (var chat in chats.Where(chat => chat.Settings.ServantListNotifications)) {
                try {
                    await BotData.Bot.SendTextMessageAsync(chat.Chat.ChatId, text, ParseMode.Html);
                }
                catch (ApiRequestException e) {
                    _logger.LogWarning(e.Message);
                }
            }
        }

        private async Task UpdateRayshift(Master master) {
            var waitingMessage = await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                "Aggiornamento della support list, attendere...");

            try {
                var response = await _rayshiftClient.RequestSupportLookupAsync(ServerToRegion(master.Server), master.FriendCode);
                if (response?.MessageType == MessageCode.Finished) {
                    await BotData.Bot.EditMessageTextAsync(TelegramChat.Id, waitingMessage.MessageId,
                        "Aggiornamento completato");
                    await SendSupportListUpdateNotifications(master,
                        $"<i>La Support list del Master {master.Name} è stata aggiornata (via Rayshift)</i>");
                }
            } catch (Exception e) {
                _logger.LogError(e, "Error while requesting support lookup of {FriendCode}", master.FriendCode);
                await BotData.Bot.EditMessageTextAsync(TelegramChat.Id, waitingMessage.MessageId,
                    "Errore durante l'ottenimento della nuova support list.");
            }
        }

        private async Task<List<RegisteredChatSettings>> GetRegisteredChatWithSettings(Master master) {
            var chats = await TelegramContext.RegisteredChats
                .Where(c => c.MasterId == master.Id)
                .Join(TelegramContext.ChatSettings,
                    chat => chat.ChatId,
                    settings => settings.Id,
                    (chat, settings) => new RegisteredChatSettings(chat, settings))
                .ToListAsync();
            return chats;
        }

        private IReplyMarkup BuildSettingsKeyboard(Master master) {
            var data = new Dictionary<string, string>() {
                {"master", master.Name}
            };
            
            var updateSupportList = new InlineKeyboardButton();

            if (master.UseRayshift) {
                updateSupportList.Text = "Aggiorna Support List";
            }
            else {
                updateSupportList.Text = "Cambia Support List";
            }
            updateSupportList.CallbackData = new InlineDataWrapper(InlineKeyboardCommands.UpdateSupportList, data).ToString();

            var updateServantList = new InlineKeyboardButton {
                Text = "Cambia Servant List",
                CallbackData = new InlineDataWrapper(InlineKeyboardCommands.UpdateServantList, data).ToString()
            };
            
            var toggleRayshift = new InlineKeyboardButton();
            if (master.UseRayshift) {
                toggleRayshift.Text = "Disabilita Rayshift";
                toggleRayshift.CallbackData =
                    new InlineDataWrapper(InlineKeyboardCommands.DisableRayshift, data).ToString();
            }
            else {
                toggleRayshift.Text = "Abilita Rayshift";
                toggleRayshift.CallbackData =
                    new InlineDataWrapper(InlineKeyboardCommands.EnableRayshift, data).ToString();
            }
            
            var deleteMaster = new InlineKeyboardButton() {
                Text = "Elimina Master",
                CallbackData = new InlineDataWrapper(InlineKeyboardCommands.DeleteMaster, data).ToString()
            };

            var keyboard = new InlineKeyboardMarkup(new [] {
                new [] {
                    updateSupportList, updateServantList
                },
                new [] {
                    toggleRayshift
                },
                new [] {
                    deleteMaster
                }
            });

            return keyboard;
        }

        private async Task GotoEditSupportList(Master master) {
            TelegramChat["edit_support_list"] = master.Id.ToString();
            TelegramChat.State = ConversationState.UpdatingSupportList;
            if (await SaveChanges()) {
                await ReplyTextMessageAsync(
                    "Inviami la nuova foto, /rayshift se vuoi impostare Rayshift.io come provider o /skip se vuoi rimuoverla");
            }
        }

        private async Task GotoEditServantList(Master master) {
            TelegramChat["edit_servant_list"] = master.Id.ToString();
            TelegramChat.State = ConversationState.UpdatingServantList;
            if (await SaveChanges()) {
                await ReplyTextMessageAsync("Inviami la nuova foto o /skip che rimuoverla");
            }
        }

        private async Task<ApiResponse> SetupRayshift(Region region, Master master) {
            var response = await _rayshiftClient.RequestSupportLookupAsync(region, master.FriendCode);

            if (response?.Response == null) {
                // TODO Use better exceptions
                throw new NullReferenceException("Null response from RequestSupportLookupAsync()");
            }

            if (response.MessageType != MessageCode.Finished) {
                // TODO Use better exceptions
                throw new Exception($"Support lookup response message type is not finished ({response.MessageType})");
            }
            
            master.UseRayshift = true;
            master.SupportList = null;
            return response;
        }

        private static class InlineKeyboardCommands {
            public const string UpdateSupportList = "UpdateSupportList";
            public const string UpdateServantList = "UpdateServantList";
            public const string DeleteMaster = "DeleteMaster";
            public const string EnableRayshift = "EnableRayshift";
            public const string DisableRayshift = "DisableRayshift";
        }

        private class RegisteredChatSettings {
            public RegisteredChat Chat { get; }
            public ChatSettings Settings { get; }

            public RegisteredChatSettings(RegisteredChat chat, ChatSettings settings) {
                Chat = chat;
                Settings = settings;
            }
        }
    }
}