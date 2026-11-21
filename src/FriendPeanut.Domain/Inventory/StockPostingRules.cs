namespace FriendPeanut.Domain.Inventory;

public static class StockPostingRules
{
    public const int QuantityPrecision = 3;
    public const int MaximumQuantityScale = 3;

    public static void ValidateQuantity(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Stock quantity must be greater than zero.");
        }

        if (decimal.Round(
                quantity,
                MaximumQuantityScale,
                MidpointRounding.ToEven) != quantity)
        {
            throw new InvalidOperationException(
                "Quantity precision exceeds numeric(18,3).");
        }
    }

    public static void ValidateMovement(StockMovement movement)
    {
        if (movement.ItemId == Guid.Empty)
            throw new InvalidOperationException("ItemId is required.");

        if (movement.WarehouseId == Guid.Empty)
            throw new InvalidOperationException("WarehouseId is required.");

        if (movement.LotId == Guid.Empty)
            throw new InvalidOperationException("LotId is required.");

        if (movement.UnitId == Guid.Empty)
            throw new InvalidOperationException("UnitId is required.");

        ValidateQuantity(movement.Quantity);
    }
}
