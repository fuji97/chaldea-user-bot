using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Server.DbContext;
using Telegram.Bot.Advanced.Core.Holder;

namespace Server.Controllers {
    [ApiController]
    [Route("[controller]")]
    public class AdminController(ITelegramHolder holder, IConfiguration configuration) : ControllerBase {
        // GET
        [HttpGet("set_webhook/{endpoint}")]
        public async Task<IActionResult> SetWebhook([FromRoute] string endpoint) {
            List<string> webhooks = new List<string>();
            var bot = holder.FirstOrDefault(b => b.Endpoint == endpoint);
            
            if (bot == null) 
                return Ok(webhooks);
            
            await bot.Bot.SetWebhookAsync(configuration["BaseUrl"] + configuration["BasePath"] +
                                          bot.Endpoint);
            webhooks.Add((await bot.Bot.GetWebhookInfoAsync()).Url);
            return Ok(webhooks);
        }
        
        [HttpGet("remove_webhook/{endpoint}")]
        public async Task<IActionResult> RemoveWebhook([FromRoute] string endpoint) {
            var bot = holder.FirstOrDefault(b => b.Endpoint == endpoint);
            if (bot != null) {
                await bot.Bot.DeleteWebhookAsync();
            }
            return Ok("Done");
        }

        [HttpGet("migrate")]
        public async Task<IActionResult> ApplyMigration([FromServices] MasterContext context) {
            await context.Database.MigrateAsync();
            return Ok("Done");
        }
    }
}