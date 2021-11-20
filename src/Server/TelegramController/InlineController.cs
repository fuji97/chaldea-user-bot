using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DataScraper;
using DataScraper.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Rayshift;
using Telegram.Bot.Advanced.Core.Dispatcher.Filters;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;

namespace Server.TelegramController {
    public class InlineController : Controller {
        private ILogger<InlineController> _logger;

        public InlineController(IMemoryCache cache, IConfiguration configuration, ILogger<InlineController> logger, IRayshiftClient rayshiftClient) : base(logger, cache, configuration, rayshiftClient) {
            _logger = logger;
        }

        [UpdateTypeFilter(UpdateType.InlineQuery)]
        public async Task InlineRequest() {
            List<InlineQueryResultBase> results = new List<InlineQueryResultBase>();
            
            if (Update.InlineQuery.Query.Length < 3) {
                results.Add(new InlineQueryResultArticle(
                    "1",
                    "Pochi caratteri",
                    new InputTextMessageContent("")));
                await BotData.Bot.AnswerInlineQueryAsync(
                    Update.InlineQuery.Id,
                    results);
                return;
            }
            
            List<ServantEntry> servants = await Cache.GetOrCreateAsync("servants", entry => {
                entry.SlidingExpiration = TimeSpan.FromHours(1);
                var scraper = new Scraper();
                return scraper.GetAllServants();
            });
            
            var validServants = servants.Where(x => x.Name.IndexOf(Update.InlineQuery.Query, StringComparison.OrdinalIgnoreCase) >= 0);

            if (!validServants.Any()) {
                results.Add(new InlineQueryResultArticle(
                    "2",
                    "Nessun Servant trovato",
                    new InputTextMessageContent("")));
                await BotData.Bot.AnswerInlineQueryAsync(
                    Update.InlineQuery.Id,
                    results);
                return;
            }

            foreach (var servant in validServants) {
                
                results.Add(new InlineQueryResultArticle(
                    servant.Id,
                    servant.Name,
                    new InputTextMessageContent($"<a href='{servant.ImageUrl}'>&#8205;</a>" +
                                                $"<b>{servant.Name}</b>\n" +
                                                $"{servant.Class} [{(int) servant.Stars}★]\n" +
                                                $"ATK: Base: <b>{servant.BaseAttack}</b> - Max: <b>{servant.MaxAttack}</b>\n" +
                                                $"HP: Base: <b>{servant.BaseHp}</b> - Max: <b>{servant.MaxHp}</b>\n" +
                                                $"Deck: <b>{servant.GetDeckString()}</b>\n" +
                                                $"Noble Phantasm: <b>{servant.NpType}</b>\n\n" +
                                                $"Commenti:\n" +
                                                $"<i>{servant.Comments}</i>\n\n" +
                                                $"<a href='{servant.ServantUrl}'>{servant.Name} su Cirnopedia</a>"
                    ) {
                        ParseMode = ParseMode.Html,
                        DisableWebPagePreview = false
                    }
                ) {
                    Description = $"{servant.Class} [{(int) servant.Stars}★]\nDeck: {servant.GetDeckString()}",
                    ThumbUrl = servant.IconUrl
                });
            }
            
            await BotData.Bot.AnswerInlineQueryAsync(
                Update.InlineQuery.Id,
                results);
        }
    }
}