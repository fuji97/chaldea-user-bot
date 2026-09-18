using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Server.DbContext;
using Server.Security;
using Telegram.Bot;
using Telegram.Bot.Advanced.Core.Holder;
using Telegram.Bot.Advanced.Extensions;

namespace Server.Controllers;

[ApiController]
[Route("admin")]
[Authorize(AuthenticationSchemes = AdminApiAuthentication.Scheme)]
public sealed class AdminController(ITelegramHolder holder, IOptions<TelegramWebhookOptions> webhookOptions, MasterContext context) : ControllerBase {
    [HttpPost("webhook/{endpoint}")]
    public async Task<IActionResult> SetWebhook([FromRoute] string endpoint) {
        if (!holder.TryGet(endpoint, out var bot)) {
            return NotFound();
        }

        var options = webhookOptions.Value;
        if (options.BaseUri is null || string.IsNullOrEmpty(options.SecretToken)) {
            return Conflict("Webhook transport is not configured.");
        }

        var url = WebhookUrlBuilder.Build(options.BaseUri, bot.BasePath, bot.Endpoint);
        await bot.Bot.SetWebhook(url.AbsoluteUri, secretToken: options.SecretToken, cancellationToken: HttpContext.RequestAborted);
        return Ok(url.AbsoluteUri);
    }

    [HttpDelete("webhook/{endpoint}")]
    public async Task<IActionResult> RemoveWebhook([FromRoute] string endpoint) {
        if (!holder.TryGet(endpoint, out var bot)) {
            return NotFound();
        }

        await bot.Bot.DeleteWebhook(cancellationToken: HttpContext.RequestAborted);
        return Ok();
    }

    [HttpPost("migrate")]
    public async Task<IActionResult> ApplyMigration() {
        await context.Database.MigrateAsync(HttpContext.RequestAborted);
        return Ok();
    }
}
