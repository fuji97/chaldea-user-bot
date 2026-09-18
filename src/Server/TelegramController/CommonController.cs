using System.Threading.Tasks;
using FgoData;
using Microsoft.Extensions.Logging;
using Server.Services;
using Telegram.Bot.Advanced.Core.Dispatcher.Filters;
using Telegram.Bot.Types.Enums;

namespace Server.TelegramController;

public sealed class CommonController(ILogger<CommonController> logger, IServantCatalog servants) : ChaldeaController(logger) {
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
                                    "/update &lt;nome&gt; - Aggiorna la support list da Rayshift.io (se abilitato)\n" +
                                    "/rayshift - Durante la registrazione, usa Rayshift.io per la support list\n" +
                                    "/skip - Durante la registrazione, salta la fase corrente\n" +
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
                                    "/servant &lt;nome&gt; - Cerca le informazioni di un Servant\n" +
                                    "/help - Mostra questo messaggio\n" +
                                    "\n" +
                                    "Bot creato da @fuji97\n" +
                                    "Per segnalare errori o proporre miglioramenti visitare la sezione <a href=\"https://github.com/fuji97/chaldea-user-bot/issues\">Issues su GitHub</a>",
            parseMode: ParseMode.Html);
    }

    [CommandFilter("reset")]
    public async Task ResetState() {
        TelegramChat!.State = null;
        TelegramChat.Data.Clear();
        if (await SaveChangesAsync()) {
            await ReplyTextMessageAsync("Stato resettato correttamente");
        }
    }

    [CommandFilter("servant")]
    public async Task GetServant() {
        if (string.IsNullOrWhiteSpace(MessageCommand.Message)) {
            await ReplyTextMessageAsync("Devi inviarmi il nome del Servant insieme al comando");
            return;
        }

        var servant = await servants.FindServantAsync(MessageCommand.Message, CancellationToken);
        if (servant == null) {
            await ReplyTextMessageAsync("Servant non trovato");
            return;
        }

        var imageAnchor = servant.ImageUrl != null ? $"<a href='{TelegramHtml.Escape(servant.ImageUrl)}'>&#8205;</a>" : "";
        var comment = servant.Comment != null ? $"Commenti:\n<i>{TelegramHtml.Escape(servant.Comment)}</i>\n\n" : "";

        await ReplyTextMessageAsync(imageAnchor +
                                    $"<b>{TelegramHtml.Escape(servant.Name)}</b>\n" +
                                    $"{TelegramHtml.Escape(servant.ClassName)} [{servant.Rarity}★]\n" +
                                    $"ATK: Base: <b>{servant.AtkBase}</b> - Max: <b>{servant.AtkMax}</b>\n" +
                                    $"HP: Base: <b>{servant.HpBase}</b> - Max: <b>{servant.HpMax}</b>\n" +
                                    $"Deck: <b>{servant.Deck}</b>\n" +
                                    $"Noble Phantasm: <b>{TelegramHtml.Escape(servant.NpType)}</b>\n\n" +
                                    comment +
                                    $"<a href='{TelegramHtml.Escape(servant.DbUrl)}'>{TelegramHtml.Escape(servant.Name)} su Atlas Academy</a>",
            parseMode: ParseMode.Html);
    }
}
