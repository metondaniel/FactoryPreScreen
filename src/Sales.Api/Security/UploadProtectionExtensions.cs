using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;

namespace Sales.Api.Security;

public static class UploadProtectionExtensions
{
    public static IServiceCollection AddUploadProtection(this IServiceCollection services)
    {
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
            // A single bounded partition limits requests without allocating a bucket per attacker.
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
