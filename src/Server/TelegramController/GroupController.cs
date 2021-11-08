using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Rayshift.Utils;
using Server.DbContext;
using Telegram.Bot.Advanced.Core.Dispatcher.Filters;
using Telegram.Bot.Advanced.Core.Tools;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Server.TelegramController {
    [ChatTypeFilter(ChatType.Group, ChatType.Supergroup)]
    public class GroupController : Controller {
        private ILogger<InlineController> _logger;

        private ChatSettings _cachedChatSettings = null;

        public GroupController(IMemoryCache cache, IConfiguration configuration, ILogger<InlineController> logger) : base(logger, cache, configuration) {
            _logger = logger;
        }

        [CommandFilter("list"), MessageTypeFilter(MessageType.Text)]
        public async Task ListMastersInGroups() {
            _logger.LogInformation("Ricevuto comando /list in un gruppo");
            var masters = TelegramContext.RegisteredChats.Where(rc => rc.ChatId == TelegramChat.Id)
                .Include(rc => rc.Master)
                .ThenInclude(m => m.User)
                .Select(rc => $"{rc.Master.Name} by <a href=\"tg://user?id={rc.Master.UserId}\">@{rc.Master.User.Username}</a>");
            await ReplyTextMessageAsync(
                "<b>Lista dei Master registrati:</b>\n" +
                string.Join("\n", masters),
                ParseMode.Html,
                disableNotification: true);
        }

        [CommandFilter("link")]
        public async Task LinkMaster() {
            if (MessageCommand.Parameters.Count < 1) {
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                    "Devi passarmi il nome del master che vuoi collegare");
            }
            else {
                var master = TelegramContext.Masters.FirstOrDefault(m =>
                    m.User.Id == Update.Message.From.Id && m.Name == MessageCommand.Parameters.JoinStrings(" "));

                if (master == null) {
                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                        "Nessun Master trovato con il nome " + MessageCommand.Parameters.JoinStrings(" "));
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

        [CommandFilter("unlink")]
        public async Task UnlinkMaster() {
            if (MessageCommand.Parameters.Count < 1) {
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                    "Devi passarmi il nome del master che vuoi scollegare");
            }

            var name = MessageCommand.Parameters.JoinStrings(" ");
            
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
                    if (!await IsSenderAdmin()) {
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

        [CommandFilter("master")]
        public async Task ShowMasterGroups() {
            if (MessageCommand.Parameters.Count < 1) {
                _logger.LogDebug("Ricevuto comando /master senza parametri");
                await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                    "Devi passarmi il nome del master che vuoi mostrare");
            }
            else {
                var master = TelegramContext.Masters
                    .Include(m => m.RegisteredChats)
                    .Include(m => m.User).SingleOrDefault(m => m.Name == MessageCommand.Parameters.JoinStrings(" "));
                if (master == null || master.RegisteredChats.All(c => c.ChatId != TelegramChat.Id)) {
                    await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                        "Nessun Master trovato con il nome " + MessageCommand.Parameters.JoinStrings(" "));
                }
                else {
                    await SendMaster(master);
                }
            }
        }

        [CommandFilter("settings")]
        public async Task GroupSettings() {
            var settings = await GetChatSettings();
            
            _logger.LogInformation("Ricevuto comando /settings in un gruppo");
            if (await IsSenderAdmin()) {
                await ReplyTextMessageAsync(BuildSettingsMessage(settings), replyMarkup: BuildSettingsKeyboard(settings));
            }
        }

        #region Comandi inline

        [CallbackCommandFilter(InlineKeyboardCommands.EnableSupportListNotifications, 
            InlineKeyboardCommands.DisableSupportListNotifications,
            InlineKeyboardCommands.EnableServantListNotifications, 
            InlineKeyboardCommands.DisableServantListNotifications)]
        public async Task SettingsCallback() {
            await BotData.Bot.AnswerCallbackQueryAsync(Update.CallbackQuery.Id);
            
            var originalMessage = Update.CallbackQuery.Message;
            if (await IsUserAdmin(TelegramChat.Id, Update.CallbackQuery.From.Id)) {

                var settings = await GetChatSettings();

                switch (InlineDataWrapper.ParseInlineData(Update.CallbackQuery.Data).Command) {
                    case InlineKeyboardCommands.EnableSupportListNotifications:
                        settings.SupportListNotifications = true;
                        break;
                    case InlineKeyboardCommands.DisableSupportListNotifications:
                        settings.SupportListNotifications = false;
                        break;
                    case InlineKeyboardCommands.EnableServantListNotifications:
                        settings.ServantListNotifications = true;
                        break;
                    case InlineKeyboardCommands.DisableServantListNotifications:
                        settings.ServantListNotifications = false;
                        break;
                }
                
                if (await SaveChanges()) {
                    await BotData.Bot.EditMessageTextAsync(TelegramChat.Id, originalMessage.MessageId, 
                        BuildSettingsMessage(settings), replyMarkup: BuildSettingsKeyboard(settings));
                }
            }
        }

        #endregion

        private string BuildSettingsMessage(ChatSettings settings) {
            var message = $"Impostazioni del gruppo {TelegramChat.Title}:\n\n" +
                          "Notifiche aggiornamenti:\n" +
                          $"Support list: {(settings.SupportListNotifications ? "abilitate" : "disabilitate")}\n" +
                          $"Servant list: {(settings.ServantListNotifications ? "abilitate" : "disabilitate")}";

            return message;
        }

        private InlineKeyboardMarkup BuildSettingsKeyboard(ChatSettings settings) {

            var supportListNotifications = new InlineKeyboardButton();
            if (settings.SupportListNotifications) {
                supportListNotifications.Text = "Disabilita notifiche support list";
                supportListNotifications.CallbackData =
                    new InlineDataWrapper(InlineKeyboardCommands.DisableSupportListNotifications).ToString();
            }
            else {
                supportListNotifications.Text = "Abilita notifiche support list";
                supportListNotifications.CallbackData =
                    new InlineDataWrapper(InlineKeyboardCommands.EnableSupportListNotifications).ToString();
            }
            
            var servantListNotifications = new InlineKeyboardButton();
            if (settings.ServantListNotifications) {
                servantListNotifications.Text = "Disabilita notifiche servant list";
                servantListNotifications.CallbackData =
                    new InlineDataWrapper(InlineKeyboardCommands.DisableServantListNotifications).ToString();
            }
            else {
                servantListNotifications.Text = "Abilita notifiche servant list";
                servantListNotifications.CallbackData =
                    new InlineDataWrapper(InlineKeyboardCommands.EnableServantListNotifications).ToString();
            }

            var keyboard = new InlineKeyboardMarkup(new [] {
                supportListNotifications, servantListNotifications
            });

            return keyboard; 
        }

        private async Task<ChatSettings> GetChatSettings() {
            if (_cachedChatSettings == null) {
                var chatSettings =
                    await TelegramContext.ChatSettings.FirstOrDefaultAsync(cs => cs.Id == TelegramChat.Id);
                if (chatSettings != null) {
                    _cachedChatSettings = chatSettings;
                }
                else {
                    _cachedChatSettings = new ChatSettings() {
                        Id = TelegramChat.Id
                    };
                    await TelegramContext.AddAsync(_cachedChatSettings);
                    await SaveChanges();

                    _cachedChatSettings = await TelegramContext.ChatSettings.FindAsync(_cachedChatSettings.Id);
                }
            }

            return _cachedChatSettings;
        }

        private static class InlineKeyboardCommands {
            public const string EnableSupportListNotifications = "EnableSupportListNotifications";
            public const string DisableSupportListNotifications = "DisableSupportListNotifications";
            public const string EnableServantListNotifications = "EnableServantListNotifications";
            public const string DisableServantListNotifications = "DisableServantListNotifications";
        }
    }
}