using FriendPeanut.Application.Features.Inventory.PostStockDocument;
using Microsoft.AspNetCore.Mvc;

namespace FriendPeanut.WebApi.Endpoints.Inventory;

public static class StockPostingEndpoint
{
    public static IEndpointRouteBuilder MapStockPostingEndpoint(
        this IEndpointRouteBuilder endpoints)
    {
        var postEndpoint = endpoints.MapPost(
            "/api/v1/stock-documents/{id:guid}/post",
            async (
                [FromRoute] Guid id,
                [FromHeader(Name = "Idempotency-Key")]
                string idempotencyKey,
                [FromBody] PostStockDocumentRequest request,
                [FromServices] StockPostingService service,
                CancellationToken cancellationToken) =>
            {
                var command = new PostStockDocumentCommand(
                    id,
                    request.ExpectedVersion,
                    idempotencyKey,
                    request.NegativeStockOverrideRequested,
                    request.NegativeStockOverrideReason);

                var result = await service.PostAsync(
                    command,
                    cancellationToken);

                return Results.Ok(result);
            });

        postEndpoint.RequireAuthorization();

        return endpoints;
    }
}

public sealed record PostStockDocumentRequest(
    long ExpectedVersion,
    bool NegativeStockOverrideRequested,
    string? NegativeStockOverrideReason);
