using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Sales.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    // Test-only credential. It is never a runtime default.
    public const string TestKey = "test-only-key-for-sales-api-0123456789";
    public string EnvironmentName { get; init; } = "Development";
    public string ConfiguredKey { get; init; } = TestKey;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.ConfigureServices(services => services.AddDataProtection().UseEphemeralDataProtectionProvider());
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:ApiKey"] = ConfiguredKey
            }));
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://example.test"),
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("X-API-Key", TestKey);
        return client;
    }
}
