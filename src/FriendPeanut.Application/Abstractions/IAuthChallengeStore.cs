namespace FriendPeanut.Application.Abstractions;

public sealed record AuthChallengeRecord(Guid Id, Guid UserId, string ChallengeHash, string NextStep,
    DateTimeOffset ExpiresAt, DateTimeOffset? ConsumedAt, int Attempts);

public interface IAuthChallengeStore
{
    Task<(string token, AuthChallengeRecord record)> CreateAsync(Guid userId, string nextStep, TimeSpan lifetime, CancellationToken ct);
    Task<AuthChallengeRecord?> ConsumeAsync(string token, CancellationToken ct);
    Task RecordAttemptAsync(Guid challengeId, CancellationToken ct);
}