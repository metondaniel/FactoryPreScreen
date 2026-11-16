namespace Sales.Api.Security;

public sealed class RequestProtectionMiddleware(RequestDelegate next, IWebHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers.XFrameOptions = "DENY";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers.CacheControl = "no-store";
            if (context.Request.Path.StartsWithSegments("/api"))
                context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            return Task.CompletedTask;
        });

        // Reject rather than redirect: a redirect cannot protect a key already sent in plaintext.
        if (!environment.IsDevelopment() && !context.Request.IsHttps)
        {
            await Results.Problem(statusCode: 400, title: "HTTPS required",
                detail: "Use an HTTPS connection.").ExecuteAsync(context);
            return;
        }

        if (context.Request.ContentLength > UploadLimits.MaxRequestBytes)
        {
            await Results.Problem(statusCode: 413, title: "Request too large",
                detail: "The CSV must not exceed 5 MiB.").ExecuteAsync(context);
            return;
        }

        await next(context);
    }
}
