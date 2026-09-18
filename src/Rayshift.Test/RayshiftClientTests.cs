using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Rayshift.Models;

namespace Rayshift.Test;

[TestFixture]
[Category("LiveIntegration")]
[Explicit("Requires rayshift.io plus ApiKey/FriendCode user secrets")]
public class RayshiftClientTests {
    private const Region Region = Models.Region.Na;

    private HttpClient _httpClient = null!;
    private RayshiftClient _client = null!;
    private string _apiKey = null!, _friendCode = null!;

    [SetUp]
    public void Setup() {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<RayshiftClientTests>()
            .Build();

        _apiKey = configuration["ApiKey"]!;
        _friendCode = configuration["FriendCode"]!;

        Assert.That(_apiKey, Is.Not.Null.And.Not.Empty, "Missing API Key");
        Assert.That(_friendCode, Does.Match("^[0-9]{9}$"), "Missing or invalid Friend Code");

        _httpClient = new HttpClient { BaseAddress = new Uri(RayshiftClient.ApiBaseAddress) };
        _client = new RayshiftClient(_httpClient, new RayshiftOptions { ApiKey = _apiKey });
    }

    [TearDown]
    public void TearDown() {
        _httpClient?.Dispose();
    }

    [Test]
    public async Task TestDecks() {
        var result = await _client.GetSupportDeckAsync(Region, _friendCode, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(200));
        Assert.That(result.Response, Is.Not.Null);
        Assert.That(result.Response!.Code, Is.EqualTo(_friendCode));
        await CheckImages(result);
    }

    [Test]
    public async Task TestLookup() {
        var result = await _client.RequestSupportLookupAsync(Region, _friendCode, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(200));
        Assert.That(result.MessageType, Is.EqualTo(MessageCode.Finished));
        Assert.That(result.Response, Is.Not.Null);
        Assert.That(result.Response!.Code, Is.EqualTo(_friendCode));
        await CheckImages(result);
    }

    private async Task CheckImages(ApiResponse apiResponse) {
        Assert.That(apiResponse.Response, Is.Not.Null);
        using var client = new HttpClient();
        await CheckImage(client, apiResponse.Response!.SupportList(Region));
    }

    private async Task CheckImage(HttpClient client, string url) {
        Console.WriteLine($"URL: {url}");
        using var response = await client.GetAsync(url);
        Assert.That(response.IsSuccessStatusCode, Is.True);
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/png").Or.EqualTo("image/jpeg"));
    }
}
