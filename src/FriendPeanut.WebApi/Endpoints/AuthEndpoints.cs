using FriendPeanut.Application.Abstractions;
using FriendPeanut.Application.Behaviors;
using FriendPeanut.Application.Features.Identity.AuthenticateUser;
using FriendPeanut.Application.Features.Identity.ChangeFirstPassword;
using FriendPeanut.Application.Features.Identity.EnrollAndVerifyMfa;
using Microsoft.AspNetCore.Mvc;

namespace FriendPeanut.WebApi.Endpoints;

public static class AuthEndpoints
{
    private const string SessionCookie = "fp_session";
    private const string ChallengeCookie = "fp_challenge";
    private const string CsrfCookie = "fp_csrf";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/auth/csrf", (HttpContext ctx) =>
        {
            var token = Guid.NewGuid().ToString("N");
            ctx.Response.Cookies.Append(CsrfCookie, token, new CookieOptions
            { HttpOnly = false, Secure = true, SameSite = SameSiteMode.Strict, Path = "/" });
            return Results.Ok(new { csrfToken = token });
        }).AllowAnonymous();

        app.MapPost("/api/v1/auth/login", async (HttpContext ctx, [FromBody] AuthenticateUserCommand cmd,
            [FromServices] AuthenticateUserValidator validator, [FromServices] AuthenticateUserHandler handler) =>
        {
            await ValidationBehavior.ValidateAsync(validator, cmd, ctx.RequestAborted);
            var ip = ctx.Connection.RemoteIpAddress?.ToString();
            var outcome = await handler.HandleAsync(cmd, "LAN", ip, ctx.TraceIdentifier, ctx.RequestAborted);

            if (outcome.NextStep == "AUTHENTICATED" && outcome.SessionId is not null)
                ctx.Response.Cookies.Append(SessionCookie, outcome.PreAuthToken, new CookieOptions
                { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/", Expires = outcome.ExpiresAt });
            else
                ctx.Response.Cookies.Append(ChallengeCookie, outcome.PreAuthToken, new CookieOptions
                { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/api/v1/auth", Expires = DateTimeOffset.UtcNow.AddMinutes(15) });

            return Results.Ok(new { nextStep = outcome.NextStep, accessChannel = outcome.AccessChannel });
        }).AllowAnonymous();

        app.MapPost("/api/v1/auth/change-password", async (HttpContext ctx, [FromBody] ChangeFirstPasswordCommand cmd,
            [FromServices] ChangeFirstPasswordValidator v, [FromServices] ChangeFirstPasswordHandler h) =>
        {
            await ValidationBehavior.ValidateAsync(v, cmd, ctx.RequestAborted);
            var result = await h.HandleAsync(cmd, ctx.RequestAborted);
            ctx.Response.Cookies.Append(ChallengeCookie, result.PreAuthToken, new CookieOptions
            { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/api/v1/auth", Expires = DateTimeOffset.UtcNow.AddMinutes(5) });
            return Results.Ok(new { nextStep = result.NextStep });
        }).AllowAnonymous();

        app.MapPost("/api/v1/auth/mfa/setup", async (HttpContext ctx, [FromServices] SetupMfaHandler h) =>
        {
            if (ctx.User.Identity?.IsAuthenticated != true) return Results.Unauthorized();
            var id = Guid.Parse(ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            return Results.Ok(await h.HandleAsync(new SetupMfaCommand(id), ctx.RequestAborted));
        }).RequireAuthorization();

        app.MapPost("/api/v1/auth/mfa/verify", async (HttpContext ctx, [FromBody] MfaCodeRequest body, [FromServices] VerifyMfaHandler h) =>
        {
            if (ctx.User.Identity?.IsAuthenticated != true) return Results.Unauthorized();
            var userId = Guid.Parse(ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var result = await h.HandleAsync(new VerifyMfaCommand(userId, body.Code), "LAN", ctx.RequestAborted);
            if (!result.Success)
                return Results.BadRequest(new { code = "MFA_INVALID", messageAr = "رمز التحقق غير صحيح." });

            ctx.Response.Cookies.Append(SessionCookie, result.SessionToken!, new CookieOptions
            { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/", Expires = result.ExpiresAt });
            ctx.Response.Cookies.Delete(ChallengeCookie);
            return Results.Ok(new { nextStep = "AUTHENTICATED", expiresAt = result.ExpiresAt });
        }).RequireAuthorization();

        app.MapPost("/api/v1/auth/logout", async (HttpContext ctx, [FromServices] ISessionStore sessions) =>
        {
            var token = ctx.Request.Cookies[SessionCookie];
            if (!string.IsNullOrEmpty(token))
            {
                var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
                var existing = await sessions.FindByTokenHashAsync(hash, ctx.RequestAborted);
                if (existing is not null) await sessions.RevokeAsync(existing.Id, ctx.RequestAborted);
            }
            ctx.Response.Cookies.Delete(SessionCookie);
            ctx.Response.Cookies.Delete(ChallengeCookie);
            return Results.NoContent();
        }).RequireAuthorization();

        return app;
    }
}

public sealed record MfaCodeRequest(string Code);