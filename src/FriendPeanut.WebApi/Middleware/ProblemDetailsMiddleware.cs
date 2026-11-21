using FriendPeanut.Application.Common.Validation;
using FriendPeanut.Application.Features.Identity.AuthenticateUser;
using FriendPeanut.Domain.Common;
using System.Text.Json;

namespace FriendPeanut.WebApi.Middleware;

public sealed partial class ProblemDetailsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsMiddleware> _log;

    public ProblemDetailsMiddleware(RequestDelegate next, ILogger<ProblemDetailsMiddleware> log)
    {
        _next = next;
        _log = log;
    }

    // Source-generated structured logging — avoids CA1848 and is the idiomatic .NET 10 pattern.
    [LoggerMessage(EventId = 5001, Level = LogLevel.Error, Message = "Unhandled exception")]
    private static partial void LogUnhandledException(ILogger logger, Exception ex);

    public async Task Invoke(HttpContext ctx)
    {
        try { await _next(ctx); }
        catch (ValidationException ex) { await Write(ctx, 422, "VALIDATION_FAILED", "بيانات غير صالحة.", ex.Errors); }
        catch (InvalidCredentialsException ex) { await Write(ctx, 401, "INVALID_CREDENTIALS", ex.Message, null); }
        catch (AccountNotActiveException ex) { await Write(ctx, 403, "ACCOUNT_NOT_ACTIVE", ex.Message, null); }
        catch (DomainException ex) { await Write(ctx, 409, ex.Code, ex.Message, null); }
        catch (Exception ex) when (ex.Message == "CONFLICT_VERSION") { await Write(ctx, 409, "VERSION_CONFLICT", "تغيّر السجل. أعد التحميل ثم حاول مرة أخرى.", null); }
        catch (Exception ex)
        {
            LogUnhandledException(_log, ex);
            await Write(ctx, 500, "INTERNAL_ERROR", "حدث خطأ غير متوقع. يرجى المحاولة لاحقًا.", null);
        }
    }

    private static async Task Write(HttpContext ctx, int status, string code, string messageAr, IReadOnlyDictionary<string, string[]>? errors)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.Clear();
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json; charset=utf-8";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
        { type = "about:blank", title = code, status, code, messageAr, traceId = ctx.TraceIdentifier, errors }));
    }
}