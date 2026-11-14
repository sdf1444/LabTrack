using LabTrack.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace LabTrack.Tests;

public sealed class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task Adds_security_headers_and_continues_the_pipeline()
    {
        var context = new DefaultHttpContext();
        var nextCalled = false;
        var middleware = new SecurityHeadersMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Contains(
            "default-src 'self'",
            context.Response.Headers["Content-Security-Policy"].ToString());
        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"]);
        Assert.Equal("strict-origin-when-cross-origin", context.Response.Headers["Referrer-Policy"]);
        Assert.Equal(
            "camera=(), microphone=(), geolocation=()",
            context.Response.Headers["Permissions-Policy"]);
    }
}
