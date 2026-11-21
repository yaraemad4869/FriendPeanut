namespace FriendPeanut.Application.Abstractions;

public interface IStockAuditWriter
{
    Task WriteAsync(
        string action,
        Guid documentId,
        string details,
        CancellationToken cancellationToken);
}
