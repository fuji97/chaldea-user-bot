using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Server.DbContext;
using Telegram.Bot.Advanced.Holder;

namespace Server.Controllers {
    [ApiController]
    [Route("[controller]")]
    public class AdminController : Controller {
        private readonly ITelegramHolder _holder;
        private readonly IConfiguration _configuration;
        

        public AdminController(ITelegramHolder holder, IConfiguration configuration) {
            _holder = holder;
            _configuration = configuration;
        }
        
        // GET
        [HttpGet("set_webhook")]
        public async Task<IActionResult> SetWebhook() {
            List<string> webhooks = new List<string>();
            foreach (var bot in _holder) {
                await bot.Bot.SetWebhookAsync(_configuration["BaseUrl"] + _configuration["BasePath"] +
                                              bot.Endpoint);
                webhooks.Add((await bot.Bot.GetWebhookInfoAsync()).Url);
            }
            return Ok(webhooks);
        }
        
        [HttpGet("remove_webhook")]
        public async Task<IActionResult> RemoveWebhook() {
            List<string> webhooks = new List<string>();
            foreach (var bot in _holder) {
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