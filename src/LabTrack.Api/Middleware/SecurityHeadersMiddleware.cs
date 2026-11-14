namespace LabTrack.Api.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ContentSecurityPolicy =
        "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; " +
        "form-action 'self'; img-src 'self' data:; style-src 'self'; script-src 'self'; " +
        "connect-src 'self'; font-src 'self' data:";

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["Content-Security-Policy"] = ContentSecurityPolicy;
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        await next(context);
    }
}
