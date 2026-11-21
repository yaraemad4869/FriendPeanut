using FriendPeanut.Domain.Inventory;

namespace FriendPeanut.Application.Abstractions;

public interface IStockPostingRepository
{
    Task<bool> DocumentExistsAsync(Guid documentId, CancellationToken cancellationToken);

    Task<bool> IsDocumentPostedAsync(Guid documentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StockMovement>> GetMovementsAsync(
        Guid documentId,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<StockLockKey, decimal>> GetAvailableQuantitiesAsync(
        IReadOnlyCollection<StockLockKey> lockKeys,
        CancellationToken cancellationToken);

    Task AcquireStockLocksAsync(
        IReadOnlyCollection<StockLockKey> lockKeys,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StockMovement>> ApplyLedgerEntriesAsync(
        Guid documentId,
        Guid actorId,
        IReadOnlyCollection<StockMovement> movements,
        CancellationToken cancellationToken);

    Task UpdateBalancesAsync(
        IReadOnlyCollection<StockMovement> movements,
        bool negativeOverrideAuthorized,
        string? negativeOverrideReason,
        CancellationToken cancellationToken);

    Task MarkDocumentPostedAsync(
        Guid documentId,
        Guid actorId,
        long expectedVersion,
        CancellationToken cancellationToken);
}

public sealed record StockLockKey(
    Guid ItemId,
    Guid WarehouseId,
    Guid LotId,
    Guid UnitId);