using Serilog.Context;

namespace FriendPeanut.WebApi.Middleware;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;
    public CorrelationIdMiddleware(RequestDelegate next) { _next = next; }

    public async Task Invoke(HttpContext ctx)
    {
        string correlationId =
            (ctx.Request.Headers.TryGetValue(HeaderName, out var incoming) && !string.IsNullOrWhiteSpace(incoming))
                ? incoming.ToString()
                : ctx.TraceIdentifier;

        if (correlationId.Length > 80) correlationId = correlationId.Substring(0, 80);

        ctx.TraceIdentifier = correlationId;
        ctx.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(ctx);
        }
    }
}
