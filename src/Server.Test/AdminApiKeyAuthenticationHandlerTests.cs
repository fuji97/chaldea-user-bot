using System;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Server.Security;

namespace Server.Test;

[TestFixture]
public class AdminApiKeyAuthenticationHandlerTests {
    private static async Task<AuthenticateResult> AuthenticateAsync(string? configuredKey, string? headerValue) {
        var handler = new AdminApiKeyAuthenticationHandler(
            new OptionsMonitorStub<AuthenticationSchemeOptions>(new AuthenticationSchemeOptions()),
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            Options.Create(new AdminApiOptions { ApiKey = configuredKey }));

        var scheme = new AuthenticationScheme(AdminApiAuthentication.Scheme, AdminApiAuthentication.Scheme, typeof(AdminApiKeyAuthenticationHandler));
        var context = new DefaultHttpContext();
        if (headerValue != null) {
            context.Request.Headers[AdminApiAuthentication.HeaderName] = headerValue;
        }

        await handler.InitializeAsync(scheme, context);
        return await handler.AuthenticateAsync();
    }

    [Test]
    public async Task AuthenticateAsync_CorrectKey_Succeeds() {
        var result = await AuthenticateAsync("secret", "secret");

        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    public async Task AuthenticateAsync_WrongKey_Fails() {
        var result = await AuthenticateAsync("secret", "wrong");

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Failure, Is.Not.Null);
    }

    [Test]
    public async Task AuthenticateAsync_MissingHeader_IsNoResult() {
        var result = await AuthenticateAsync("secret", null);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.None, Is.True);
    }

    [Test]
    public async Task AuthenticateAsync_EmptyConfiguredKey_NeverAuthenticates() {
        var result = await AuthenticateAsync("", "anything");

        Assert.That(result.Succeeded, Is.False);
    }

    private sealed class OptionsMonitorStub<T>(T value) : IOptionsMonitor<T> {
        public T CurrentValue { get; } = value;

        public T Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<T, string> listener) => NullDisposable.Instance;

        private sealed class NullDisposable : IDisposable {
            public static readonly NullDisposable Instance = new();

            public void Dispose() {
            }
        }
    }
}
