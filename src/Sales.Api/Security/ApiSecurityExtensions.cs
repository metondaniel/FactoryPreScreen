using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;

namespace Sales.Api.Security;

public static class ApiSecurityExtensions
{
    public static IServiceCollection AddApiSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ApiKeyAuthenticationOptions>(ApiKeyAuthenticationOptions.SchemeName)
            .Bind(configuration.GetSection("Security"))
            .Validate(options => options.HasValidKey(),
                "Security:ApiKey must contain 32 to 128 printable ASCII characters without spaces. Configure it through user-secrets or Security__ApiKey.")
            .ValidateOnStart();
        services.AddAuthentication(ApiKeyAuthenticationOptions.SchemeName)
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationOptions.SchemeName, _ => { });
        services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = UploadLimits.MaxRequestBytes;
            options.MemoryBufferThreshold = UploadLimits.MaxRequestBytes;
            options.ValueCountLimit = 8;
            options.ValueLengthLimit = 1024;
            options.MultipartHeadersCountLimit = 16;
            options.MultipartHeadersLengthLimit = 8192;
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // A single bounded partition also limits unauthenticated attempts without allocating a bucket per attacker.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                RateLimitPartition.GetFixedWindowLimiter("sales-api", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
            options.AddConcurrencyLimiter(UploadLimits.ConcurrencyPolicy, limiter =>
            {
                limiter.PermitLimit = 2;
                limiter.QueueLimit = 0;
            });
            options.OnRejected = async (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Ceiling(retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                await Results.Problem(statusCode: 429, title: "Too many requests",
                    detail: "The service is busy. Retry later.").ExecuteAsync(context.HttpContext);
            };
        });
        return services;
    }
}
