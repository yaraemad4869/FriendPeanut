namespace FriendPeanut.Application.Features.Inventory.GetStockAvailability;

public sealed record StockAvailabilityResult(
    Guid ItemId,
    Guid WarehouseId,
    Guid? LotId,
    decimal AvailableQuantity,
    string UnitCode);
