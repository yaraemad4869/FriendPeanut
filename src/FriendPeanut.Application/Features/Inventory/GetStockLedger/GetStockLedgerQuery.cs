namespace FriendPeanut.Application.Features.Inventory.GetStockLedger;

public sealed record GetStockLedgerQuery(
    Guid? ItemId,
    Guid? WarehouseId,
    Guid? LotId,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int Page = 1,
    int PageSize = 25);
