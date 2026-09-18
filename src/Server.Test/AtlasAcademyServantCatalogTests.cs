using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FgoData;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Server.Test;

[TestFixture]
public class AtlasAcademyServantCatalogTests {
    private const string IndexJson =
        "[{\"collectionNo\":1,\"name\":\"Mash Kyrielight\"},{\"collectionNo\":50,\"name\":\"Artoria Pendragon\"}]";

    private static string BuildNiceServantJson(string comment) =>
        "{\"collectionNo\":1,\"name\":\"Mash Kyrielight\",\"className\":\"shielder\",\"rarity\":1," +
        "\"atkBase\":100,\"atkMax\":200,\"hpBase\":300,\"hpMax\":400," +
        "\"cards\":[\"quick\",\"quick\",\"arts\",\"arts\",\"buster\"]," +
        "\"noblePhantasms\":[{\"card\":\"arts\"},{\"card\":\"buster\"}]," +
        "\"profile\":{\"comments\":[{\"comment\":\"\"},{\"comment\":\"" + comment + "\"}]}," +
        "\"extraAssets\":{\"charaGraph\":{\"ascension\":{\"1\":\"https://example.com/mash.png\"}}}}";

    private static (AtlasAcademyServantCatalog Catalog, ScriptedHandler Handler) CreateCatalog(IDictionary<string, string> routes) {
        var handler = new ScriptedHandler(routes);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.atlasacademy.io/") };
        var cache = new MemoryCache(new MemoryCacheOptions());
        var catalog = new AtlasAcademyServantCatalog(httpClient, cache, NullLogger<AtlasAcademyServantCatalog>.Instance);
        return (catalog, handler);
    }

    [Test]
    public async Task FindServantAsync_MapsAllFields() {
        var (catalog, _) = CreateCatalog(new Dictionary<string, string> {
            ["export/NA/basic_servant.json"] = IndexJson,
            ["nice/NA/servant/1"] = BuildNiceServantJson("A short comment")
        });

        var result = await catalog.FindServantAsync("Mash", CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.CollectionNo, Is.EqualTo(1));
        Assert.That(result.Name, Is.EqualTo("Mash Kyrielight"));
        Assert.That(result.ClassName, Is.EqualTo("Shielder"));
        Assert.That(result.Rarity, Is.EqualTo(1));
        Assert.That(result.AtkBase, Is.EqualTo(100));
        Assert.That(result.AtkMax, Is.EqualTo(200));
        Assert.That(result.HpBase, Is.EqualTo(300));
        Assert.That(result.HpMax, Is.EqualTo(400));
        Assert.That(result.Deck, Is.EqualTo("QQAAB"));
        Assert.That(result.NpType, Is.EqualTo("Buster"));
        Assert.That(result.Comment, Is.EqualTo("A short comment"));
        Assert.That(result.ImageUrl, Is.EqualTo("https://example.com/mash.png"));
        Assert.That(result.DbUrl, Is.EqualTo("https://apps.atlasacademy.io/db/NA/servant/1"));
    }

    [Test]
    public async Task FindServantAsync_TruncatesLongComments() {
        var longComment = new string('x', 950);
        var (catalog, _) = CreateCatalog(new Dictionary<string, string> {
            ["export/NA/basic_servant.json"] = IndexJson,
            ["nice/NA/servant/1"] = BuildNiceServantJson(longComment)
        });

        var result = await catalog.FindServantAsync("Mash", CancellationToken.None);

        Assert.That(result!.Comment, Has.Length.EqualTo(901));
        Assert.That(result.Comment, Does.EndWith("…"));
    }

    [Test]
    public async Task FindServantAsync_UnknownName_ReturnsNull() {
        var (catalog, _) = CreateCatalog(new Dictionary<string, string> {
            ["export/NA/basic_servant.json"] = IndexJson
        });

        var result = await catalog.FindServantAsync("Nonexistent Servant Name", CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task FindServantAsync_TwoLookups_IssuesOnlyOneIndexRequest() {
        var (catalog, handler) = CreateCatalog(new Dictionary<string, string> {
            ["export/NA/basic_servant.json"] = IndexJson,
            ["nice/NA/servant/1"] = BuildNiceServantJson("c1"),
            ["nice/NA/servant/50"] = BuildNiceServantJson("c2")
        });

        await catalog.FindServantAsync("Mash", CancellationToken.None);
        await catalog.FindServantAsync("Artoria", CancellationToken.None);

        Assert.That(handler.RequestCount("export/NA/basic_servant.json"), Is.EqualTo(1));
    }

    [Test]
    public void FindServantAsync_NullIndexBody_ThrowsInvalidDataException() {
        var (catalog, _) = CreateCatalog(new Dictionary<string, string> {
            ["export/NA/basic_servant.json"] = "null"
        });

        Assert.ThrowsAsync<InvalidDataException>(async () => await catalog.FindServantAsync("Mash", CancellationToken.None));
    }

    [Test]
    [Category("LiveIntegration")]
    [Explicit("Requires network access to api.atlasacademy.io")]
    public async Task FindServantAsync_LiveAtlasAcademy_ReturnsMashKyrielight() {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.atlasacademy.io/") };
        var cache = new MemoryCache(new MemoryCacheOptions());
        var catalog = new AtlasAcademyServantCatalog(httpClient, cache, NullLogger<AtlasAcademyServantCatalog>.Instance);

        var result = await catalog.FindServantAsync("Mash Kyrielight", CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.ClassName, Is.EqualTo("Shielder"));
        Assert.That(result.Rarity, Is.GreaterThan(0));
    }

    private sealed class ScriptedHandler(IDictionary<string, string> routes) : HttpMessageHandler {
        private readonly Dictionary<string, int> _counts = new();

        public int RequestCount(string path) => _counts.GetValueOrDefault(path, 0);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            var path = request.RequestUri!.AbsolutePath.TrimStart('/');
            _counts[path] = _counts.GetValueOrDefault(path, 0) + 1;

            if (!routes.TryGetValue(path, out var body)) {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
