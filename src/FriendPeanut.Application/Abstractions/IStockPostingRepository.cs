using FriendPeanut.Domain.Inventory;

namespace FriendPeanut.Application.Abstractions;

public interface IStockPostingRepository
{
    Task<bool> DocumentExistsAsync(Guid documentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<StockMovement>> GetMovementsAsync(Guid documentId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, decimal>> GetAvailableQuantitiesAsync(
        IReadOnlyCollection<Guid> itemIds,
        IReadOnlyCollection<Guid> warehouseIds,
        CancellationToken cancellationToken);
    Task AcquireStockLocksAsync(IReadOnlyCollection<StockLockKey> lockKeys, CancellationToken cancellationToken);
    Task ApplyLedgerEntriesAsync(Guid documentId, IReadOnlyCollection<StockMovement> movements, CancellationToken cancellationToken);
    Task UpdateBalancesAsync(IReadOnlyCollection<StockMovement> movements, CancellationToken cancellationToken);
    Task MarkDocumentPostedAsync(Guid documentId, long expectedVersion, CancellationToken cancellationToken);
    Task<bool> IsDocumentPostedAsync(Guid documentId, CancellationToken cancellationToken);
}

public sealed record StockLockKey(Guid ItemId, Guid WarehouseId, Guid LotId);