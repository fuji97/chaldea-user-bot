using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DataScraper;
using DataScraper.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Rayshift;
using Rayshift.Models;
using Server.DbContext;
using Telegram.Bot.Advanced.Controller;
using Telegram.Bot.Advanced.Core.Dispatcher.Filters;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace Server.TelegramController
{
    
    public class Controller : TelegramController<MasterContext> {
        private readonly ILogger _logger;
        protected IMemoryCache _cache;
        protected IConfiguration _configuration;

        public Controller(ILogger logger, IMemoryCache cache, IConfiguration configuration) {
            _logger = logger;
            _cache = cache;
            _configuration = configuration;
        }

        public Controller() {
        }

        [CommandFilter("help")]
        public async Task Help() {
            await ReplyTextMessageAsync("<b>Chaldea Bot</b>\n\n" +
                                        "Benvenuto nel bot dedicato a Fate/Grand Order.\n" +
                                        "Attualmente questo bot può permettervi di registrare un Master (in privato) " +
                                        "inviandomi le sue informazioni. Dopo che è stato registrato correttamente " +
                                        "potete collegarlo in una qualsasi chat in cui è presente questo bot in modo " +
                                        "da poter inviare velocemente il Master in qualsiasi momento\n" +
                                        "\n" +
                                        "<b>Lista dei comandi:</b>\n" +
                                        "[IN PRIVATO]\n" +
                                        "/add &lt;nome?&gt; - Registra un nuovo Master\n" +
                                        "/list - Mostra una lista di tutti i tuoi Master\n" +
                                        "/master &lt;nome&gt; - Visualizza le informazioni di un tuo Master\n" +
                                        "/remove &lt;nome&gt; - Cancella il Master\n" +
                                        "/support_list &lt;nome&gt; - Aggiorna la support list del Master\n" +
                                        "/servant_list &lt;nome&gt; - Aggiorna la servant list del Master\n" +
                                        "/reset - Resetta lo stato del bot e cancella i dati temporanei (non cancella i Master registrati), da usare solo se il bot da errori ESPLICITI; da NON usare se semplicemente non risponde\n" +
                                        "\n" +
                                        "[NEI GRUPPI]\n" +
                                        "/list - Mostra una lista di tutti i Master registrati nel gruppo\n" +
                                        "/link &lt;nome&gt; - Collega il Master alla chat, in modo che possa essere visualizzato con il comando /master\n" +
                                        "/master &lt;nome&gt; - Visualizza le informazioni del Master (se è collegato)\n" +
                                        "/unlink &lt;nome&gt; - Scollega il Master (gli admin possono scollegare qualsiasi Master)\n" +
                                        "/reset - [SOLO ADMIN] - Resetta lo stato del bot e cancella i dati temporanei (non cancella i Master collegati), da usare solo se il bot da errori ESPLICITI; da NON usare se semplicemente non risponde\n" +
                                        "\n" +
                                        "[OVUNQUE]\n" +
                                        "/help - Mostra questo messaggio\n" +
                                        "\n" +
                                        "Bot creato da @fuji97",
                ParseMode.Html);
        }

        [CommandFilter("reset")]
        public async Task ResetState() {
            if (Update.Message.Chat.Type == ChatType.Group || Update.Message.Chat.Type == ChatType.Supergroup) {
                if (!Update.Message.Chat.AllMembersAreAdministrators) {
                    if ((await BotData.Bot.GetChatAdministratorsAsync(TelegramChat.Id))
                        .All(cm => cm.User != Update.Message.From)) {

                        await ReplyTextMessageAsync(
                            "Solo gli admin possono resettare lo stato di Chaldea in un gruppo");
                        return;
                    }
                }
            }

            TelegramChat.State = ConversationState.Idle;
            TelegramChat.Data.Clear();

            if (await SaveChanges("Reset Impossibile, contattate un admin di Chaldea per avere supporto diretto")) {
                await ReplyTextMessageAsync("Reset completato con successo");
            }
        }

        [CommandFilter("servant")]
        public async Task GetServant() {
            if (MessageCommand.Parameters.Count < 1) {
                await ReplyTextMessageAsync("Devi inviarmi il nome del Servant insieme al comando");
                return;
            }

            List<ServantEntry> servants = await _cache.GetOrCreateAsync("servants", entry => {
                entry.SlidingExpiration = TimeSpan.FromHours(1);
                var scraper = new Scraper();
                return scraper.GetAllServants();
            });
            
            var servant = servants.FirstOrDefault(x => x.Name.IndexOf(MessageCommand.Message, StringComparison.OrdinalIgnoreCase) >= 0);
            if (servant != null) {
                await ReplyTextMessageAsync($"<a href='{servant.ImageUrl}'>&#8205;</a>" +
                                      $"<b>{servant.Name}</b>\n" +
                                      $"{servant.Class} [{(int) servant.Stars}★]\n" +
                                      $"ATK: Base: <b>{servant.BaseAttack}</b> - Max: <b>{servant.MaxAttack}</b>\n" +
                                      $"HP: Base: <b>{servant.BaseHp}</b> - Max: <b>{servant.MaxHp}</b>\n" +
                                      $"Deck: <b>{servant.GetDeckString()}</b>\n" +
                                      $"Noble Phantasm: <b>{servant.NpType}</b>\n\n" +
                                      $"Commenti:\n" +
                                      $"<i>{servant.Comments}</i>\n\n" +
                                      $"<a href='{servant.ServantUrl}'>{servant.Name} su Cirnopedia</a>",
                    ParseMode.Html, true);
            }
            else {
                await ReplyTextMessageAsync("Servant non trovato");
            }
        }
        
        protected async Task SendMaster(Master master) {
            var album = new List<IAlbumInputMedia>();
            
            if (master.UseRayshift) {
                var supportList = await GetSupportImageFromRayshift(ServerToRegion(master.Server), master.FriendCode);
                
                if (supportList != null) {
                    album.Add(new InputMediaPhoto(new InputMedia(supportList[SupportListType.Normal])));
                    album.Add(new InputMediaPhoto(new InputMedia(supportList[SupportListType.Event])));
                }
                else {
                    _logger.LogError("Errore nell'ottenere la support list di {master} da rayshift.io", master.ToString());
                    await ReplyTextMessageAsync("Errore nell'ottenere la support list da Rayshift.io");
                }
            } else if (master.SupportList != null) {
                album.Add(new InputMediaPhoto(new InputMedia(master.SupportList)));
            }

            if (master.ServantList != null) {
                album.Add(new InputMediaPhoto(new InputMedia(master.ServantList)));
            }

            await BotData.Bot.SendMediaGroupAsync(album, TelegramChat.Id);

            await BotData.Bot.SendTextMessageAsync(TelegramChat.Id,
                $"<b>Master:</b> {master.Name}\n" +
                $"<b>Friend Code:</b> {master.FriendCode}\n" +
                $"<b>Server:</b> {master.Server.ToString()}\n" +
                $"<b>Registrato da:</b> <a href=\"tg://user?id={master.UserId}\">@{master.User.Username}</a>",
                ParseMode.Html);
        }

        protected async Task<Dictionary<SupportListType,string>> GetSupportImageFromRayshift(Region region, string friendCode) {
            Dictionary<SupportListType, string> images = null;
            using (var client = new RayshiftClient(_configuration["ApiKey"])) {
                var master = (await client.GetSupportDeck(region, friendCode))?.Response;
                if (master != null) {
                    images = new Dictionary<SupportListType, string>();
                    images[SupportListType.Normal] = master.SupportList(SupportListType.Normal);
                    images[SupportListType.Event] = master.SupportList(SupportListType.Event);
                    images[SupportListType.Both] = master.SupportList(SupportListType.Both);
                }

                return images;
            }
        }
        
        protected async Task<Stream> GetImageStream(Uri url) {
            using HttpClient client = new HttpClient();
            return await client.GetStreamAsync(url);
        }
        
        protected static Region ServerToRegion(MasterServer server) {
            Region region = Region.Na;
            switch (server) {
                case MasterServer.JP:
                    region = Region.Jp;
                    break;
                case MasterServer.US:
                    region = Region.Na;
                    break;
            }

            return region;
        }

        protected async Task<bool> SaveChanges(string text = "Errore nel salvare i dati, provare a reinviare l'ultimo messaggio") {
            bool error = false;
                try
                {
                    // save data
                    await TelegramContext.SaveChangesAsync();
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex.Message);
                    error = true;
                    await ReplyTextMessageAsync(text);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                    await ReplyTextMessageAsync(text);
                    throw;
                }
            return !error;
        }
    }

    public static class ConversationState {
        public const string Idle = null;
        public const string Nome = "ChaldeabotController_Nome";
        public const string FriendCode = "ChaldeabotController_FriendCode";
        public const string Server = "ChaldeabotController_Server";
        public const string SupportList = "ChaldeabotController_SupportList";
        public const string ServantList = "ChaldeabotController_ServantList";
        public const string UpdatingSupportList = "ChaldeabotController_UpdatingSupportList";
        public const string UpdatingServantList = "ChaldeabotController_UpdatingServantList";
        public const string WaitingRayshift = "ChaldeabotController_WaitingRayshift";
    }
}
