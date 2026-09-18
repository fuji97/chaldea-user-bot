using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
using Rayshift.Models;

namespace Rayshift;

public sealed partial class RayshiftClient : IRayshiftClient {
    public const string BaseAddress = "https://rayshift.io";
    public const string ImagesPath = "static/images/deck-gen/";
    public const string ApiBaseAddress = "https://rayshift.io/api/v1/";
    private const string SupportDecks = "support/decks/";
    private const string SupportLookup = "support/lookup/";

    private const string Finished = "finished";

    private readonly HttpClient _client;
    private readonly RayshiftOptions _options;
    private readonly ILogger<RayshiftClient>? _logger;

    public RayshiftClient(HttpClient client, RayshiftOptions options, ILogger<RayshiftClient>? logger = null) {
        _client = client;
        _options = options;
        _logger = logger;
    }

    public async Task<ApiResponse> GetSupportDeckAsync(Region region, string friendCode, CancellationToken cancellationToken) {
        ValidateFriendCode(friendCode);
        var regionStr = Utils.Utils.StringRegion(region);

        var requestUri = $"{SupportDecks}{regionStr}/{friendCode}?random={Guid.NewGuid()}";
        using var response = await _client.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        var parsedResponse = DeserializeResponse(content);
        LogResponse(nameof(GetSupportDeckAsync), response.StatusCode, parsedResponse);
        return parsedResponse;
    }

    public async Task<ApiResponse> RequestSupportLookupAsync(Region region, string friendCode, CancellationToken cancellationToken) {
        ValidateFriendCode(friendCode);
        var regionInt = (int) region;
        _ = Utils.Utils.StringRegion(region); // validates the region before issuing any request

        var query = HttpUtility.ParseQueryString(string.Empty);
        query["apiKey"] = _options.ApiKey;
        query["region"] = regionInt.ToString();
        query["friendId"] = friendCode;
        var fullUrl = SupportLookup + '?' + query;

        using var response = await _client.GetAsync(fullUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        var parsedResponse = DeserializeResponse(content);
        LogResponse(nameof(RequestSupportLookupAsync), response.StatusCode, parsedResponse);

        if (parsedResponse.Status == 200) {
            return await WaitResponse(fullUrl, parsedResponse, cancellationToken);
        }

        return parsedResponse;
    }

    private async Task<ApiResponse> WaitResponse(string query, ApiResponse firstResponse, CancellationToken cancellationToken) {
        var response = firstResponse;
        var currentRequests = 1;

        while (response.Message != Finished && response.Status == 200 && currentRequests < _options.MaxLookupRequests) {
            await Task.Delay(_options.RequestsInterval, cancellationToken);

            using var httpResponse = await _client.GetAsync(query, cancellationToken);
            httpResponse.EnsureSuccessStatusCode();
            var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

            response = DeserializeResponse(content);
            LogResponse(nameof(WaitResponse), httpResponse.StatusCode, response);

            currentRequests++;
        }

        return response;
    }

    private void ValidateFriendCode(string friendCode) {
        if (!FriendCodeRegex().IsMatch(friendCode)) {
            throw new ArgumentException("Not a valid friend code", nameof(friendCode));
        }
    }

    private void LogResponse(string operation, System.Net.HttpStatusCode statusCode, ApiResponse response) {
        _logger?.LogDebug(
            "Rayshift {Operation} responded {StatusCode} (status={ApiStatus}, message={ApiMessage})",
            operation, (int) statusCode, response.Status, response.Message);
    }

    private static ApiResponse DeserializeResponse(string response) {
        var parsedResponse = JsonSerializer.Deserialize<ApiResponse>(response)
            ?? throw new JsonException("Rayshift returned a null or unparsable response.");
        if (parsedResponse.Response != null) {
            parsedResponse.Response.BaseAddress = BaseAddress;
        }

        return parsedResponse;
    }

    [System.Text.RegularExpressions.GeneratedRegex("^[0-9]{9}$")]
    private static partial System.Text.RegularExpressions.Regex FriendCodeRegex();
}
