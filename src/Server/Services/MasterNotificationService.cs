using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Server.DbContext;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;

namespace Server.Services;

public sealed class MasterNotificationService(MasterContext context, ILogger<MasterNotificationService> logger) {
    public async Task SendSupportListUpdateAsync(ITelegramBotClient bot, int masterId, string html, CancellationToken cancellationToken) {
        var recipients = await GetRecipientsAsync(masterId, s => s.SupportListNotifications, cancellationToken);
        await SendToAllAsync(bot, recipients, html, cancellationToken);
    }

    public async Task SendServantListUpdateAsync(ITelegramBotClient bot, int masterId, string html, CancellationToken cancellationToken) {
        var recipients = await GetRecipientsAsync(masterId, s => s.ServantListNotifications, cancellationToken);
        await SendToAllAsync(bot, recipients, html, cancellationToken);
    }

    private async Task SendToAllAsync(ITelegramBotClient bot, List<long> chatIds, string html, CancellationToken cancellationToken) {
        foreach (var chatId in chatIds) {
            try {
                await bot.SendMessage(chatId, html, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
            }
            catch (ApiRequestException e) {
                logger.LogWarning(e, "Failed to send notification to chat {ChatId}", chatId);
            }
        }
    }

    private Task<List<long>> GetRecipientsAsync(int masterId, System.Linq.Expressions.Expression<System.Func<ChatSettings, bool>> notificationFlag, CancellationToken cancellationToken) =>
        context.RegisteredChats
            .Where(rc => rc.MasterId == masterId)
            .Join(context.ChatSettings.Where(notificationFlag),
                rc => rc.ChatId, s => s.Id, (rc, s) => rc.ChatId)
            .ToListAsync(cancellationToken);
}
