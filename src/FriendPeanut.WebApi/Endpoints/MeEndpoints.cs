using FriendPeanut.Application.Abstractions;
using FriendPeanut.Application.Features.Identity.GetMyProfile;
using Microsoft.AspNetCore.Mvc;

namespace FriendPeanut.WebApi.Endpoints;

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/me", async ([FromServices] ICurrentUser user, [FromServices] GetMyProfileHandler h, CancellationToken ct) =>
        {
            if (user.UserId is null) return Results.Unauthorized();
            var dto = await h.HandleAsync(user.UserId.Value, ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        }).RequireAuthorization();

        app.MapGet("/api/v1/me/permissions", ([FromServices] ICurrentUser user) =>
        {
            if (!user.IsAuthenticated) return Results.Unauthorized();
            return Results.Ok(new
            {
                permissions = user.Permissions,
                warehouseScopes = user.WarehouseScopes,
                factoryScopes = user.FactoryScopes,
                departmentScopes = user.DepartmentScopes
            });
        }).RequireAuthorization();

        return app;
    }
}