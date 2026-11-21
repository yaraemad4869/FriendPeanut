using System.Security.Cryptography;
using System.Text;
using FriendPeanut.Application.Abstractions;

namespace FriendPeanut.Application.Features.Identity.AuthenticateUser;

public sealed class InvalidCredentialsException : Exception { public InvalidCredentialsException() : base("بيانات الدخول غير صحيحة.") { } }
public sealed class AccountNotActiveException : Exception { public AccountNotActiveException() : base("الحساب غير مفعل أو مغلق.") { } }

public sealed class AuthenticateUserHandler
{
    public const int MaxFailedLogins = 5;
    private readonly IUserStore _users;
    private readonly IPasswordHasher _hasher;
    private readonly ISessionStore _sessions;
    private readonly IAuthChallengeStore _challenges;
    private readonly ILoginAttemptStore _attempts;

    public AuthenticateUserHandler(IUserStore users, IPasswordHasher hasher, ISessionStore sessions,
        IAuthChallengeStore challenges, ILoginAttemptStore attempts)
    { _users = users; _hasher = hasher; _sessions = sessions; _challenges = challenges; _attempts = attempts; }

    public async Task<AuthenticateUserResult> HandleAsync(AuthenticateUserCommand cmd, string accessChannel, string? sourceIp, string correlationId, CancellationToken ct)
    {
        var user = await _users.FindByUsernameAsync(cmd.Username.Trim(), ct);
        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
        {
            await _attempts.RecordAsync(null, false, "USER_NOT_FOUND", sourceIp, correlationId, ct);
            throw new InvalidCredentialsException();
        }
        if (user.Status != "ACTIVE")
        {
            await _attempts.RecordAsync(user.Id, false, "ACCOUNT_NOT_ACTIVE", sourceIp, correlationId, ct);
            throw new AccountNotActiveException();
        }

        if (!_hasher.Verify(cmd.Password, user.PasswordHash))
        {
            await _users.IncrementFailedLoginAsync(user.Id, ct);
            await _attempts.RecordAsync(user.Id, false, "BAD_PASSWORD", sourceIp, correlationId, ct);
            var refreshed = await _users.FindByIdAsync(user.Id, ct);
            if (refreshed is not null && refreshed.FailedLoginCount >= MaxFailedLogins)
                await _users.LockAsync(user.Id, ct);
            throw new InvalidCredentialsException();
        }

        await _users.ResetFailedLoginAsync(user.Id, ct);
        await _attempts.RecordAsync(user.Id, true, null, sourceIp, correlationId, ct);

        if (user.MustChangePassword)
        {
            var (tok, _) = await _challenges.CreateAsync(user.Id, "CHANGE_PASSWORD", TimeSpan.FromMinutes(15), ct);
            return new AuthenticateUserResult("CHANGE_PASSWORD", tok, null, accessChannel, null);
        }
        var (mfaTok, _) = await _challenges.CreateAsync(user.Id, "MFA_VERIFY", TimeSpan.FromMinutes(5), ct);
        return new AuthenticateUserResult("MFA_VERIFY", mfaTok, null, accessChannel, null);
    }

    internal static string Sha256Hex(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
}