using FriendPeanut.Application.Abstractions;
using FriendPeanut.Application.Behaviors;
using FriendPeanut.Application.Features.Identity.AssignFactoryOperators;
using FriendPeanut.Application.Features.Identity.ConfigureMainUsers;
using FriendPeanut.Application.Features.Identity.EnforceScopes;
using FriendPeanut.Application.Features.Identity.LockAccountAndLoginHistory;
using FriendPeanut.Application.Features.Identity.ManageAccountStatus;
using FriendPeanut.Application.Features.Identity.ManageRoles;
using FriendPeanut.Application.Features.Identity.RevokeSessions;
using FriendPeanut.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace FriendPeanut.WebApi.Endpoints;

public static class IdentityAdminEndpoints
{
    public static IEndpointRouteBuilder MapIdentityAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/users", async ([FromServices] IUserStore store, CancellationToken ct) =>
        {
            var users = await store.ListAsync(ct);
            return Results.Ok(users.Select(u => new { u.Id, u.SlotNo, u.Username, u.DisplayName, u.Status, u.MustChangePassword, u.RemoteAccessEnabled, u.FailedLoginCount, u.Version }));
        }).RequireAuthorization();

        app.MapPost("/api/v1/users/{id:guid}/status", async (Guid id, [FromBody] AccountStatusBody body,
            [FromServices] ManageAccountStatusHandler h, [FromServices] SetUserStatusValidator v, CancellationToken ct) =>
        {
            var cmd = new SetUserStatusCommand(id, body.Status);
            await ValidationBehavior.ValidateAsync(v, cmd, ct);
            await h.HandleSetStatusAsync(cmd, ct);
            return Results.NoContent();
        }).RequireAuthorization();

        app.MapPost("/api/v1/users/{id:guid}/remote-access", async (Guid id, [FromBody] RemoteAccessBody body,
            [FromServices] ManageAccountStatusHandler h, CancellationToken ct) =>
        {
            await h.HandleSetRemoteAccessAsync(new SetRemoteAccessCommand(id, body.Enabled), ct);
            return Results.NoContent();
        }).RequireAuthorization();

        app.MapPost("/api/v1/users/{id:guid}/reset-password", async (Guid id, [FromBody] AdminResetPasswordBody body,
            [FromServices] ManageAccountStatusHandler h, [FromServices] AdminResetPasswordValidator v, CancellationToken ct) =>
        {
            var cmd = new AdminResetPasswordCommand(id, body.NewPassword, body.Reason);
            await ValidationBehavior.ValidateAsync(v, cmd, ct);
            await h.HandleAdminResetPasswordAsync(cmd, ct);
            return Results.NoContent();
        }).RequireAuthorization();

        app.MapGet("/api/v1/login-attempts", async (int? take, [FromServices] LoginHistoryQuery q, CancellationToken ct)
            => Results.Ok(await q.HandleAsync(take ?? 25, ct))).RequireAuthorization();

        app.MapGet("/api/v1/users/{id:guid}/sessions", async (Guid id, [FromServices] ISessionStore store, CancellationToken ct)
            => Results.Ok(await store.ListForUserAsync(id, ct))).RequireAuthorization();

        app.MapPost("/api/v1/users/{id:guid}/sessions/revoke", async (Guid id, [FromServices] RevokeSessionsHandler h, CancellationToken ct) =>
        {
            await h.HandleAsync(new RevokeSessionsCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization();

        app.MapGet("/api/v1/roles", async ([FromServices] ManageRolesHandler h, CancellationToken ct)
            => Results.Ok(await h.ListAsync(ct))).RequireAuthorization();

        app.MapGet("/api/v1/roles/{id:guid}", async (Guid id, [FromServices] ManageRolesHandler h, CancellationToken ct) =>
        {
            var role = await h.GetAsync(id, ct);
            return role is null ? Results.NotFound() : Results.Ok(role);
        }).RequireAuthorization();

        app.MapPost("/api/v1/roles", async ([FromBody] CreateRoleBody body, [FromServices] ManageRolesHandler h, CancellationToken ct)
            => Results.Created("/api/v1/roles", await h.CreateAsync(new CreateRoleCommand(body.Code, body.NameAr), ct))).RequireAuthorization();

        app.MapPut("/api/v1/roles/{id:guid}", async (Guid id, [FromBody] UpdateRoleBody body,
            [FromServices] ManageRolesHandler h, CancellationToken ct) =>
        {
            await h.UpdateAsync(new UpdateRoleCommand(id, body.Code, body.NameAr, body.Version), ct);
            return Results.NoContent();
        }).RequireAuthorization();

        app.MapPost("/api/v1/roles/{id:guid}/permissions", async (Guid id, [FromBody] IdListBody body,
            [FromServices] ManageRolesHandler h, CancellationToken ct) =>
        {
            await h.SetPermissionsAsync(new SetRolePermissionsCommand(id, body.Ids), ct);
            return Results.NoContent();
        }).RequireAuthorization();

        app.MapGet("/api/v1/permissions", async ([FromServices] IRoleStore store, CancellationToken ct)
            => Results.Ok(await store.ListPermissionsAsync(ct))).RequireAuthorization();

        app.MapPost("/api/v1/users/{id:guid}/roles", async (Guid id, [FromBody] IdListBody body,
            [FromServices] ManageRolesHandler h, CancellationToken ct) =>
        {
            await h.SetUserRolesAsync(new SetUserRolesCommand(id, body.Ids), ct);
            return Results.NoContent();
        }).RequireAuthorization();

        app.MapGet("/api/v1/users/{id:guid}/scopes", async (Guid id, [FromServices] EnforceScopesHandler h, CancellationToken ct)
            => Results.Ok(await h.ListAsync(id, ct))).RequireAuthorization();

        app.MapPost("/api/v1/users/{id:guid}/scopes", async (Guid id, [FromBody] ScopeBody body,
            [FromServices] EnforceScopesHandler h, CancellationToken ct) =>
        {
            var scopes = body.Scopes.Select(s => new UserScopeRecord(Guid.NewGuid(), id, s.ScopeKind, s.WarehouseId, s.FactoryId, s.DepartmentCode)).ToArray();
            await h.HandleAsync(new SetUserScopesCommand(id, scopes), ct);
            return Results.NoContent();
        }).RequireAuthorization();

        app.MapPost("/api/v1/users/{id:guid}/factory-operator", async (Guid id, [FromBody] FactoryAssignmentBody body,
            [FromServices] AssignFactoryOperatorsHandler h, CancellationToken ct) =>
        {
            var r = await h.HandleAsync(new AssignFactoryOperatorCommand(id, body.FactoryId), ct);
            return MapBoolResult(r);
        }).RequireAuthorization();

        app.MapPost("/api/v1/users/configure-main", async ([FromBody] ConfigureMainUsersBody body,
            [FromServices] ConfigureMainUsersHandler h, CancellationToken ct) =>
        {
            var r = await h.HandleAsync(new ConfigureMainUsersCommand(body.UserIds, body.RoleIds), ct);
            return MapBoolResult(r);
        }).RequireAuthorization();

        return app;
    }

    private static IResult MapBoolResult(Result<bool> r) => r.Status switch
    {
        ResultStatus.PolicyPending => Results.Problem(statusCode: 422, title: r.ErrorCode, detail: r.ErrorMessageAr),
        ResultStatus.Success => Results.Ok(r.Value),
        ResultStatus.NotFound => Results.NotFound(),
        ResultStatus.Forbidden => Results.Forbid(),
        _ => Results.Problem(statusCode: 500, title: r.ErrorCode, detail: r.ErrorMessageAr)
    };
}

public sealed record AccountStatusBody(string Status);
public sealed record RemoteAccessBody(bool Enabled);
public sealed record AdminResetPasswordBody(string NewPassword, string Reason);
public sealed record CreateRoleBody(string Code, string NameAr);
public sealed record UpdateRoleBody(string Code, string NameAr, long Version);
public sealed record IdListBody(IReadOnlyList<Guid> Ids);
public sealed record ScopeBody(IReadOnlyList<ScopeItemBody> Scopes);
public sealed record ScopeItemBody(string ScopeKind, Guid? WarehouseId, Guid? FactoryId, string? DepartmentCode);
public sealed record FactoryAssignmentBody(Guid FactoryId);
public sealed record ConfigureMainUsersBody(IReadOnlyList<Guid> UserIds, IReadOnlyList<Guid> RoleIds);