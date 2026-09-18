using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Server.DbContext;
using Server.Services;
using Telegram.Bot;
using Telegram.Bot.Advanced.Controller;
using Telegram.Bot.Advanced.Core.Tools;

namespace Server.TelegramController;

public abstract class ChaldeaController(ILogger logger) : TelegramController<MasterContext> {
    protected async Task<bool> SaveChangesAsync(string errorText = "Errore nel salvare i dati, provare a reinviare l'ultimo messaggio") {
        try {
            await TelegramContext.SaveChangesAsync(CancellationToken);
            return true;
        }
        catch (DbUpdateException ex) {
            logger.LogError(ex, "{Message}", ex.Message);
            await ReplyTextMessageAsync(errorText, cancellationToken: CancellationToken);
            return false;
        }
        catch (Exception ex) {
            logger.LogError(ex, "{Message}", ex.Message);
            await ReplyTextMessageAsync(errorText, cancellationToken: CancellationToken);
            throw;
        }
    }

    protected async Task<bool> IsSenderAdminAsync() =>
        await IsUserAdminAsync(TelegramChat!.Id, Update.Message!.From!.Id);

    protected async Task<bool> IsUserAdminAsync(long chatId, long userId) =>
        (await BotData.Bot.GetChatAdministrators(chatId, cancellationToken: CancellationToken))
        .Any(ua => ua.User.Id == userId);

    protected async Task<Master> GetMasterFromCallbackAsync(MasterQueryService masters) {
        var data = Update?.CallbackQuery?.Data;
        var senderId = Update?.CallbackQuery?.Message?.Chat.Id;
        if (data == null || senderId == null) {
            return null;
        }

        if (!InlineDataWrapper.ParseInlineData(data).Data.TryGetValue("master_id", out var raw)
            || !int.TryParse(raw, out var masterId)) {
            return null;
        }

        return await masters.GetOwnedByIdAsync(masterId, senderId.Value, CancellationToken);
    }
}
