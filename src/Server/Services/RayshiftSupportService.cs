using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Rayshift;
using Rayshift.Models;
using Server.DbContext;
using Server.Exceptions;

namespace Server.Services;

public sealed class RayshiftSupportService(IRayshiftClient rayshift, HttpClient http, ILogger<RayshiftSupportService> logger) {
    public static Region ServerToRegion(MasterServer server) => server switch {
        MasterServer.Jp => Region.Jp,
        MasterServer.Na => Region.Na,
        _ => throw new ArgumentOutOfRangeException(nameof(server), server, null)
    };

    public async Task<ApiMaster> EnableAsync(Master master, CancellationToken cancellationToken) {
        var response = await RequestSupportListAsync(ServerToRegion(master.Server), master.FriendCode, cancellationToken);
        master.UseRayshift = true;
        master.SupportList = null;
        return response;
    }

    public Task<ApiMaster> RefreshAsync(Master master, CancellationToken cancellationToken) =>
        RequestSupportListAsync(ServerToRegion(master.Server), master.FriendCode, cancellationToken);

    public async Task<ApiMaster> GetSupportDeckAsync(Master master, CancellationToken cancellationToken) {
        var response = await rayshift.GetSupportDeckAsync(ServerToRegion(master.Server), master.FriendCode, cancellationToken);
        if (response.Status != 200 || response.Response is null) {
            throw new RayshiftUnavailableException("Rayshift did not return a support deck.");
        }

        return response.Response;
    }

    public async Task<string> GetSupportImageUrlAsync(ApiMaster response, Region region, CancellationToken cancellationToken) {
        var url = response.SupportList(region);
        using var headResponse = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        headResponse.EnsureSuccessStatusCode();
        return url;
    }

    public async Task<ApiMaster> RequestSupportListAsync(Region region, string friendCode, CancellationToken cancellationToken) {
        var response = await rayshift.RequestSupportLookupAsync(region, friendCode, cancellationToken);
        if (response.Status != 200 || response.MessageType != MessageCode.Finished || response.Response is null) {
            logger.LogWarning("Rayshift lookup did not finish successfully (status={Status}, message={Message})", response.Status, response.Message);
            throw new RayshiftUnavailableException("Rayshift lookup did not finish successfully.");
        }

        return response.Response;
    }
}
