using System.Text.Json;

namespace LabTrack.Api.Application;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception exception)
        {
            var status = exception is ApiException apiException ? apiException.StatusCode : 500;
            if (status == 500) logger.LogError(exception, "Unhandled request failure");
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            var title = status == 500 ? "An unexpected server error occurred." : exception.Message;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                type = "about:blank", title, status, traceId = context.TraceIdentifier
            }));
        }
    }
}
