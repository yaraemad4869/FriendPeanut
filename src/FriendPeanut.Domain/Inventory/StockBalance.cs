namespace FriendPeanut.Domain.Inventory;

public sealed class StockBalance
{
    public Guid ItemId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid LotId { get; private set; }
    public decimal Quantity { get; private set; }
    public long Version { get; private set; }

    private StockBalance()
    {
    }

    public StockBalance(
        Guid itemId,
        Guid warehouseId,
        Guid lotId,
        decimal quantity)
    {
        ItemId = itemId;
        WarehouseId = warehouseId;
        LotId = lotId;
        Quantity = quantity;
        Version = 1;
    }

    public void Apply(decimal delta)
    {
        Quantity += delta;
        Version++;
    }
}
