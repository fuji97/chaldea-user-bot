using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Server.DbContext;

namespace Server.Services;

public sealed class ChatSettingsService(MasterContext context, ILogger<ChatSettingsService> logger) {
    public async Task<ChatSettings> GetOrCreateAsync(long chatId, CancellationToken cancellationToken) {
        var settings = await context.ChatSettings.FindAsync([chatId], cancellationToken);
        if (settings != null) {
            return settings;
        }

        settings = new ChatSettings { Id = chatId };
        context.ChatSettings.Add(settings);

        try {
            await context.SaveChangesAsync(cancellationToken);
            return settings;
        }
        catch (DbUpdateException e) {
            logger.LogError(e, "Failed to create chat settings for chat {ChatId}", chatId);
            return null;
        }
    }
}
