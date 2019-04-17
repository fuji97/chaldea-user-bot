using Telegram.Bot.Advanced.Dispatcher.Filters;
using Telegram.Bot.Advanced.Holder;
using Telegram.Bot.Advanced.Models;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Server.Filters {
    public class InlineFilter : DispatcherFilterAttribute {
        public override bool IsValid(Update update, TelegramChat chat, MessageCommand command, ITelegramBotData botData) {
            return update.Type == UpdateType.InlineQuery;
        }
    }
}