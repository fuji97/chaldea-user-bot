using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Rayshift;
using Server.DbContext;
using Server.Exceptions;
using Telegram.Bot;
using Telegram.Bot.Advanced.DbContexts;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Server.Services;

public sealed class MasterDisplayService(RayshiftSupportService rayshiftSupport, ILogger<MasterDisplayService> logger) {
    public async Task ShowAsync(ITelegramBotClient bot, TelegramChat chat, Master master, CancellationToken cancellationToken) {
        var album = new List<IAlbumInputMedia>();
        var loadingMessage = await bot.SendMessage(chat.Id, "Caricamento delle informazioni da Rayshift, attendere...", cancellationToken: cancellationToken);

        if (master.UseRayshift) {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            try {
                var region = RayshiftSupportService.ServerToRegion(master.Server);
                var response = await rayshiftSupport.GetSupportDeckAsync(master, timeout.Token);
                var supportUrl = await rayshiftSupport.GetSupportImageUrlAsync(response, region, timeout.Token);
                album.Add(new InputMediaPhoto(new InputFileUrl(new Uri(supportUrl))));
            }
            catch (TaskCanceledException e) {
                logger.LogError(e, "Timeout while retrieving support list");
                await bot.SendMessage(chat.ToChatId(), "Timeout mentre ottengo la support list da Rayshift.io", cancellationToken: cancellationToken);
            }
            catch (RayshiftUnavailableException e) {
                logger.LogError(e, "{Master} non trovato su rayshift.io", master.Name);
                await bot.SendMessage(chat.ToChatId(), "Master non trovato su Rayshift.io", cancellationToken: cancellationToken);
            }
        }
        else if (master.SupportList != null) {
            album.Add(new InputMediaPhoto(new InputFileUrl(master.SupportList)));
        }

        if (master.ServantList != null) {
            album.Add(new InputMediaPhoto(new InputFileUrl(master.ServantList)));
        }

        if (album.Count > 0) {
            try {
                await bot.SendMediaGroup(chat.Id, album, cancellationToken: cancellationToken);
            }
            catch (ApiRequestException e) {
                logger.LogError(e, "Exception thrown while sending the support list album");
                await bot.SendMessage(chat.Id, "Errore di invio delle immagini della support list", cancellationToken: cancellationToken);
            }
        }

        var messageText = $"<b>Master:</b> {TelegramHtml.Escape(master.Name)}\n" +
                           $"<b>Friend Code:</b> {TelegramHtml.Escape(master.FriendCode)}\n" +
                           $"<b>Server:</b> {master.Server}\n" +
                           $"<b>Registrato da:</b> <a href=\"tg://user?id={master.UserId}\">@{TelegramHtml.Escape(master.User.Username)}</a>";

        if (master.UseRayshift) {
            messageText += $"\n\n<a href=\"{BuildRayshiftUrl(master)}\">Rayshift.io</a>";
        }

        await bot.SendMessage(chat.Id, messageText, parseMode: ParseMode.Html,
            linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true }, cancellationToken: cancellationToken);
        await bot.DeleteMessage(chat.ToChatId(), loadingMessage.MessageId, cancellationToken: cancellationToken);
    }

    private static Uri BuildRayshiftUrl(Master master) {
        var region = RayshiftSupportService.ServerToRegion(master.Server);
        var uriBuilder = new UriBuilder(RayshiftClient.BaseAddress) {
            Path = $"{Rayshift.Utils.Utils.StringRegion(region)}/{master.FriendCode}"
        };

        return uriBuilder.Uri;
    }
}
