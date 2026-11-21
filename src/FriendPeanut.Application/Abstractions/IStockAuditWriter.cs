namespace FriendPeanut.Application.Abstractions;

public interface IStockAuditWriter
{
    Task WriteAsync(
        string action,
        Guid documentId,
        Guid actorId,
        string details,
        string? reason,
        CancellationToken cancellationToken);
}