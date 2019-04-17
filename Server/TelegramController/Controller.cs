using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DataScraper;
using DataScraper.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Server.DbContext;
using Server.Filters;
using Telegram.Bot.Advanced.Controller;
using Telegram.Bot.Advanced.DbContexts;
using Telegram.Bot.Advanced.Dispatcher.Filters;
using Telegram.Bot.Advanced.Models;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;

namespace Server.TelegramController
{
    
    public class Controller : TelegramController<MasterContext> {
        private readonly ILogger<Controller> _logger;
        private IMemoryCache _cache;

        public Controller(ILogger<Controller> logger, IMemoryCache cache) {
            _logger = logger;
            _cache = cache;
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
                                        "/remove &lt;nome&gt; - Cancella il Master\n" +
                                        "/support_list &lt;nome&gt; - Aggiorna la support list del Master\n" +
                                        "/servant_list &lt;nome&gt; - Aggiorna la servant list del Master\n" +
                                        "/reset - Resetta lo stato del bot e cancella i dati temporanei (non cancella i Master registrati), da usare solo se il bot da errori ESPLICITI; da NON usare se semplicemente non risponde\n" +
                                        "\n" +
                                        "[NEI GRUPPI]\n" +
                                        "/list - Mostra una lista di tutti i Master registrati nel gruppo\n" +
                                        "/link &lt;nome&gt; - Collega il Master alla chat, in modo che possa essere visualizzato con il comando /master\n" +
                                        "/master &lt;nome&gt; - Visualizza le informazioni del Master (se è collegato)\n" +
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
                TelegramChat.State = (int) ConversationState.Nome;
                if (await SaveChanges()) {
                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Ok, inviami il nome che vuoi usare");
                }
            }
            else {
                if (CheckName(MessageCommand.Message)) {
                    
                    TelegramChat.State = (int) ConversationState.FriendCode;
                    TelegramChat["nome"] = MessageCommand.Message;
                    if (await SaveChanges()) {
                        await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                            "Ok, inviami il friend code in formato XXXXXXXXX");
                    }
                }
                else {
                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                        "Nome invalido o già in uso, sceglierne un altro");
                }

            }
        }

        [ChatStateFilter((int) ConversationState.Nome), NoCommandFilter, MessageTypeFilter(MessageType.Text)]
        public async Task GetNome() {
            _logger.LogInformation($"Nome ricevuto da @{TelegramChat?.Username}: {MessageCommand.Text}");
            if (TelegramChat != null) {
                if (CheckName(MessageCommand.Text)) {
                    TelegramChat["nome"] = MessageCommand.Text;
                    TelegramChat.State = (int)ConversationState.FriendCode;
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

        [ChatStateFilter((int) ConversationState.FriendCode), NoCommandFilter, MessageTypeFilter(MessageType.Text)]
        public async Task GetFriendCode() {
            _logger.LogInformation($"Ricevuto friend code: {Update.Message.Text}");
            if (Regex.IsMatch(Update.Message.Text, @"^\d{5}")) {
                TelegramChat["friend_code"] = Update.Message.Text;
                TelegramChat.State = (int) ConversationState.Server;
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
                    "Il friend code non è valido, deve avere il seguente formato XXXXXXXXX");
            }
        }

        [ChatStateFilter((int)ConversationState.Server), NoCommandFilter, MessageTypeFilter(MessageType.Text)]
        public async Task GetServer()
        {
            _logger.LogInformation($"Ricevuto server {Update.Message.Text}");
            switch (Update.Message.Text) {
                case "JP":
                    
                    TelegramChat["server"] = ((int) MasterServer.JP).ToString();
                    TelegramChat.State = (int) ConversationState.SupportList;
                    if (await SaveChanges()) {
                        await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Server giapponese impostato, lo screen dei tuoi support o /skip se vuoi saltare questa fase ",
                            replyMarkup: new ReplyKeyboardRemove());
                    }
 
                    break;
                case "US":
                    
                    TelegramChat["server"] = ((int)MasterServer.US).ToString();
                    TelegramChat.State = (int)ConversationState.SupportList;
                    if (await SaveChanges()) {
                        await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Server americano impostato, lo screen dei tuoi support o /skip se vuoi saltare questa fase ",
                            replyMarkup: new ReplyKeyboardRemove());
                    }
                    break;
                default:
                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Server non valido, specificare 'JP' o 'US'");
                    break;
            }
        }

        [ChatStateFilter((int) ConversationState.SupportList), CommandFilter("skip"), MessageTypeFilter(MessageType.Text)]
        public async Task SkipSupportList() {
            TelegramChat["support_photo"] = null;
            TelegramChat.State = (int) ConversationState.ServantList;
            if (await SaveChanges()) {
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Hai saltato l'assegnazione della support list\nOra inviami lo screen della lista dei tuoi servant o /skip se vuoi saltare questa fase");
            }
        }

        [ChatStateFilter((int) ConversationState.SupportList), NoCommandFilter, MessageTypeFilter(MessageType.Photo)]
        public async Task SupportList()
        {
            _logger.LogInformation("Ricevuta foto");
            TelegramChat["support_photo"] = Update.Message.Photo[0].FileId;
            TelegramChat.State = (int) ConversationState.ServantList;
            if (await SaveChanges()) {
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, $"Ok, ora inviami lo screen della lista dei tuoi servant o /skip se vuoi saltare questa fase");
            }
        }
        
        [ChatStateFilter((int)ConversationState.ServantList), NoCommandFilter, MessageTypeFilter(MessageType.Photo)]
        public async Task ServantList()
        {
            _logger.LogInformation("Ricevuta foto, creazione Master e inserimento");
            var master = new Master(TelegramChat, TelegramChat["nome"], TelegramChat["friend_code"], (MasterServer) Int32.Parse(TelegramChat["server"]), TelegramChat["support_photo"],
                Update.Message.Photo[0].FileId);
            TelegramContext.Add(master);
            TelegramChat.State = (int)ConversationState.Idle;
            TelegramChat.Data.Clear();
            if (await SaveChanges()) {
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, $"Ok, Master creato\nOra lo puoi collevare alle varie chat con il comando /link " + TelegramChat["nome"]);
            }
        }
        
        [ChatStateFilter((int) ConversationState.ServantList), CommandFilter("skip"), MessageTypeFilter(MessageType.Text)]
        public async Task SkipServantList() {
            var master = new Master(TelegramChat, TelegramChat["nome"], TelegramChat["friend_code"], (MasterServer) Int32.Parse(TelegramChat["server"]), TelegramChat["support_photo"],
                null);
            TelegramContext.Add(master);
            TelegramChat.State = (int)ConversationState.Idle;
            TelegramChat.Data.Clear();
            if (await SaveChanges()) {
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Hai saltato l'assegnazione della servant list\nOk, Master creato\nOra lo puoi collevare alle varie chat con il comando /link " + TelegramChat["nome"]);
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

        [CommandFilter("master"), ChatTypeFilter(ChatType.Group, ChatType.Supergroup)]
        public async Task ShowMaster() {
            if (MessageCommand.Parameters.Count < 1) {
                _logger.LogDebug("Ricevuto comando /master senza parametri");
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                    "Devi passarmi il nome del master che vuoi inviare");
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
                    if (master.ServantList != null && master.SupportList != null) {
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
                    TelegramChat.State = (int) ConversationState.UpdatingServantList;
                    if (await SaveChanges()) {
                        await ReplyTextMessageAsync("Inviami la nuova foto o /skip che rimuoverla");
                    }
                }
            }
        }

        [ChatStateFilter((int) ConversationState.UpdatingServantList), MessageTypeFilter(MessageType.Photo)]
        public async Task SetUpdatedServantList() {
            var master = await TelegramContext.Masters
                .Include(m => m.RegisteredChats)
                .FirstOrDefaultAsync(m => m.Id == int.Parse(TelegramChat["edit_servant_list"]));

            if (master == null) {
                await ReplyTextMessageAsync("Il Master scelto non è più disponibile");
                TelegramChat.State = (int) ConversationState.Idle;
                await SaveChanges();
                return;
            }

            _logger.LogDebug($"Impostando l'immagine {Update.Message.Photo[0].FileId} come servant list del Master {master.Name}");
            master.ServantList = Update.Message.Photo[0].FileId;
            TelegramChat.State = (int) ConversationState.Idle;
            if (await SaveChanges()) {
                foreach (var chat in master.RegisteredChats) {
                    await BotData.Bot.SendTextMessageAsync(chat.ChatId,
                        $"<i>La Servant list del Master {master.Name} è stata aggiornata</i>", ParseMode.Html);
                }
                await ReplyTextMessageAsync("Aggiornamento della lista dei servant completato correttamente");
            }
        }
        
        [ChatStateFilter((int) ConversationState.UpdatingServantList), CommandFilter("skip")]
        public async Task SetUpdatedServantListEmpty() {
            var master = await TelegramContext.Masters.FindAsync(int.Parse(TelegramChat["edit_servant_list"]));

            if (master == null) {
                await ReplyTextMessageAsync("Il Master scelto non è più disponibile");
                TelegramChat.State = (int) ConversationState.Idle;
                await SaveChanges();
                return;
            }

            master.ServantList = null;
            _logger.LogDebug($"Impostato null come servant list del Master {master.Name}");
            TelegramChat.State = (int) ConversationState.Idle;
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
                    TelegramChat.State = (int) ConversationState.UpdatingSupportList;
                    if (await SaveChanges()) {
                        await ReplyTextMessageAsync("Inviami la nuova foto o /skip che rimuoverla");
                    }
                }
            }
        }

        [ChatStateFilter((int) ConversationState.UpdatingSupportList), MessageTypeFilter(MessageType.Photo)]
        public async Task SetUpdatedSupportList() {
            var master = await TelegramContext.Masters
                .Include(m => m.RegisteredChats)
                .FirstOrDefaultAsync(m => m.Id == int.Parse(TelegramChat["edit_support_list"]));

            if (master == null) {
                await ReplyTextMessageAsync("Il Master scelto non è più disponibile");
                TelegramChat.State = (int) ConversationState.Idle;
                await SaveChanges();
                return;
            }
            _logger.LogDebug($"Impostando l'immagine {Update.Message.Photo[0].FileId} come support list del Master {master.Name}");
            master.SupportList = Update.Message.Photo[0].FileId;
            TelegramChat.State = (int) ConversationState.Idle;
            if (await SaveChanges()) {
                foreach (var chat in master.RegisteredChats) {
                    await BotData.Bot.SendTextMessageAsync(chat.ChatId,
                        $"<i>La Support list del Master {master.Name} è stata aggiornata</i>", ParseMode.Html);
                }
                await ReplyTextMessageAsync("Aggiornamento della lista dei support avvenuto correttamente");
            }
        }
        
        [ChatStateFilter((int) ConversationState.UpdatingSupportList), CommandFilter("skip")]
        public async Task SetUpdatedSupportListEmpty() {
            var master = await TelegramContext.Masters.FindAsync(int.Parse(TelegramChat["edit_support_list"]));

            if (master == null) {
                await ReplyTextMessageAsync("Il Master scelto non è più disponibile");
                TelegramChat.State = (int) ConversationState.Idle;
                await SaveChanges();
                return;
            }

            _logger.LogDebug($"Impostato null come support list del Master {master.Name}");
            master.SupportList = null;
            TelegramChat.State = (int) ConversationState.Idle;
            if (await SaveChanges()) {
                await ReplyTextMessageAsync("Lista dei support rimossa correttamente");
            }
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

            TelegramChat.State = 0;
            TelegramChat.Data.Clear();

            if (await SaveChanges("Reset Impossibile, contattate un admin di Chaldea per avere supporto diretto")) {
                await ReplyTextMessageAsync("Reset completato con successo");
            }
        }

        [InlineFilter]
        public async Task InlineRequest() {
            List<InlineQueryResultBase> results = new List<InlineQueryResultBase>();
            
            if (MessageCommand.Text.Length < 3) {
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
            
            var validServants = servants.Where(x => x.Name.IndexOf(MessageCommand.Message, StringComparison.OrdinalIgnoreCase) >= 0);

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
                    new InputTextMessageContent($"" +
                                                $"<b>{servant.Name}</b>\n" +
                                                $"{servant.Class} [{(int) servant.Stars}★]\n" +
                                                $"ATK: Base: <b>{servant.BaseAttack}</b> - Max: <b>{servant.MaxAttack}</b>\n" +
                                                $"HP: Base: <b>{servant.BaseHp}</b> - Max: <b>{servant.MaxHp}</b>\n" +
                                                $"Cards:\n" +
                                                $"- Quick: <b>{servant.Cards[AttackType.Quick]}</b>\n" +
                                                $"- Arts: <b>{servant.Cards[AttackType.Arts]}</b>\n" +
                                                $"- Buster: <b>{servant.Cards[AttackType.Buster]}</b>\n" +
                                                $"Noble Phantasm: <b>{servant.NpType}</b>\n\n" +
                                                $"Commenti:\n" +
                                                $"<i>{servant.Comments}</i>\n\n" +
                                                $"<a href='{servant.ServantUrl}'>{servant.Name} su Cirnopedia</a>"
                    ) {
                        ParseMode = ParseMode.Html,
                        DisableWebPagePreview = true
                    }
                ) {
                    Description = $"{servant.Class} [{(int) servant.Stars}★]\n",
                    ThumbUrl = servant.ImageUrl
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
                await ReplyTextMessageAsync($"<b>{servant.Name}</b>\n" +
                                      $"{servant.Class} [{(int) servant.Stars}★]\n" +
                                      $"ATK: Base: <b>{servant.BaseAttack}</b> - Max: <b>{servant.MaxAttack}</b>\n" +
                                      $"HP: Base: <b>{servant.BaseHp}</b> - Max: <b>{servant.MaxHp}</b>\n" +
                                      $"Cards:\n" +
                                      $"- Quick: <b>{servant.Cards[AttackType.Quick]}</b>\n" +
                                      $"- Arts: <b>{servant.Cards[AttackType.Arts]}</b>\n" +
                                      $"- Buster: <b>{servant.Cards[AttackType.Buster]}</b>\n" +
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

    public enum ConversationState {
        Idle = 0,
        Nome = 1,
        FriendCode = 2,
        Server = 3,
        SupportList = 4,
        ServantList = 5,
        UpdatingSupportList = 6,
        UpdatingServantList = 7
    }
}
