using System.Net;

namespace Collector.Hosting.Middlewares;

internal sealed class ExceptionHandlingMiddleware(RequestDelegate requestDelegate, ILogger<ExceptionHandlingMiddleware> logger)
{
    private readonly RequestDelegate _requestDelegate = requestDelegate ?? throw new ArgumentNullException(nameof(requestDelegate));

    public async Task Invoke(HttpContext httpContext)
    {
        try
        {
            await _requestDelegate(httpContext);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Cancellation has occurred");
            httpContext.Response.StatusCode = 499;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error has occurred");
            httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        }
    }
}