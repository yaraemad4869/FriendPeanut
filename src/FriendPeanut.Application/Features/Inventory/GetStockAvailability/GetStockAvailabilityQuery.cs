namespace FriendPeanut.Application.Features.Inventory.GetStockAvailability;

public sealed record GetStockAvailabilityQuery(
    Guid ItemId,
    Guid WarehouseId,
    Guid? LotId);
