using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Sales.Api.Security;

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.HeaderName, out var values))
            return Task.FromResult(AuthenticateResult.NoResult());

        var suppliedKey = values.Count == 1 ? values[0] : null;
        if (suppliedKey is null || suppliedKey.Length is < 32 or > 128 || !Options.HasValidKey())
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        // Fixed-length hashes allow a constant-time comparison without exposing the configured key.
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(Options.ApiKey));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(suppliedKey));
        if (!CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash))
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "sales-api-client")], Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "ApiKey";
        return Results.Problem(statusCode: StatusCodes.Status401Unauthorized,
            title: "Authentication required", detail: "Provide a valid X-API-Key header.").ExecuteAsync(Context);
    }
}
