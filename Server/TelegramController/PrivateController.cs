using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Rayshift;
using Rayshift.Models;
using Server.DbContext;
using Telegram.Bot.Advanced.Core.Dispatcher.Filters;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace Server.TelegramController {
    [ChatTypeFilter(ChatType.Private)]
    public class PrivateController : Controller {
        private ILogger<InlineController> _logger;

        public PrivateController(IMemoryCache cache, IConfiguration configuration, ILogger<InlineController> logger) : base(logger, cache, configuration) {
            _logger = logger;
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

        #region Create Master

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
                if (CheckName(MessageCommand.Message)) {
                    
                    TelegramChat.State = ConversationState.FriendCode;
                    TelegramChat["nome"] = MessageCommand.Message;
                    // TODO Merge Friend code request
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
        }

        [ChatStateFilter(ConversationState.Nome), NoCommandFilter, MessageTypeFilter(MessageType.Text)]
        public async Task GetNome() {
            _logger.LogInformation($"Nome ricevuto da @{TelegramChat?.Username}: {MessageCommand.Text}");
            if (TelegramChat != null) {
                if (CheckName(MessageCommand.Text)) {
                    TelegramChat["nome"] = MessageCommand.Text;
                    TelegramChat.State = ConversationState.FriendCode;
                    if (await SaveChanges()) {
                        await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Ok, adesso inviami il friend code in formato XXXXXXXXX");
                    }
                }
                else {
                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                        "Nome invalido o già in uso, sceglierne un altro");
                }
                
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
                    
                    TelegramChat["server"] = ((int) MasterServer.JP).ToString();
                    TelegramChat.State = ConversationState.SupportList;
                    if (await SaveChanges()) {
                        await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Server giapponese impostato, inviami lo screen dei tuoi support, /rayshift se vuoi ottenere automaticamente la support list da Rayshift.io o /skip se vuoi saltare questa fase",
                            replyMarkup: new ReplyKeyboardRemove());
                    }
 
                    break;
                case "US":
                    
                    TelegramChat["server"] = ((int)MasterServer.US).ToString();
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

            using (var client = new RayshiftClient(_configuration["ApiKey"])) {
                var result = await client.RequestSupportLookupAsync(region, TelegramChat["friend_code"]);
                
                if (result?.Response != null) {
                    TelegramChat["support_photo"] = null;
                    TelegramChat["use_rayshift"] = "true";
                    TelegramChat.State = ConversationState.ServantList;
                    if (await SaveChanges()) {
                        await ReplyTextMessageAsync(
                            "Connessione avvenuta con successo! La seguente support list è ottenuta da Rayshift.io:");
                        await ReplyPhotoAsync(
                            new InputOnlineFile(new Uri(result.Response.SupportList(SupportListType.Both))));
                        await ReplyTextMessageAsync(
                            "È possibile disabilitare successivamente Rayshift.io tramite il comando /support_list <MASTER> o aggiornare la lista tramite il comando /update <MASTER>\n" +
                            "Ora inviami lo screen della lista dei tuoi servant o /skip se vuoi saltare questa fase. Ora inviami lo screen della lista dei tuoi servant o /skip se vuoi saltare questa fase");
                    }
                } else {
                    await ReplyTextMessageAsync("Errore nell'impostare Rayshift.io come provider.\n" +
                                                "Inviami lo screen dei tuoi support, /rayshift se vuoi riprovare la connessione a Rayshift.io o /skip se vuoi saltare questa fase");
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
        public async Task ServantList()
        {
            _logger.LogInformation("Ricevuta foto, creazione Master e inserimento");
            // TODO Unire le due creazioni del master sotto un unico metodo
            var master = new Master(TelegramChat, TelegramChat["nome"], TelegramChat["friend_code"], (MasterServer) Int32.Parse(TelegramChat["server"]), TelegramChat["support_photo"],
                Update.Message.Photo[0].FileId, TelegramChat["use_rayshift"] == "true");
            TelegramContext.Add(master);
            TelegramChat.State = ConversationState.Idle;
            TelegramChat.Data.Clear();
            if (await SaveChanges()) {
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, $"Ok, Master creato\nOra lo puoi collegare alle varie chat con il comando /link " + TelegramChat["nome"]);
            }
        }
        
        [ChatStateFilter(ConversationState.ServantList), CommandFilter("skip"), MessageTypeFilter(MessageType.Text)]
        public async Task SkipServantList() {
            var master = new Master(TelegramChat, TelegramChat["nome"], TelegramChat["friend_code"], (MasterServer) Int32.Parse(TelegramChat["server"]), TelegramChat["support_photo"],
                null, TelegramChat["use_rayshift"] == "true");
            TelegramContext.Add(master);
            TelegramChat.State = ConversationState.Idle;
            TelegramChat.Data.Clear();
            if (await SaveChanges()) {
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Hai saltato l'assegnazione della servant list\nOk, Master creato\nOra lo puoi collegare alle varie chat con il comando /link " + TelegramChat["nome"]);
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
                    m.UserId == TelegramChat.Id && m.Name == MessageCommand.Parameters.Join(" "));

                if (master == null) {
                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                        "Nessun Master trovato con il nome " + MessageCommand.Parameters.Join(" "));
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
                    .SingleOrDefault(m => m.Name == MessageCommand.Parameters.Join(" ") && 
                                          m.UserId == Update.Message.Chat.Id);
                if (master == null) {
                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                        "Nessun Master trovato con il nome " + MessageCommand.Parameters.Join(" "));
                }
                else {
                    await SendMaster(master);
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
                    TelegramChat["edit_servant_list"] = master.Id.ToString();
                    TelegramChat.State = ConversationState.UpdatingServantList;
                    if (await SaveChanges()) {
                        await ReplyTextMessageAsync("Inviami la nuova foto o /skip che rimuoverla");
                    }
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
                foreach (var chat in master.RegisteredChats) {
                    await BotData.Bot.SendTextMessageAsync(chat.ChatId,
                        $"<i>La Servant list del Master {master.Name} è stata aggiornata</i>", ParseMode.Html);
                }
                await ReplyTextMessageAsync("Aggiornamento della lista dei servant completato correttamente");
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
                    TelegramChat["edit_support_list"] = master.Id.ToString();
                    TelegramChat.State = ConversationState.UpdatingSupportList;
                    if (await SaveChanges()) {
                        await ReplyTextMessageAsync("Inviami la nuova foto, /rayshift se vuoi impostare Rayshift.io come provider o /skip se vuoi rimuoverla");
                    }
                }
            }
        }

        [ChatStateFilter(ConversationState.UpdatingSupportList), MessageTypeFilter(MessageType.Photo)]
        public async Task SetUpdatedSupportList() {
            var master = await TelegramContext.Masters
                .Include(m => m.RegisteredChats)
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
                foreach (var chat in master.RegisteredChats) {
                    await BotData.Bot.SendTextMessageAsync(chat.ChatId,
                        $"<i>La Support list del Master {master.Name} è stata aggiornata</i>", ParseMode.Html);
                }
                await ReplyTextMessageAsync("Aggiornamento della lista dei support avvenuto correttamente");
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

            await ReplyTextMessageAsync("Ok, provo ad impostare Rayshift.io come provider\nAttendere per favore...");

            var region = ServerToRegion(master.Server);

            // TODO Unificare la connessione a Rayshift?

            using (var client = new RayshiftClient(_configuration["ApiKey"])) {
                var result = await client.RequestSupportLookup(region, master.FriendCode, async response => {
                    // TODO Use a different DbContext to avoid using a dispatched object
                    if (response?.Response != null) {
                        master.UseRayshift = true;
                        master.SupportList = null;
                        TelegramChat.State = ConversationState.Idle;
                        if (await SaveChanges()) {
                            await ReplyTextMessageAsync(
                                "Connessione avvenuta con successo! La seguente support list è ottenuta da Rayshift.io:");
                            await ReplyPhotoAsync(
                                new InputOnlineFile(new Uri(response.Response.SupportList(SupportListType.Both))));
                            await ReplyTextMessageAsync(
                                "È possibile disabilitare successivamente Rayshift.io tramite il comando /support_list <MASTER> o aggiornare la lista tramite il comando /update <MASTER>\n" +
                                "Ora inviami lo screen della lista dei tuoi servant o /skip se vuoi saltare questa fase. Ora inviami lo screen della lista dei tuoi servant o /skip se vuoi saltare questa fase");
                        }
                    }
                });

                if (!result) {
                    await ReplyTextMessageAsync("Errore nell'impostare Rayshift.io come provider.\n" +
                                                "Inviami lo screen dei tuoi support, /rayshift se vuoi riprovare la connessione a Rayshift.io o /skip se vuoi saltare questa fase");
                    return;
                }
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
            }
        }

        [ChatStateFilter(ConversationState.Idle), CommandFilter("update")]
        public async Task UpdateRayshiftSupportList() {
            await ReplyTextMessageAsync(
                "Non ancora implementato, per favore, aggiornare il master manualmente da Rayshift.io");
            // TODO Implement
        }
        
        #endregion
    }
}