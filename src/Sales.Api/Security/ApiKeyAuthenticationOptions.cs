using Microsoft.AspNetCore.Authentication;

namespace Sales.Api.Security;

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-API-Key";
    public string ApiKey { get; set; } = string.Empty;

    public bool HasValidKey() =>
        ApiKey is { Length: >= 32 and <= 128 } && ApiKey.All(character => character is >= '!' and <= '~');
}
