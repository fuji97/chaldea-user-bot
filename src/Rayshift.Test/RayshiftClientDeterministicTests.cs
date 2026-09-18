using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Rayshift.Models;

namespace Rayshift.Test;

[TestFixture]
public class RayshiftClientDeterministicTests {
    private const string ValidFriendCode = "123456789";

    private static HttpClient CreateClient(ScriptedHandler handler) =>
        new(handler) { BaseAddress = new Uri(RayshiftClient.ApiBaseAddress) };

    [Test]
    public async Task RequestSupportLookupAsync_PollsUntilFinished() {
        var handler = new ScriptedHandler(
            new StringContent("{\"status\":200,\"message\":\"in queue\"}", Encoding.UTF8, "application/json"),
            new StringContent(FinishedResponseJson, Encoding.UTF8, "application/json"));
        using var httpClient = CreateClient(handler);
        var client = new RayshiftClient(httpClient, new RayshiftOptions { ApiKey = "k", MaxLookupRequests = 2, RequestsInterval = TimeSpan.FromMilliseconds(1) });

        var result = await client.RequestSupportLookupAsync(Region.Na, ValidFriendCode, CancellationToken.None);

        Assert.That(result.MessageType, Is.EqualTo(MessageCode.Finished));
        Assert.That(handler.RequestedUris, Has.Count.EqualTo(2));
    }

    [Test]
    public void RequestSupportLookupAsync_Cancelled_ThrowsAndStopsPolling() {
        using var cts = new CancellationTokenSource();
        var handler = new ScriptedHandler(cts,
            new StringContent("{\"status\":200,\"message\":\"in queue\"}", Encoding.UTF8, "application/json"),
            new StringContent(FinishedResponseJson, Encoding.UTF8, "application/json"));
        using var httpClient = CreateClient(handler);
        var client = new RayshiftClient(httpClient, new RayshiftOptions { ApiKey = "k", MaxLookupRequests = 2, RequestsInterval = TimeSpan.FromSeconds(30) });

        Assert.That(async () => await client.RequestSupportLookupAsync(Region.Na, ValidFriendCode, cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(handler.RequestedUris, Has.Count.EqualTo(1));
    }

    [Test]
    public void GetSupportDeckAsync_InvalidFriendCode_ThrowsBeforeAnyRequest() {
        var handler = new ScriptedHandler(new StringContent(FinishedResponseJson, Encoding.UTF8, "application/json"));
        using var httpClient = CreateClient(handler);
        var client = new RayshiftClient(httpClient, new RayshiftOptions { ApiKey = "k" });

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await client.GetSupportDeckAsync(Region.Na, "12345", CancellationToken.None));
        Assert.That(handler.RequestedUris, Is.Empty);
    }

    [Test]
    public void NullJsonBody_ThrowsJsonException() {
        var handler = new ScriptedHandler(new StringContent("null", Encoding.UTF8, "application/json"));
        using var httpClient = CreateClient(handler);
        var client = new RayshiftClient(httpClient, new RayshiftOptions { ApiKey = "k" });

        Assert.ThrowsAsync<JsonException>(async () =>
            await client.GetSupportDeckAsync(Region.Na, ValidFriendCode, CancellationToken.None));
    }

    [Test]
    public async Task Logging_NeverIncludesTheApiKey() {
        const string apiKey = "super-secret-key";
        var handler = new ScriptedHandler(new StringContent(FinishedResponseJson, Encoding.UTF8, "application/json"));
        using var httpClient = CreateClient(handler);
        var capturingLogger = new CapturingLogger();
        var client = new RayshiftClient(httpClient, new RayshiftOptions { ApiKey = apiKey }, capturingLogger);

        await client.RequestSupportLookupAsync(Region.Na, ValidFriendCode, CancellationToken.None);

        Assert.That(handler.RequestedUris.Count, Is.EqualTo(1));
        Assert.That(handler.RequestedUris[0].Query, Does.Contain(apiKey), "test sanity: the request itself must carry the key");
        Assert.That(capturingLogger.Messages, Is.All.Not.Contains(apiKey));
    }

    private const string FinishedResponseJson =
        "{\"status\":200,\"message\":\"finished\",\"response\":{\"lastUpdate\":1600000000,\"name\":\"Test\",\"code\":\"123456789\",\"lastLogin\":1600000000,\"guid\":\"abc\",\"decks\":{\"1\":\"https://example.com/a/b/c.png\"}}}";

    private sealed class ScriptedHandler : HttpMessageHandler {
        private readonly Queue<HttpContent> _responses;
        private readonly CancellationTokenSource? _cancelAfterFirst;

        public ScriptedHandler(params HttpContent[] responses) : this(null, responses) {
        }

        public ScriptedHandler(CancellationTokenSource? cancelAfterFirst, params HttpContent[] responses) {
            _cancelAfterFirst = cancelAfterFirst;
            _responses = new Queue<HttpContent>(responses);
        }

        public List<Uri> RequestedUris { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            RequestedUris.Add(request.RequestUri!);
            var content = _responses.Count > 0 ? _responses.Dequeue() : _responses.Peek();
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };

            if (_cancelAfterFirst != null && RequestedUris.Count == 1) {
                _cancelAfterFirst.Cancel();
            }

            return Task.FromResult(response);
        }
    }

    private sealed class CapturingLogger : ILogger<RayshiftClient> {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            Messages.Add(formatter(state, exception));
        }
    }
}
