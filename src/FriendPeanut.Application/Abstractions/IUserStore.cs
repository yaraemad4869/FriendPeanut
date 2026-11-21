namespace FriendPeanut.Application.Abstractions;

public sealed record UserRecord(
    Guid Id, int SlotNo, string Username, string DisplayName, string? PasswordHash,
    string Status, bool MustChangePassword, bool RemoteAccessEnabled,
    int FailedLoginCount, Guid SecurityStamp, long Version, DateTimeOffset CreatedAt);

public interface IUserStore
{
    Task<UserRecord?> FindByUsernameAsync(string username, CancellationToken ct);
    Task<UserRecord?> FindByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<UserRecord>> ListAsync(CancellationToken ct);
    Task IncrementFailedLoginAsync(Guid userId, CancellationToken ct);
    Task ResetFailedLoginAsync(Guid userId, CancellationToken ct);
    Task LockAsync(Guid userId, CancellationToken ct);
    Task SetStatusAsync(Guid userId, string status, CancellationToken ct);
    Task SetPasswordHashAsync(Guid userId, string hash, bool mustChange, CancellationToken ct);
    Task RotateSecurityStampAsync(Guid userId, CancellationToken ct);
    Task SetRemoteAccessAsync(Guid userId, bool enabled, CancellationToken ct);
}