using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace FgoData;

public sealed class AtlasAcademyServantCatalog(HttpClient client, IMemoryCache cache, ILogger<AtlasAcademyServantCatalog> logger) : IServantCatalog {
    private const string IndexCacheKey = "fgo:servants:index";
    private static readonly SemaphoreSlim IndexLock = new(1, 1);

    public async Task<ServantDetails?> FindServantAsync(string nameQuery, CancellationToken cancellationToken) {
        var index = await GetIndexAsync(cancellationToken);

        var candidates = index
            .Where(s => s.Name.Contains(nameQuery, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 0) {
            logger.LogDebug("No Atlas Academy servant matched query {Query}", nameQuery);
            return null;
        }

        var match = candidates.FirstOrDefault(s => string.Equals(s.Name, nameQuery, StringComparison.OrdinalIgnoreCase))
                    ?? candidates[0];

        return await GetDetailsAsync(match.CollectionNo, cancellationToken);
    }

    private async Task<BasicServant[]> GetIndexAsync(CancellationToken cancellationToken) {
        if (cache.TryGetValue(IndexCacheKey, out BasicServant[]? cached) && cached is not null) {
            return cached;
        }

        await IndexLock.WaitAsync(cancellationToken);
        try {
            if (cache.TryGetValue(IndexCacheKey, out cached) && cached is not null) {
                return cached;
            }

            const string uri = "export/NA/basic_servant.json";
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var result = await System.Text.Json.JsonSerializer.DeserializeAsync(stream, FgoDataJsonContext.Default.BasicServantArray, cancellationToken);

            if (result is not { Length: > 0 }) {
                throw new InvalidDataException($"Atlas Academy endpoint '{uri}' returned no data.");
            }

            cache.Set(IndexCacheKey, result, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) });
            return result;
        }
        finally {
            IndexLock.Release();
        }
    }

    private async Task<ServantDetails> GetDetailsAsync(int collectionNo, CancellationToken cancellationToken) {
        var cacheKey = $"fgo:servant:{collectionNo}";
        if (cache.TryGetValue(cacheKey, out ServantDetails? cached) && cached is not null) {
            return cached;
        }

        var uri = $"nice/NA/servant/{collectionNo}?lore=true";
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var nice = await System.Text.Json.JsonSerializer.DeserializeAsync(stream, FgoDataJsonContext.Default.NiceServant, cancellationToken);

        if (nice is null) {
            throw new InvalidDataException($"Atlas Academy endpoint '{uri}' returned no data.");
        }

        var details = MapToServantDetails(nice);
        cache.Set(cacheKey, details, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) });
        return details;
    }

    private static ServantDetails MapToServantDetails(NiceServant nice) {
        var deck = BuildDeck(nice.Cards);
        var npType = nice.NoblePhantasms is { Length: > 0 }
            ? Capitalize(nice.NoblePhantasms[^1].Card)
            : "-";
        var comment = nice.Profile?.Comments?
            .Select(c => c.Comment?.Trim())
            .FirstOrDefault(c => !string.IsNullOrEmpty(c));
        if (comment is { Length: > 900 }) {
            comment = comment[..900] + "…";
        }

        var imageUrl = GetAscensionImage(nice.ExtraAssets?.CharaGraph?.Ascension)
                       ?? GetAscensionImage(nice.ExtraAssets?.Faces?.Ascension);

        return new ServantDetails(
            nice.CollectionNo,
            nice.Name,
            Capitalize(nice.ClassName),
            nice.Rarity,
            nice.AtkBase,
            nice.AtkMax,
            nice.HpBase,
            nice.HpMax,
            deck,
            npType,
            comment,
            imageUrl,
            $"https://apps.atlasacademy.io/db/NA/servant/{nice.CollectionNo}");
    }

    private static string? GetAscensionImage(Dictionary<string, string>? ascension) {
        if (ascension is null) {
            return null;
        }

        return ascension.TryGetValue("1", out var url) ? url : null;
    }

    private static string BuildDeck(string[]? cards) {
        if (cards is null) {
            return string.Empty;
        }

        var quick = cards.Count(c => c == "quick");
        var arts = cards.Count(c => c == "arts");
        var buster = cards.Count(c => c == "buster");

        return new string('Q', quick) + new string('A', arts) + new string('B', buster);
    }

    private static string Capitalize(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToUpper(value[0], CultureInfo.InvariantCulture) + value[1..];
}
