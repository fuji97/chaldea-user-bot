using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Server.Security;

public sealed class AdminApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<AdminApiOptions> adminOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder) {
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() {
        var expected = adminOptions.Value.ApiKey;
        if (string.IsNullOrEmpty(expected)) {
            return Task.FromResult(AuthenticateResult.Fail("Admin API is not configured."));
        }

        if (!Request.Headers.TryGetValue(AdminApiAuthentication.HeaderName, out var provided) || string.IsNullOrEmpty(provided)) {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided.ToString());

        var isValid = expectedBytes.Length == providedBytes.Length
                      && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);

        if (!isValid) {
            return Task.FromResult(AuthenticateResult.Fail("Invalid admin API key."));
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin")], AdminApiAuthentication.Scheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), AdminApiAuthentication.Scheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
