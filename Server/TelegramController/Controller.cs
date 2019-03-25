using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
        [CommandFilter("add"), ChatTypeFilter(ChatType.Private), MessageTypeFilter(MessageType.Text)]
        public async Task Add() {
            Console.WriteLine("Ricevuto comando /add");
            if (MessageCommand.Parameters.Count < 1) {
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Ok, inviami il nome che vuoi usare");
                TelegramChat.State = (int) ConversationState.Nome;
            }
            else {
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Ok, inviami il friend code in formato XXXXXXXXX");
                TelegramChat.State = (int)ConversationState.FriendCode;
                TelegramChat["nome"] = MessageCommand.Parameters.Join(" ");
            }
        }

        [ChatStateFilter((int) ConversationState.Nome), NoCommandFilter, MessageTypeFilter(MessageType.Text)]
        public async Task GetNome() {
            Console.WriteLine($"Nome ricevuto da @{TelegramChat?.Username}: {Update.Message.Text}");
            if (TelegramChat != null) {
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, "Ok, adesso inviami il friend code in formato XXXXXXXXX");
                TelegramChat["nome"] = Update.Message.Text;
                TelegramChat.State = (int)ConversationState.FriendCode;
            }
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
            await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, $"Ok, Master creato");
            var master = new Master(TelegramChat, TelegramChat["nome"], TelegramChat["friend_code"], (MasterServer) Int32.Parse(TelegramChat["server"]), TelegramChat["support_photo"],
                Update.Message.Photo[0].FileId);
            TelegramContext.Add(master);
            TelegramChat.State = (int)ConversationState.Idle;
            TelegramChat.Data.Clear();
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
