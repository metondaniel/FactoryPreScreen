using Microsoft.AspNetCore.Diagnostics;

namespace Sales.Api;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
            return true;
        }

        var status = exception is BadHttpRequestException requestException
            ? requestException.StatusCode
            : StatusCodes.Status500InternalServerError;
        if (status >= 500)
            logger.LogError(exception, "Unhandled error while processing a sales request.");

        await Results.Problem(statusCode: status,
            title: status == 413 ? "Request too large" : status >= 500 ? "An unexpected error occurred" : "Invalid request",
            detail: status == 413 ? "The CSV must not exceed 5 MiB." : null).ExecuteAsync(context);
        return true;
    }
}
