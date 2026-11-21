namespace FriendPeanut.Application.Features.Inventory.PostStockDocument;

public sealed record PostStockDocumentCommand(
    Guid DocumentId,
    long ExpectedVersion,
    string IdempotencyKey,
    bool NegativeStockOverrideRequested,
    string? NegativeStockOverrideReason);
