using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
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
    
    public class Controller : TelegramController<MasterContext>
    {

        [CommandFilter("list"), ChatTypeFilter(ChatType.Private), MessageTypeFilter(MessageType.Text)]
        public async Task ListMasters() {
            Console.WriteLine("Ricevuto comando /list");
            var masters = TelegramContext.Masters.Where(m => m.UserId == TelegramChat.Id).Select(m => m.Name);
            await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                "<b>Lista dei tuoi Master:</b>\n" +
                string.Join("\n", masters),
                ParseMode.Html);
            Console.WriteLine("Ricevuto comando /list");
        }
        
        
        [CommandFilter("add"), ChatTypeFilter(ChatType.Private), MessageTypeFilter(MessageType.Text)]
        public async Task Add() {
            Console.WriteLine("Ricevuto comando /add");
            if (MessageCommand.Parameters.Count < 1) {
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Ok, inviami il nome che vuoi usare");
                TelegramChat.State = (int) ConversationState.Nome;
            }
            else {
                if (CheckName(MessageCommand.Parameters.Join(" "))) {
                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                        "Ok, inviami il friend code in formato XXXXXXXXX");
                    TelegramChat.State = (int) ConversationState.FriendCode;
                    TelegramChat["nome"] = MessageCommand.Parameters.Join(" ");
                }
                else {
                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                        "Nome già in uso, sceglierne un altro");
                }

            }
        }

        [ChatStateFilter((int) ConversationState.Nome), NoCommandFilter, MessageTypeFilter(MessageType.Text)]
        public async Task GetNome() {
            Console.WriteLine($"Nome ricevuto da @{TelegramChat?.Username}: {MessageCommand.Message}");
            if (TelegramChat != null) {
                if (CheckName(MessageCommand.Message)) {
                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Ok, adesso inviami il friend code in formato XXXXXXXXX");
                    TelegramChat["nome"] = MessageCommand.Message;
                    TelegramChat.State = (int)ConversationState.FriendCode;
                }
                else {
                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                        "Nome già in uso, sceglierne un altro");
                }
                
            }
        }

        private bool CheckName(string messageText) {
            return !TelegramContext.Masters.Any(m => m.Name == messageText);
        }

        [ChatStateFilter((int) ConversationState.FriendCode), NoCommandFilter, MessageTypeFilter(MessageType.Text)]
        public async Task GetFriendCode() {
            Console.WriteLine($"Ricevuto friend code: {Update.Message.Text}");
            if (Regex.IsMatch(Update.Message.Text, @"^\d{5}")) {
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
                TelegramChat["friend_code"] = Update.Message.Text;
                TelegramChat.State = (int) ConversationState.Server;
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
            Console.WriteLine($"Ricevuto server {Update.Message.Text}");
            switch (Update.Message.Text) {
                case "JP":
                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Server giapponese impostato, lo screen dei tuoi support o /skip se vuoi saltare questa fase ",
                        replyMarkup: new ReplyKeyboardRemove());
                    TelegramChat["server"] = ((int) MasterServer.JP).ToString();
                    TelegramChat.State = (int) ConversationState.SupportList;
                    break;
                case "US":
                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Server americano impostato, lo screen dei tuoi support o /skip se vuoi saltare questa fase ",
                        replyMarkup: new ReplyKeyboardRemove());
                    TelegramChat["server"] = ((int)MasterServer.US).ToString();
                    TelegramChat.State = (int)ConversationState.SupportList;
                    break;
                default:
                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Server non valido, specificare 'JP' o 'US'");
                    break;
            }
        }

        [ChatStateFilter((int) ConversationState.SupportList), CommandFilter("skip"), MessageTypeFilter(MessageType.Text)]
        public async Task SkipSupportList() {
            await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Hai saltato l'assegnazione della support list\nOra inviami lo screen della lista dei tuoi servant o /skip se vuoi saltare questa fase");
            TelegramChat["support_photo"] = null;
            TelegramChat.State = (int) ConversationState.ServantList;
        }

        [ChatStateFilter((int) ConversationState.SupportList), NoCommandFilter, MessageTypeFilter(MessageType.Photo)]
        public async Task SupportList()
        {
            Console.WriteLine("Ricevuta foto");
            await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, $"Ok, ora inviami lo screen della lista dei tuoi servant o /skip se vuoi saltare questa fase");
            TelegramChat["support_photo"] = Update.Message.Photo[0].FileId;
            TelegramChat.State = (int) ConversationState.ServantList;
        }
        
        [ChatStateFilter((int)ConversationState.ServantList), NoCommandFilter, MessageTypeFilter(MessageType.Photo)]
        public async Task ServantList()
        {
            Console.WriteLine("Ricevuta foto, creazione Master e inserimento");
            await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, $"Ok, Master creato\nOra lo puoi collevare alle varie chat con il comando \\link " + TelegramChat["nome"]);
            var master = new Master(TelegramChat, TelegramChat["nome"], TelegramChat["friend_code"], (MasterServer) Int32.Parse(TelegramChat["server"]), TelegramChat["support_photo"],
                Update.Message.Photo[0].FileId);
            TelegramContext.Add(master);
            TelegramChat.State = (int)ConversationState.Idle;
            TelegramChat.Data.Clear();
        }
        
        [ChatStateFilter((int) ConversationState.ServantList), CommandFilter("skip"), MessageTypeFilter(MessageType.Text)]
        public async Task SkipServantList() {
            await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Hai saltato l'assegnazione della servant list\nOk, Master creato\nOra lo puoi collevare alle varie chat con il comando \\link " + TelegramChat["nome"]);
            var master = new Master(TelegramChat, TelegramChat["nome"], TelegramChat["friend_code"], (MasterServer) Int32.Parse(TelegramChat["server"]), TelegramChat["support_photo"],
                null);
            TelegramContext.Add(master);
            TelegramChat.State = (int)ConversationState.Idle;
            TelegramChat.Data.Clear();
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
                    
                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                        "Master cancellato correttamente");
                }

            }
        }

        [CommandFilter("link"), ChatTypeFilter(ChatType.Group)]
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
                        await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                            "Master collegato correttamente"); 
                    }
                    else {
                        await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                            "Master già collegato"); 
                    }
                }
            }
        }

        // Needed to use multiple ChatType
        [CommandFilter("link"), ChatTypeFilter(ChatType.Supergroup)]
        public async Task LinkMasterSuperGroup() {
            await LinkMaster();
        }
        
        [CommandFilter("master"), ChatTypeFilter(ChatType.Group)]
        public async Task ShowMaster() {
            if (MessageCommand.Parameters.Count < 1) {
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                    "Devi passarmi il nome del master che vuoi inviare");
            }
            else {
                var master = TelegramContext.Masters
                    .Include(m => m.RegisteredChats)
                    .Include(m => m.User).SingleOrDefault(m =>m.Name == MessageCommand.Parameters.Join(" "));
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

        [CommandFilter("master"), ChatTypeFilter(ChatType.Supergroup)]
        public async Task ShowMasterSupergroup() {
            await ShowMaster();
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
