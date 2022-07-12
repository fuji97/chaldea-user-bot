using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using DataScraper;
using DataScraper.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Rayshift;
using Rayshift.Models;
using Server.DbContext;
using Telegram.Bot.Advanced.Controller;
using Telegram.Bot.Advanced.Core.Dispatcher.Filters;
using Telegram.Bot.Advanced.Core.Tools;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Server.TelegramController
{
    
    public class Controller : TelegramController<MasterContext> {
        private readonly ILogger<Controller> _logger;
        protected readonly IMemoryCache Cache;
        protected readonly IConfiguration Configuration;
        protected readonly IRayshiftClient RayshiftClient;

        public Controller(ILogger<Controller> logger, IMemoryCache cache, IConfiguration configuration, IRayshiftClient rayshiftClient) {
            _logger = logger;
            Cache = cache;
            Configuration = configuration;
            RayshiftClient = rayshiftClient;
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
                                        "da poter inviare velocemente il Master in qualsiasi momento.\n" +
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
                                        "/settings - [SOLO ADMIN] Apre le impostazioni per il gruppo\n" +
                                        "/reset - [SOLO ADMIN] - Resetta lo stato del bot e cancella i dati temporanei (non cancella i Master collegati), da usare solo se il bot da errori ESPLICITI; da NON usare se semplicemente non risponde\n" +
                                        "\n" +
                                        "[OVUNQUE]\n" +
                                        "/help - Mostra questo messaggio\n" +
                                        "\n" +
                                        "Bot creato da @fuji97\n" +
                                        "Per segnalare errori o proporre miglioramenti visitare la sezione <a href=\"https://github.com/fuji97/chaldea-user-bot/issues\">Issues su GitHub</a>",
                ParseMode.Html);
        }

        [CommandFilter("reset")]
        public async Task ResetState() {
            if (Update.Message.Chat.Type == ChatType.Group || Update.Message.Chat.Type == ChatType.Supergroup) {
                if (!await IsSenderAdmin()) {
                    await ReplyTextMessageAsync(
                        "Solo gli admin possono resettare lo stato di Chaldea in un gruppo");
                    return;
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

            List<ServantEntry> servants = await Cache.GetOrCreateAsync("servants", entry => {
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
            var loadingMessage = await ReplyTextMessageAsync("Caricamento delle informazioni da Rayshift, attendere...");
            
            if (master.UseRayshift) {
                try {
                    var supportList = await GetSupportImageFromRayshift(ServerToRegion(master.Server), master.FriendCode);

                    if (supportList != null) {
                        album.Add(new InputMediaPhoto(new InputMedia(supportList)));
                    }
                    else {
                        _logger.LogError("Errore nell'ottenere la support list di {master} da rayshift.io", master.ToString());
                        await BotData.Bot.EditMessageTextAsync(TelegramChat.ToChatId(), loadingMessage.MessageId, "Errore nell'ottenere la support list da Rayshift.io");
                    }
                } catch (NullReferenceException) {
                    _logger.LogError("{master} non trovato su rayshift.io", master.ToString());
                    await BotData.Bot.EditMessageTextAsync(TelegramChat.ToChatId(), loadingMessage.MessageId, "Master non trovato su Rayshift.io");
                }
            } else if (master.SupportList != null) {
                album.Add(new InputMediaPhoto(new InputMedia(master.SupportList)));
            }

            if (master.ServantList != null) {
                album.Add(new InputMediaPhoto(new InputMedia(master.ServantList)));
            }

            if (album.Any()) {
                await BotData.Bot.SendMediaGroupAsync(TelegramChat.Id, album);
            }

            var messageText = $"<b>Master:</b> {master.Name}\n" +
                              $"<b>Friend Code:</b> {master.FriendCode}\n" +
                              $"<b>Old.Server:</b> {master.Server.ToString()}\n" +
                              $"<b>Registrato da:</b> <a href=\"tg://user?id={master.UserId}\">@{master.User.Username}</a>";

            if (master.UseRayshift) {
                messageText += $"\n\n<a href=\"{BuildRayshiftUrl(master)}\">Rayshift.io</a>";
            }

            await BotData.Bot.SendTextMessageAsync(TelegramChat.Id, messageText, ParseMode.Html, 
                disableWebPagePreview: true);
            await BotData.Bot.DeleteMessageAsync(TelegramChat.ToChatId(), loadingMessage.MessageId);
        }

        protected Uri BuildRayshiftUrl(Master master) {
            var uriBuilder =
                new UriBuilder(Rayshift.RayshiftClient.BaseAddress) {
                    Path = $"{ServerToString(master.Server)}/{master.FriendCode}"
                };

            return uriBuilder.Uri;
        }

        protected string ServerToString(MasterServer server) {
            return server switch {
                MasterServer.Jp => "jp",
                MasterServer.Na => "na",
                _ => throw new ArgumentOutOfRangeException(nameof(server), server, null)
            };
        }

        protected async Task<string> GetSupportImageFromRayshift(Region region, string friendCode) {
            var master = (await RayshiftClient.GetSupportDeck(region, friendCode))?.Response;

            if (master == null) {
                throw new NullReferenceException("No Master found.");
            }
            
            return master.SupportList(region);
            
        }
        
        protected async Task<Stream> GetImageStream(Uri url) {
            using HttpClient client = new HttpClient();
            return await client.GetStreamAsync(url);
        }
        
        protected static Region ServerToRegion(MasterServer server) {
            Region region = Region.Na;
            switch (server) {
                case MasterServer.Jp:
                    region = Region.Jp;
                    break;
                case MasterServer.Na:
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

        protected async Task<Master> GetMasterFromCallbackData() {
            if (Update?.CallbackQuery?.Data == null || Update.CallbackQuery.Message?.From?.Id == null) {
                return null;
            }

            var masterName = InlineDataWrapper.ParseInlineData(Update.CallbackQuery.Data).Data["master"];
            if (masterName == null) {
                return null;
            }

            var master =
                await TelegramContext.Masters.FirstOrDefaultAsync(m =>
                    m.Name == masterName && m.UserId == Update.CallbackQuery.Message.Chat.Id);

            return master;
        }
        
        protected async Task<bool> IsSenderAdmin() {
            return await IsUserAdmin(TelegramChat.Id, Update.Message.From.Id);
        }

        protected async Task<bool> IsUserAdmin(long chatId, long userId) {
            return (await BotData.Bot.GetChatAdministratorsAsync(chatId))
                .Any(ua => ua.User.Id == userId);
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
