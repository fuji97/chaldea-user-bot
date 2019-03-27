using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Logging;
using Server.DbContext;
using Telegram.Bot.Advanced.Controller;
using Telegram.Bot.Advanced.DbContexts;
using Telegram.Bot.Advanced.Dispatcher.Filters;
using Telegram.Bot.Advanced.Models;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Server.TelegramController
{
    
    public class Controller : TelegramController<MasterContext> {
        private readonly ILogger<Controller> _logger;

        public Controller(ILogger<Controller> logger) {
            _logger = logger;
        }

        public Controller() {
        }


        [CommandFilter("list"), ChatTypeFilter(ChatType.Private), MessageTypeFilter(MessageType.Text)]
        public async Task ListMasters() {
            _logger.LogInformation("Ricevuto comando /list");
            var masters = TelegramContext.Masters.Where(m => m.UserId == TelegramChat.Id).Select(m => m.Name);
            await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                "<b>Lista dei tuoi Master:</b>\n" +
                string.Join("\n", masters),
                ParseMode.Html);
            _logger.LogInformation("Ricevuto comando /list");
        }
        
        
        [CommandFilter("add"), ChatTypeFilter(ChatType.Private), MessageTypeFilter(MessageType.Text)]
        public async Task Add() {
            _logger.LogInformation("Ricevuto comando /add");
            if (MessageCommand.Parameters.Count < 1) {
                TelegramChat.State = (int) ConversationState.Nome;
                if (SaveChanges()) {
                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Ok, inviami il nome che vuoi usare");
                }
                else {
                    await ReplyTextMessageAsync("Errore nel salvare i dati, provare a reinviare l' ultimo messaggio");
                }
            }
            else {
                if (CheckName(MessageCommand.Message)) {
                    
                    TelegramChat.State = (int) ConversationState.FriendCode;
                    TelegramChat["nome"] = MessageCommand.Message;
                    if (SaveChanges()) {
                        await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                            "Ok, inviami il friend code in formato XXXXXXXXX");
                    }
                    else {
                        await ReplyTextMessageAsync("Errore nel salvare i dati, provare a reinviare l' ultimo messaggio");
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
                    if (SaveChanges()) {
                        await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Ok, adesso inviami il friend code in formato XXXXXXXXX");
                    }
                    else {
                        await ReplyTextMessageAsync("Errore nel salvare i dati, provare a reinviare l' ultimo messaggio");
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
                if (SaveChanges()) {
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
                else {
                    await ReplyTextMessageAsync("Errore nel salvare i dati, provare a reinviare l' ultimo messaggio");
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
                    if (SaveChanges()) {
                        await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Server giapponese impostato, lo screen dei tuoi support o /skip se vuoi saltare questa fase ",
                            replyMarkup: new ReplyKeyboardRemove());
                    }
                    else {
                        await ReplyTextMessageAsync("Errore nel salvare i dati, provare a reinviare l' ultimo messaggio");
                    }
                    break;
                case "US":
                    
                    TelegramChat["server"] = ((int)MasterServer.US).ToString();
                    TelegramChat.State = (int)ConversationState.SupportList;
                    if (SaveChanges()) {
                        await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Server americano impostato, lo screen dei tuoi support o /skip se vuoi saltare questa fase ",
                            replyMarkup: new ReplyKeyboardRemove());
                    }
                    else {
                        await ReplyTextMessageAsync("Errore nel salvare i dati, provare a reinviare l' ultimo messaggio");
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
            if (SaveChanges()) {
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Hai saltato l'assegnazione della support list\nOra inviami lo screen della lista dei tuoi servant o /skip se vuoi saltare questa fase");
            }
            else {
                await ReplyTextMessageAsync("Errore nel salvare i dati, provare a reinviare l' ultimo messaggio");
            }
        }

        [ChatStateFilter((int) ConversationState.SupportList), NoCommandFilter, MessageTypeFilter(MessageType.Photo)]
        public async Task SupportList()
        {
            _logger.LogInformation("Ricevuta foto");
            TelegramChat["support_photo"] = Update.Message.Photo[0].FileId;
            TelegramChat.State = (int) ConversationState.ServantList;
            if (SaveChanges()) {
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, $"Ok, ora inviami lo screen della lista dei tuoi servant o /skip se vuoi saltare questa fase");
            }
            else {
                await ReplyTextMessageAsync("Errore nel salvare i dati, provare a reinviare l' ultimo messaggio");
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
            if (SaveChanges()) {
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, $"Ok, Master creato\nOra lo puoi collevare alle varie chat con il comando \\link " + TelegramChat["nome"]);
            }
            else {
                await ReplyTextMessageAsync("Errore nel salvare i dati, provare a reinviare l' ultimo messaggio");
            }
        }
        
        [ChatStateFilter((int) ConversationState.ServantList), CommandFilter("skip"), MessageTypeFilter(MessageType.Text)]
        public async Task SkipServantList() {
            var master = new Master(TelegramChat, TelegramChat["nome"], TelegramChat["friend_code"], (MasterServer) Int32.Parse(TelegramChat["server"]), TelegramChat["support_photo"],
                null);
            TelegramContext.Add(master);
            TelegramChat.State = (int)ConversationState.Idle;
            TelegramChat.Data.Clear();
            if (SaveChanges()) {
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Hai saltato l'assegnazione della servant list\nOk, Master creato\nOra lo puoi collevare alle varie chat con il comando \\link " + TelegramChat["nome"]);
            }
            else {
                await ReplyTextMessageAsync("Errore nel salvare i dati, provare a reinviare l' ultimo messaggio");
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
                    if (SaveChanges()) {
                        await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                            "Master cancellato correttamente");                    }
                    else {
                        await ReplyTextMessageAsync("Errore nel salvare i dati, provare a reinviare l' ultimo messaggio");
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
                        if (SaveChanges()) {
                            await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                                "Master collegato correttamente");                    }
                        else {
                            await ReplyTextMessageAsync("Errore nel salvare i dati, provare a reinviare l' ultimo messaggio");
                        }
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
                                new InputMediaPhoto(new InputMedia(master.ServantList)),
                                new InputMediaPhoto(new InputMedia(master.SupportList))
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
                        $"<b>Registrato da:</b> <a href=\"tg://user?id={master.UserId}\">@{master.User.Username}</a>\n",
                        ParseMode.Html);
                }
            }
        }

        private bool SaveChanges() {
            bool error = false;
                try
                {
                    // save data
                    TelegramContext.SaveChanges();
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex.Message);
                    error = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                    // quit if a severe error occurred
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
        ServantList = 5
    }
}
