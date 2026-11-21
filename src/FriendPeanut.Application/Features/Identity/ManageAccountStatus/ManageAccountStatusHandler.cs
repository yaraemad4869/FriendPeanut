using FriendPeanut.Application.Abstractions;

namespace FriendPeanut.Application.Features.Identity.ManageAccountStatus;

public sealed class ManageAccountStatusHandler
{
    private readonly IUserStore _users;
    private readonly ISessionStore _sessions;
    private readonly IPasswordHasher _hasher;

    public ManageAccountStatusHandler(IUserStore users, ISessionStore sessions, IPasswordHasher hasher)
    { _users = users; _sessions = sessions; _hasher = hasher; }

    public async Task HandleSetStatusAsync(SetUserStatusCommand cmd, CancellationToken ct)
    {
        await _users.SetStatusAsync(cmd.UserId, cmd.Status, ct);
        if (cmd.Status != "ACTIVE")
        {
            await _sessions.RevokeAllForUserAsync(cmd.UserId, ct);
            await _users.RotateSecurityStampAsync(cmd.UserId, ct);
        }
    }

    public async Task HandleSetRemoteAccessAsync(SetRemoteAccessCommand cmd, CancellationToken ct)
    {
        await _users.SetRemoteAccessAsync(cmd.UserId, cmd.Enabled, ct);
        await _sessions.RevokeAllForUserAsync(cmd.UserId, ct);
    }

    public async Task HandleAdminResetPasswordAsync(AdminResetPasswordCommand cmd, CancellationToken ct)
    {
        await _users.SetPasswordHashAsync(cmd.UserId, _hasher.Hash(cmd.NewPassword), mustChange: true, ct);
        await _users.RotateSecurityStampAsync(cmd.UserId, ct);
        await _sessions.RevokeAllForUserAsync(cmd.UserId, ct);
    }
}