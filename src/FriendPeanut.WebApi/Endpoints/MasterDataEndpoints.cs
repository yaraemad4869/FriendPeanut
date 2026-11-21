using FriendPeanut.Application.Behaviors;
using FriendPeanut.Application.Features.MasterData.ConvertQuantity;
using FriendPeanut.Application.Features.MasterData.HoldPendingUnits;
using FriendPeanut.Application.Features.MasterData.HoldRojektGrades;
using FriendPeanut.Application.Features.MasterData.ListItemCategories;
using FriendPeanut.Application.Features.MasterData.ListWarehouses;
using FriendPeanut.Application.Features.MasterData.ManageCostCenters;
using FriendPeanut.Application.Features.MasterData.ManageItems;
using FriendPeanut.Application.Features.MasterData.ManageLots;
using FriendPeanut.Application.Features.MasterData.ManageParties;
using FriendPeanut.Application.Features.MasterData.SearchItemAliases;
using FriendPeanut.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace FriendPeanut.WebApi.Endpoints;

public static class MasterDataEndpoints
{
    public static IEndpointRouteBuilder MapMasterDataEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/items", async ([FromServices] ManageItemsHandler h, int? page, int? pageSize, CancellationToken ct)
            => Results.Ok(await h.ListAsync(page is null or <= 0 ? 1 : page.Value, pageSize is null or <= 0 ? 25 : pageSize.Value, ct))).RequireAuthorization();

        app.MapGet("/api/v1/items/search", async (string q, [FromServices] ManageItemsHandler h, CancellationToken ct)
            => Results.Ok(await h.SearchAsync(q ?? string.Empty, ct))).RequireAuthorization();

        app.MapGet("/api/v1/items/{id:guid}", async (Guid id, [FromServices] ManageItemsHandler h, CancellationToken ct) =>
        {
            var dto = await h.GetAsync(id, ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        }).RequireAuthorization();

        app.MapPost("/api/v1/items", async ([FromBody] ItemCreateRequest body, [FromServices] ManageItemsHandler h, [FromServices] ItemCreateValidator v, CancellationToken ct) =>
        {
            await ValidationBehavior.ValidateAsync(v, body, ct);
            var dto = await h.CreateAsync(body, ct);
            return Results.Created($"/api/v1/items/{dto.Id}", dto);
        }).RequireAuthorization();

        app.MapPut("/api/v1/items/{id:guid}", async (Guid id, [FromBody] ItemUpdateRequest body, long version, [FromServices] ManageItemsHandler h, [FromServices] ItemUpdateValidator v, CancellationToken ct) =>
        {
            await ValidationBehavior.ValidateAsync(v, body, ct);
            var dto = await h.UpdateAsync(id, body, version, ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        }).RequireAuthorization();

        app.MapPost("/api/v1/items/{id:guid}/deactivate", async (Guid id, [FromServices] ManageItemsHandler h, CancellationToken ct) =>
        {
            await h.DeactivateAsync(id, ct);
            return Results.NoContent();
        }).RequireAuthorization();

        app.MapPost("/api/v1/items/{id:guid}/aliases", async (Guid id, [FromBody] AddAliasRequest body, [FromServices] SearchItemAliasesHandler h, CancellationToken ct) =>
        {
            await h.AddAsync(id, body, ct);
            return Results.NoContent();
        }).RequireAuthorization();

        app.MapPost("/api/v1/items/rojekt-grades", async ([FromBody] RojektGradeRequest body, [FromServices] HoldRojektGradesHandler h, CancellationToken ct) =>
        {
            var r = await h.HandleAsync(body, ct);
            return MapBoolResult(r);
        }).RequireAuthorization();

        app.MapPost("/api/v1/items/{id:guid}/verify-pending-unit", async (Guid id, [FromServices] HoldPendingUnitsHandler h, CancellationToken ct) =>
        {
            var r = await h.HandleAsync(id, ct);
            return MapBoolResult(r);
        }).RequireAuthorization();

        app.MapGet("/api/v1/item-categories", async ([FromServices] ListItemCategoriesHandler h, CancellationToken ct)
            => Results.Ok(await h.ListAsync(ct))).RequireAuthorization();

        app.MapGet("/api/v1/item-categories/{id:guid}", async (Guid id, [FromServices] ListItemCategoriesHandler h, CancellationToken ct) =>
        {
            var dto = await h.GetAsync(id, ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        }).RequireAuthorization();

        app.MapPost("/api/v1/item-categories", async ([FromBody] CategoryCreateRequest body, [FromServices] ListItemCategoriesHandler h, CancellationToken ct)
            => Results.Created("/api/v1/item-categories", await h.CreateAsync(body, ct))).RequireAuthorization();

        app.MapPut("/api/v1/item-categories/{id:guid}", async (Guid id, [FromBody] CategoryUpdateRequest body, long version, [FromServices] ListItemCategoriesHandler h, CancellationToken ct) =>
        {
            var dto = await h.UpdateAsync(id, body, version, ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        }).RequireAuthorization();

        app.MapGet("/api/v1/units", async ([FromServices] ConvertQuantityHandler h, CancellationToken ct)
            => Results.Ok(await h.ListUnitsAsync(ct))).RequireAuthorization();

        app.MapPost("/api/v1/units", async ([FromBody] UnitCreateRequest body, [FromServices] ConvertQuantityHandler h, CancellationToken ct)
            => Results.Created("/api/v1/units", await h.CreateUnitAsync(body, ct))).RequireAuthorization();

        app.MapGet("/api/v1/unit-conversions", async ([FromServices] ConvertQuantityHandler h, CancellationToken ct)
            => Results.Ok(await h.ListConversionsAsync(ct))).RequireAuthorization();

        app.MapPost("/api/v1/unit-conversions", async ([FromBody] UnitConversionCreateRequest body, [FromServices] ConvertQuantityHandler h, CancellationToken ct)
            => Results.Created("/api/v1/unit-conversions", await h.CreateConversionAsync(body, ct))).RequireAuthorization();

        app.MapGet("/api/v1/warehouses", async ([FromServices] ListWarehousesHandler h, CancellationToken ct)
            => Results.Ok(await h.HandleAsync(ct))).RequireAuthorization();

        app.MapGet("/api/v1/parties", async ([FromServices] ManagePartiesHandler h, CancellationToken ct)
            => Results.Ok(await h.ListAsync(ct))).RequireAuthorization();

        app.MapGet("/api/v1/parties/{id:guid}", async (Guid id, [FromServices] ManagePartiesHandler h, CancellationToken ct) =>
        {
            var dto = await h.GetAsync(id, ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        }).RequireAuthorization();

        app.MapPost("/api/v1/parties", async ([FromBody] PartyCreateRequest body, [FromServices] ManagePartiesHandler h, CancellationToken ct)
            => Results.Created("/api/v1/parties", await h.CreateAsync(body, ct))).RequireAuthorization();

        app.MapPut("/api/v1/parties/{id:guid}", async (Guid id, [FromBody] PartyUpdateRequest body, long version, [FromServices] ManagePartiesHandler h, CancellationToken ct) =>
        {
            var dto = await h.UpdateAsync(id, body, version, ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        }).RequireAuthorization();

        app.MapGet("/api/v1/cost-centers", async ([FromServices] ManageCostCentersHandler h, CancellationToken ct)
            => Results.Ok(await h.ListAsync(ct))).RequireAuthorization();

        app.MapGet("/api/v1/cost-centers/{id:guid}", async (Guid id, [FromServices] ManageCostCentersHandler h, CancellationToken ct) =>
        {
            var dto = await h.GetAsync(id, ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        }).RequireAuthorization();

        app.MapPost("/api/v1/cost-centers", async ([FromBody] CostCenterCreateRequest body, [FromServices] ManageCostCentersHandler h, CancellationToken ct)
            => Results.Created("/api/v1/cost-centers", await h.CreateAsync(body, ct))).RequireAuthorization();

        app.MapPut("/api/v1/cost-centers/{id:guid}", async (Guid id, [FromBody] CostCenterUpdateRequest body, long version, [FromServices] ManageCostCentersHandler h, CancellationToken ct) =>
        {
            var dto = await h.UpdateAsync(id, body, version, ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        }).RequireAuthorization();

        app.MapGet("/api/v1/lots", async ([FromServices] ManageLotsHandler h, CancellationToken ct)
            => Results.Ok(await h.ListAsync(ct))).RequireAuthorization();

        app.MapGet("/api/v1/lots/{id:guid}", async (Guid id, [FromServices] ManageLotsHandler h, CancellationToken ct) =>
        {
            var dto = await h.GetAsync(id, ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        }).RequireAuthorization();

        app.MapPost("/api/v1/lots", async ([FromBody] LotCreateRequest body, [FromServices] ManageLotsHandler h, CancellationToken ct)
            => Results.Created("/api/v1/lots", await h.CreateAsync(body, ct))).RequireAuthorization();

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