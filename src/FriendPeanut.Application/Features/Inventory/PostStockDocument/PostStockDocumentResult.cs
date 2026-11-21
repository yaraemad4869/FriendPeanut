namespace FriendPeanut.Application.Features.Inventory.PostStockDocument;

public sealed record PostStockDocumentResult(
    Guid DocumentId,
    string Status,
    long Version,
    bool Replayed);
