using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
using Rayshift.Models;

namespace Rayshift {
    public class RayshiftClient : IRayshiftClient {
        public const string BaseAddress = "https://rayshift.io";
        public const string ImagesPath = "static/images/deck-gen/";
        private const string ApiBaseAddress = "https://rayshift.io/api/v1/";
        private const string SupportDecks = "support/decks/";
        private const string SupportLookup = "support/lookup/";

        private const string InQueue = "in queue";
        private const string Processing = "processing";
        private const string Finished = "finished";

        private readonly HttpClient _client;
        private readonly string? _apiKey;
        private readonly ILogger<RayshiftClient>? _logger;

        public int MaxLookupRequests { get; set; } = 15;
        public int RequestsInterval { get; set; } = 2000;

        public RayshiftClient(string? apiKey = null, string? baseAddress = null, ILogger<RayshiftClient>? logger = null) {
            _apiKey = apiKey;
            _logger = logger;

            _client = new HttpClient {
                BaseAddress = baseAddress != null ? new Uri(baseAddress) : new Uri(ApiBaseAddress),
            };
        }

        public async Task<ApiResponse?> GetSupportDeck(Region region, string friendCode) {
            if (!Regex.IsMatch(friendCode, "^[0-9]{9}$")) {
                throw new ArgumentException("Not a valid friend code", nameof(friendCode));
            }
            
            var regionStr = Utils.Utils.StringRegion(region);

            var requestUri = $"{SupportDecks}{regionStr}/{friendCode}";
            var response = await _client.GetAsync(requestUri);
            var content = await response.Content.ReadAsStringAsync();
            _logger?.LogDebug("Response from Rayshift [{Url}]: {Content}", _client.BaseAddress + requestUri, content);
            
            var parsedResponse = DeserializeResponse(content);
            return parsedResponse;
        }
        
        public async Task<ApiResponse?> RequestSupportLookupAsync(Region region, string friendCode) {
            if (!Regex.IsMatch(friendCode, "^[0-9]{9}$")) {
                throw new ArgumentException("Not a valid friend code", nameof(friendCode));
            }
            
            var regionInt = (int) region;

            var query = HttpUtility.ParseQueryString(string.Empty);
            query["apiKey"] = _apiKey;
            query["region"] = regionInt.ToString();
            query["friendId"] = friendCode;
            string fullUrl = SupportLookup + '?' + query.ToString();

            var response = await _client.GetAsync(fullUrl);
            var content = await response.Content.ReadAsStringAsync();
            _logger?.LogDebug("Response from Rayshift [{Url}]: {Content}", _client.BaseAddress + fullUrl, content);
            
            var parsedResponse = DeserializeResponse(content);
            
            if (parsedResponse.Status == 200) {
                return await WaitResponse(fullUrl, parsedResponse);
            }

            return parsedResponse;
        }

        public async Task<bool> RequestSupportLookup(Region region, string friendCode, Func<ApiResponse?, Task>? callback = null) {
            if (!Regex.IsMatch(friendCode, "^[0-9]{9}$")) {
                throw new ArgumentException("Not a valid friend code", nameof(friendCode));
            }
            
            var regionInt = (int) region;

            var query = HttpUtility.ParseQueryString(string.Empty);
            query["apiKey"] = _apiKey;
            query["region"] = regionInt.ToString();
            query["friendId"] = friendCode;
            string fullUrl = SupportLookup + '?' + query.ToString();

            var response = await _client.GetAsync(fullUrl);
            var content = await response.Content.ReadAsStringAsync();
            _logger?.LogDebug("Response from Rayshift [{Url}]: {Content}", _client.BaseAddress + fullUrl, content);
            
            try {
                var parsedResponse = DeserializeResponse(content);
                if (parsedResponse.Status == 200) {
                    if (callback != null) {
#pragma warning disable 4014
                        WaitAndCallCallback(fullUrl, parsedResponse, callback);
#pragma warning restore 4014
                    }
                    return true;
                }
            }
            catch (Exception e) {
                _logger?.LogError(e, "Exception thrown when deserializing response");
                return false;
            }

            return false;
        }

        private async Task WaitAndCallCallback(string query, ApiResponse firstResponse, Func<ApiResponse?, Task> callback) {
            var response = await WaitResponse(query, firstResponse);

            await callback.Invoke(response);
        }

        private async Task<ApiResponse?> WaitResponse(string query, ApiResponse firstResponse) {
            var response = firstResponse;
            var currentRequests = 1;

            while (response.Message != Finished && response.Status == 200 && currentRequests < MaxLookupRequests) {
                Thread.Sleep(RequestsInterval);

                var httpResponse = await _client.GetAsync(query);
                var content = await httpResponse.Content.ReadAsStringAsync();
                _logger?.LogDebug("Response from Rayshift [{Url}]: {Content}", _client.BaseAddress + query, content);
                
                try {
                    response = DeserializeResponse(content);
                }
                catch (Exception e) {
                    _logger?.LogError(e, "Exception thrown when deserializing response");
                    response = null;
                    break;
                }

                currentRequests++;
            }

            return response;
        }

        private static ApiResponse DeserializeResponse(string response) {
            var parsedResponse = JsonSerializer.Deserialize<ApiResponse>(response);
            if (parsedResponse.Response != null) {
                parsedResponse.Response.BaseAddress = BaseAddress;
            }

            return parsedResponse;
        }

        public void Dispose() {
            _client.Dispose();
        }
    }
}