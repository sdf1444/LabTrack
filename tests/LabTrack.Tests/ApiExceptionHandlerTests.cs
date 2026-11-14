using System.Text.Json;
using LabTrack.Api.Application;
using LabTrack.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LabTrack.Tests;

public sealed class ApiExceptionHandlerTests
{
    [Fact]
    public async Task Api_exception_returns_problem_details_with_trace_identifier()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddProblemDetails();
        using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { TraceIdentifier = "test-trace-id" };
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        var handler = new ApiExceptionHandler(provider.GetRequiredService<IProblemDetailsService>());
        var handled = await handler.TryHandleAsync(
            context, new ApiException(StatusCodes.Status409Conflict, "The sample changed."),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);

        responseBody.Position = 0;
        using var document = await JsonDocument.ParseAsync(responseBody);
        Assert.Equal("The sample changed.", document.RootElement.GetProperty("title").GetString());
        Assert.Equal(StatusCodes.Status409Conflict, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("test-trace-id", document.RootElement.GetProperty("traceId").GetString());
    }
}
