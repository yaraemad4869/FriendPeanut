namespace FriendPeanut.Domain.Inventory;

public sealed record StockMovement(
    Guid StockLineId,
    Guid ItemId,
    Guid WarehouseId,
    Guid LotId,
    Guid UnitId,
    decimal Quantity,
    StockMovementDirection Direction)
{
    public decimal SignedQuantity =>
        Direction == StockMovementDirection.Inbound ? Quantity : -Quantity;
}

public enum StockMovementDirection
{
    Inbound = 1,
    Outbound = 2
}