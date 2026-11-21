namespace FriendPeanut.Application.Abstractions;

public sealed record SessionRecord(
    Guid Id, Guid UserId, string TokenHash, Guid? DeviceId, string AccessChannel,
    DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? RevokedAt,
    Guid SecurityStamp, DateTimeOffset? MfaVerifiedAt);

public interface ISessionStore
{
    Task<SessionRecord?> FindByTokenHashAsync(string tokenHash, CancellationToken ct);
    Task<Guid> CreateAsync(Guid userId, string tokenHash, Guid? deviceId, string accessChannel, TimeSpan lifetime, Guid securityStamp, DateTimeOffset? mfaVerifiedAt, CancellationToken ct);
    Task RevokeAsync(Guid sessionId, CancellationToken ct);
    Task RevokeAllForUserAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<SessionRecord>> ListForUserAsync(Guid userId, CancellationToken ct);
    Task MarkMfaVerifiedAsync(Guid sessionId, DateTimeOffset at, CancellationToken ct);
}