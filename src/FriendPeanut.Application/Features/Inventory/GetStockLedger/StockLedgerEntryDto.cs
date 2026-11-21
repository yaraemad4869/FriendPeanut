namespace FriendPeanut.Application.Features.Inventory.GetStockLedger;

public sealed record StockLedgerEntryDto(
    Guid Id,
    Guid DocumentId,
    Guid ItemId,
    Guid WarehouseId,
    Guid LotId,
    decimal Quantity,
    string Direction,
    DateTimeOffset PostedAt,
    string Version);
