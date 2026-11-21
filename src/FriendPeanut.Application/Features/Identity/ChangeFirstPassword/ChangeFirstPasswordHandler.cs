using FriendPeanut.Application.Abstractions;

namespace FriendPeanut.Application.Features.Identity.ChangeFirstPassword;

public sealed class ChangeFirstPasswordHandler
{
    private readonly IAuthChallengeStore _challenges;
    private readonly IUserStore _users;
    private readonly IPasswordHasher _hasher;

    public ChangeFirstPasswordHandler(IAuthChallengeStore challenges, IUserStore users, IPasswordHasher hasher)
    { _challenges = challenges; _users = users; _hasher = hasher; }

    public async Task<ChangeFirstPasswordResult> HandleAsync(ChangeFirstPasswordCommand cmd, CancellationToken ct)
    {
        var ch = await _challenges.ConsumeAsync(cmd.PreAuthToken, ct) ?? throw new InvalidOperationException("Challenge expired.");
        if (ch.NextStep != "CHANGE_PASSWORD") throw new InvalidOperationException("Challenge step mismatch.");
        await _users.SetPasswordHashAsync(ch.UserId, _hasher.Hash(cmd.NewPassword), mustChange: false, ct);
        await _users.RotateSecurityStampAsync(ch.UserId, ct);
        var (tok, _) = await _challenges.CreateAsync(ch.UserId, "MFA_VERIFY", TimeSpan.FromMinutes(5), ct);
        return new ChangeFirstPasswordResult("MFA_VERIFY", tok);
    }
}