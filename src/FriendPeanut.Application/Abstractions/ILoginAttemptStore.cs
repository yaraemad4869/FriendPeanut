namespace FriendPeanut.Application.Abstractions;

public sealed record LoginAttemptRecord(Guid Id, Guid? UserId, DateTimeOffset OccurredAt, bool Succeeded, string? FailureCode, string? SourceIp, string CorrelationId);

public interface ILoginAttemptStore
{
    Task RecordAsync(Guid? userId, bool succeeded, string? failureCode, string? sourceIp, string correlationId, CancellationToken ct);
    Task<IReadOnlyList<LoginAttemptRecord>> ListRecentAsync(int take, CancellationToken ct);
}