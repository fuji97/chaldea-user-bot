using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DataScraper;
using DataScraper.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Rayshift;
using Rayshift.Models;
using Server.DbContext;
using Telegram.Bot.Advanced.Controller;
using Telegram.Bot.Advanced.Core.Dispatcher.Filters;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace Server.TelegramController
{
    
    public class Controller : TelegramController<MasterContext> {
        private readonly ILogger<Controller> _logger;
        private IMemoryCache _cache;
        private IConfiguration _configuration;

        public Controller(ILogger<Controller> logger, IMemoryCache cache, IConfiguration configuration) {
            _logger = logger;
            _cache = cache;
            _configuration = configuration;
        }

        public Controller() {
        }

        [CommandFilter("help")]
        public async Task Help() {
            await ReplyTextMessageAsync("<b>Chaldea Bot</b>\n\n" +
                                        "Benvenuto nel bot dedicato a Fate/Grand Order.\n" +
                                        "Attualmente questo bot può permettervi di registrare un Master (in privato) " +
                                        "inviandomi le sue informazioni. Dopo che è stato registrato correttamente " +
                                        "potete collegarlo in una qualsasi chat in cui è presente questo bot in modo " +
                                        "da poter inviare velocemente il Master in qualsiasi momento\n" +
                                        "\n" +
                                        "<b>Lista dei comandi:</b>\n" +
                                        "[IN PRIVATO]\n" +
                                        "/add &lt;nome?&gt; - Registra un nuovo Master\n" +
                                        "/list - Mostra una lista di tutti i tuoi Master\n" +
                                        "/master &lt;nome&gt; - Visualizza le informazioni di un tuo Master\n" +
                                        "/remove &lt;nome&gt; - Cancella il Master\n" +
                                        "/support_list &lt;nome&gt; - Aggiorna la support list del Master\n" +
                                        "/servant_list &lt;nome&gt; - Aggiorna la servant list del Master\n" +
                                        "/reset - Resetta lo stato del bot e cancella i dati temporanei (non cancella i Master registrati), da usare solo se il bot da errori ESPLICITI; da NON usare se semplicemente non risponde\n" +
                                        "\n" +
                                        "[NEI GRUPPI]\n" +
                                        "/list - Mostra una lista di tutti i Master registrati nel gruppo\n" +
                                        "/link &lt;nome&gt; - Collega il Master alla chat, in modo che possa essere visualizzato con il comando /master\n" +
                                        "/master &lt;nome&gt; - Visualizza le informazioni del Master (se è collegato)\n" +
                                        "/unlink &lt;nome&gt; - Scollega il Master (gli admin possono scollegare qualsiasi Master)\n" +
                                        "/reset - [SOLO ADMIN] - Resetta lo stato del bot e cancella i dati temporanei (non cancella i Master collegati), da usare solo se il bot da errori ESPLICITI; da NON usare se semplicemente non risponde\n" +
                                        "\n" +
                                        "[OVUNQUE]\n" +
                                        "/help - Mostra questo messaggio\n" +
                                        "\n" +
                                        "Bot creato da @fuji97",
                ParseMode.Html);
        }

        #region Lists

        [CommandFilter("list"), ChatTypeFilter(ChatType.Private), MessageTypeFilter(MessageType.Text)]
        public async Task ListMasters() {
            _logger.LogInformation("Ricevuto comando /list in privato");
            var masters = TelegramContext.Masters.Where(m => m.UserId == TelegramChat.Id).Select(m => m.Name);
            await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                "<b>Lista dei tuoi Master:</b>\n" +
                string.Join("\n", masters),
                ParseMode.Html);
        }
        
        [CommandFilter("list"), ChatTypeFilter(ChatType.Group, ChatType.Supergroup), MessageTypeFilter(MessageType.Text)]
        public async Task ListMastersInGroups() {
            _logger.LogInformation("Ricevuto comando /list in un gruppo");
            var masters = TelegramContext.RegisteredChats.Where(rc => rc.ChatId == TelegramChat.Id)
                .Include(rc => rc.Master)
                .ThenInclude(m => m.User)
                .Select(rc => $"{rc.Master.Name} by <a href=\"tg://user?id={rc.Master.UserId}\">@{rc.Master.User.Username}</a>");
            await ReplyTextMessageAsync(
                "<b>Lista dei Master registrati:</b>\n" +
                string.Join("\n", masters),
                ParseMode.Html);
        }
        
        #endregion
        
        [CommandFilter("add"), ChatTypeFilter(ChatType.Private), MessageTypeFilter(MessageType.Text)]
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
                var result = await client.RequestSupportLookup(region, TelegramChat["friend_code"], async response => {
                    // TODO Use a different DbContext to avoid using a dispatched object
                    if (response?.Response != null) {
                        TelegramChat["support_photo"] = null;
                        TelegramChat["use_rayshift"] = "true";
                        TelegramChat.State = ConversationState.ServantList;
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

        [CommandFilter("remove"), ChatTypeFilter(ChatType.Private)]
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

        [CommandFilter("link"), ChatTypeFilter(ChatType.Group, ChatType.Supergroup)]
        public async Task LinkMaster() {
            if (MessageCommand.Parameters.Count < 1) {
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                    "Devi passarmi il nome del master che vuoi collegare");
            }
            else {
                var master = TelegramContext.Masters.FirstOrDefault(m =>
                    m.User.Id == Update.Message.From.Id && m.Name == MessageCommand.Parameters.Join(" "));

                if (master == null) {
                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                        "Nessun Master trovato con il nome " + MessageCommand.Parameters.Join(" "));
                }
                else {
                    var chat = TelegramContext.RegisteredChats.FirstOrDefault(c =>
                        c.MasterId == master.Id && c.ChatId == TelegramChat.Id);

                    if (chat == null) {
                        TelegramContext.RegisteredChats.Add(new RegisteredChat(master.Id, TelegramChat.Id));
                        if (await SaveChanges()) {
                            await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                                "Master collegato correttamente");                    }
                    }
                    else {
                        await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                            "Master già collegato"); 
                    }
                }
            }
        }
        
        [CommandFilter("unlink"), ChatTypeFilter(ChatType.Group, ChatType.Supergroup)]
        public async Task UnlinkMaster() {
            if (MessageCommand.Parameters.Count < 1) {
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                    "Devi passarmi il nome del master che vuoi scollegare");
            }

            var name = MessageCommand.Parameters.Join(" ");
            
            var master = await TelegramContext.RegisteredChats
                .Include(c => c.Master)
                .Where(c => c.ChatId == TelegramChat.Id)
                .FirstOrDefaultAsync(c => c.Master.Name == name);

            if (master == null) {
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                    "Nessun Master collegato con il nome " + name);
            }
            else {
                if (Update.Message.From.Id != master.Master.UserId) {
                    var isAdmin = (await BotData.Bot.GetChatAdministratorsAsync(TelegramChat.Id))
                        .Any(ua => ua.User.Id == Update.Message.From.Id);
                    if (!isAdmin) {
                        await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                            "Non puoi scollegare questo utente");
                        return;
                    }
                }
                
                TelegramContext.RegisteredChats.Remove(master);
                if (await SaveChanges()) {
                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                        "Master scollegato correttamente"); 
                }
            }
        }

        [CommandFilter("master"), ChatTypeFilter(ChatType.Group, ChatType.Supergroup)]
        public async Task ShowMasterGroups() {
            if (MessageCommand.Parameters.Count < 1) {
                _logger.LogDebug("Ricevuto comando /master senza parametri");
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                    "Devi passarmi il nome del master che vuoi mostrare");
            }
            else {
                var master = TelegramContext.Masters
                    .Include(m => m.RegisteredChats)
                    .Include(m => m.User).SingleOrDefault(m => m.Name == MessageCommand.Parameters.Join(" "));
                if (master == null || master.RegisteredChats.All(c => c.ChatId != TelegramChat.Id)) {
                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                        "Nessun Master trovato con il nome " + MessageCommand.Parameters.Join(" "));
                }
                else {
                    // TODO Unificare metodo di invio del master
                    if (master.UseRayshift) {
                        var supportList = await GetSupportImageFromRayshift(ServerToRegion(master.Server), master.FriendCode);
                        if (supportList != null) {
                            if (master.ServantList != null) {
                                await BotData.Bot.SendMediaGroupAsync(new[] {
                                        new InputMediaPhoto(new InputMedia(master.SupportList)),
                                        new InputMediaPhoto(new InputMedia(master.ServantList))
                                    },
                                    TelegramChat.Id);
                            }
                            else {
                                await BotData.Bot.SendPhotoAsync(TelegramChat.Id, new InputMedia(master.SupportList));
                            }
                        }
                        else {
                            // TODO Log
                            await ReplyTextMessageAsync("Errore nell'ottenere la support list da Rayshift.io");

                            if (master.ServantList != null) {
                                await BotData.Bot.SendPhotoAsync(TelegramChat.Id, new InputMedia(master.SupportList));
                            }
                        }
                    } else if (master.ServantList != null && master.SupportList != null) {
                        await BotData.Bot.SendMediaGroupAsync(new[] {
                                new InputMediaPhoto(new InputMedia(master.SupportList)),
                                new InputMediaPhoto(new InputMedia(master.ServantList))
                            },
                            TelegramChat.Id);
                    }
                    else if (master.ServantList != null) {
                        await BotData.Bot.SendPhotoAsync(TelegramChat.Id, new InputMedia(master.ServantList));
                    }
                    else if (master.SupportList != null) {
                        await BotData.Bot.SendPhotoAsync(TelegramChat.Id, new InputMedia(master.SupportList));
                    }

                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                        $"<b>Master:</b> {master.Name}\n" +
                        $"<b>Friend Code:</b> {master.FriendCode}\n" +
                        $"<b>Server:</b> {master.Server.ToString()}\n" +
                        $"<b>Registrato da:</b> <a href=\"tg://user?id={master.UserId}\">@{master.User.Username}</a>",
                        ParseMode.Html);
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
                    if (master.UseRayshift) {
                        var supportList = await GetSupportImageFromRayshift(ServerToRegion(master.Server), master.FriendCode);
                        if (supportList != null) {
                            if (master.ServantList != null) {
                                await BotData.Bot.SendMediaGroupAsync(new[] {
                                        new InputMediaPhoto(new InputMedia(supportList[SupportListType.Normal])),
                                        new InputMediaPhoto(new InputMedia(supportList[SupportListType.Event])),
                                        new InputMediaPhoto(new InputMedia(master.ServantList))
                                    },
                                    TelegramChat.Id);
                            }
                            else {
                                await BotData.Bot.SendMediaGroupAsync(new IAlbumInputMedia[] {
                                        new InputMediaPhoto(new InputMedia(supportList[SupportListType.Normal])),
                                        new InputMediaPhoto(new InputMedia(supportList[SupportListType.Event]))
                                    }, 
                                    TelegramChat.Id);
                            }
                        }
                        else {
                            // TODO Log
                            await ReplyTextMessageAsync("Errore nell'ottenere la support list da Rayshift.io");

                            if (master.ServantList != null) {
                                await BotData.Bot.SendPhotoAsync(TelegramChat.Id, new InputMedia(master.SupportList));
                            }
                        }
                    } else if (master.ServantList != null && master.SupportList != null) {
                        await BotData.Bot.SendMediaGroupAsync(new[] {
                                new InputMediaPhoto(new InputMedia(master.SupportList)),
                                new InputMediaPhoto(new InputMedia(master.ServantList))
                            },
                            TelegramChat.Id);
                    }
                    else if (master.ServantList != null) {
                        await BotData.Bot.SendPhotoAsync(TelegramChat.Id, new InputMedia(master.ServantList));
                    }
                    else if (master.SupportList != null) {
                        await BotData.Bot.SendPhotoAsync(TelegramChat.Id, new InputMedia(master.SupportList));
                    }

                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                        $"<b>Master:</b> {master.Name}\n" +
                        $"<b>Friend Code:</b> {master.FriendCode}\n" +
                        $"<b>Server:</b> {master.Server.ToString()}\n" +
                        $"<b>Registrato da:</b> <a href=\"tg://user?id={master.UserId}\">@{master.User.Username}</a>",
                        ParseMode.Html);
                }
            }
        }
        
        // TODO Move down
        private async Task<Stream> GetImageStream(Uri url) {
            using HttpClient client = new HttpClient();
            return await client.GetStreamAsync(url);
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

        // TODO Move down
        private static Region ServerToRegion(MasterServer server) {
            Region region = Region.Na;
            switch (server) {
                case MasterServer.JP:
                    region = Region.Jp;
                    break;
                case MasterServer.US:
                    region = Region.Na;
                    break;
            }

            return region;
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

        [CommandFilter("reset")]
        public async Task ResetState() {
            if (Update.Message.Chat.Type == ChatType.Group || Update.Message.Chat.Type == ChatType.Supergroup) {
                if (!Update.Message.Chat.AllMembersAreAdministrators) {
                    if ((await BotData.Bot.GetChatAdministratorsAsync(TelegramChat.Id))
                        .All(cm => cm.User != Update.Message.From)) {

                        await ReplyTextMessageAsync(
                            "Solo gli admin possono resettare lo stato di Chaldea in un gruppo");
                        return;
                    }
                }
            }

            TelegramChat.State = ConversationState.Idle;
            TelegramChat.Data.Clear();

            if (await SaveChanges("Reset Impossibile, contattate un admin di Chaldea per avere supporto diretto")) {
                await ReplyTextMessageAsync("Reset completato con successo");
            }
        }

        [UpdateTypeFilter(UpdateType.InlineQuery)]
        public async Task InlineRequest() {
            List<InlineQueryResultBase> results = new List<InlineQueryResultBase>();
            
            if (Update.InlineQuery.Query.Length < 3) {
                results.Add(new InlineQueryResultArticle(
                    "1",
                    "Pochi caratteri",
                    new InputTextMessageContent("")));
                await BotData.Bot.AnswerInlineQueryAsync(
                    Update.InlineQuery.Id,
                    results);
                return;
            }
            
            List<ServantEntry> servants = await _cache.GetOrCreateAsync("servants", entry => {
                entry.SlidingExpiration = TimeSpan.FromHours(1);
                var scraper = new Scraper();
                return scraper.GetAllServants();
            });
            
            var validServants = servants.Where(x => x.Name.IndexOf(Update.InlineQuery.Query, StringComparison.OrdinalIgnoreCase) >= 0);

            if (!validServants.Any()) {
                results.Add(new InlineQueryResultArticle(
                    "2",
                    "Nessun Servant trovato",
                    new InputTextMessageContent("")));
                await BotData.Bot.AnswerInlineQueryAsync(
                    Update.InlineQuery.Id,
                    results);
                return;
            }

            foreach (var servant in validServants) {
                
                results.Add(new InlineQueryResultArticle(
                    servant.Id,
                    servant.Name,
                    new InputTextMessageContent($"<a href='{servant.ImageUrl}'>&#8205;</a>" +
                                                $"<b>{servant.Name}</b>\n" +
                                                $"{servant.Class} [{(int) servant.Stars}★]\n" +
                                                $"ATK: Base: <b>{servant.BaseAttack}</b> - Max: <b>{servant.MaxAttack}</b>\n" +
                                                $"HP: Base: <b>{servant.BaseHp}</b> - Max: <b>{servant.MaxHp}</b>\n" +
                                                $"Deck: <b>{servant.GetDeckString()}</b>\n" +
                                                $"Noble Phantasm: <b>{servant.NpType}</b>\n\n" +
                                                $"Commenti:\n" +
                                                $"<i>{servant.Comments}</i>\n\n" +
                                                $"<a href='{servant.ServantUrl}'>{servant.Name} su Cirnopedia</a>"
                    ) {
                        ParseMode = ParseMode.Html,
                        DisableWebPagePreview = false
                    }
                ) {
                    Description = $"{servant.Class} [{(int) servant.Stars}★]\nDeck: {servant.GetDeckString()}",
                    ThumbUrl = servant.IconUrl
                });
            }
            
            await BotData.Bot.AnswerInlineQueryAsync(
                Update.InlineQuery.Id,
                results);
        }

        [CommandFilter("servant")]
        public async Task GetServant() {
            if (MessageCommand.Parameters.Count < 1) {
                await ReplyTextMessageAsync("Devi inviarmi il nome del Servant insieme al comando");
                return;
            }

            List<ServantEntry> servants = await _cache.GetOrCreateAsync("servants", entry => {
                entry.SlidingExpiration = TimeSpan.FromHours(1);
                var scraper = new Scraper();
                return scraper.GetAllServants();
            });
            
            var servant = servants.FirstOrDefault(x => x.Name.IndexOf(MessageCommand.Message, StringComparison.OrdinalIgnoreCase) >= 0);
            if (servant != null) {
                await ReplyTextMessageAsync($"<a href='{servant.ImageUrl}'>&#8205;</a>" +
                                      $"<b>{servant.Name}</b>\n" +
                                      $"{servant.Class} [{(int) servant.Stars}★]\n" +
                                      $"ATK: Base: <b>{servant.BaseAttack}</b> - Max: <b>{servant.MaxAttack}</b>\n" +
                                      $"HP: Base: <b>{servant.BaseHp}</b> - Max: <b>{servant.MaxHp}</b>\n" +
                                      $"Deck: <b>{servant.GetDeckString()}</b>\n" +
                                      $"Noble Phantasm: <b>{servant.NpType}</b>\n\n" +
                                      $"Commenti:\n" +
                                      $"<i>{servant.Comments}</i>\n\n" +
                                      $"<a href='{servant.ServantUrl}'>{servant.Name} su Cirnopedia</a>",
                    ParseMode.Html, true);
            }
            else {
                await ReplyTextMessageAsync("Servant non trovato");
            }
        }

        private async Task<Dictionary<SupportListType,string>> GetSupportImageFromRayshift(Region region, string friendCode) {
            Dictionary<SupportListType, string> images = null;
            using (var client = new RayshiftClient(_configuration["ApiKey"])) {
                var master = (await client.GetSupportDeck(region, friendCode))?.Response;
                if (master != null) {
                    images = new Dictionary<SupportListType, string>();
                    images[SupportListType.Normal] = master.SupportList(SupportListType.Normal);
                    images[SupportListType.Event] = master.SupportList(SupportListType.Event);
                    images[SupportListType.Both] = master.SupportList(SupportListType.Both);
                }

                return images;
            }
        }

        private async Task<bool> SaveChanges(string text = "Errore nel salvare i dati, provare a reinviare l'ultimo messaggio") {
            bool error = false;
                try
                {
                    // save data
                    await TelegramContext.SaveChangesAsync();
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex.Message);
                    error = true;
                    await ReplyTextMessageAsync(text);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                    await ReplyTextMessageAsync(text);
                    throw;
                }
            return !error;
        }
    }

    public static class ConversationState {
        public const string Idle = null;
        public const string Nome = "ChaldeabotController_Nome";
        public const string FriendCode = "ChaldeabotController_FriendCode";
        public const string Server = "ChaldeabotController_Server";
        public const string SupportList = "ChaldeabotController_SupportList";
        public const string ServantList = "ChaldeabotController_ServantList";
        public const string UpdatingSupportList = "ChaldeabotController_UpdatingSupportList";
        public const string UpdatingServantList = "ChaldeabotController_UpdatingServantList";
        public const string WaitingRayshift = "ChaldeabotController_WaitingRayshift";
    }
}
