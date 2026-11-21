namespace FriendPeanut.WebApi.Middleware;

public sealed class CsrfValidationMiddleware
{
    public const string CookieName = "fp_csrf";
    public const string HeaderName = "X-CSRF-Token";
    private readonly RequestDelegate _next;

    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    { "GET", "HEAD", "OPTIONS", "TRACE" };

    private static readonly HashSet<string> ExemptPaths = new(StringComparer.Ordinal)
    {
        "/api/v1/auth/csrf",
        "/api/v1/auth/login",
        "/api/v1/auth/change-password"
    };

    public CsrfValidationMiddleware(RequestDelegate next) { _next = next; }

    public async Task Invoke(HttpContext ctx)
    {
        if (SafeMethods.Contains(ctx.Request.Method) || ExemptPaths.Contains(ctx.Request.Path.Value ?? string.Empty))
        {
            await _next(ctx);
            return;
        }

        var cookie = ctx.Request.Cookies[CookieName];
        var header = ctx.Request.Headers[HeaderName].ToString();

        if (string.IsNullOrEmpty(cookie) || string.IsNullOrEmpty(header) || !CryptographicEquals(cookie, header))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            ctx.Response.ContentType = "application/problem+json; charset=utf-8";
            await ctx.Response.WriteAsync("{\"type\":\"about:blank\",\"title\":\"CSRF_INVALID\",\"status\":403,\"code\":\"CSRF_INVALID\",\"messageAr\":\"CSRF token missing or invalid.\"}");
            return;
        }

        await _next(ctx);
    }

    private static bool CryptographicEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
