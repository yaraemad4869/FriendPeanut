using System.Security.Cryptography;
using System.Text;
using FriendPeanut.Application.Abstractions;

namespace FriendPeanut.Application.Features.Identity.EnrollAndVerifyMfa;

public sealed class SetupMfaHandler
{
    private readonly IMfaCredentialStore _creds;
    private readonly IMfaCrypto _crypto;
    private readonly ITotpService _totp;
    private readonly IUserStore _users;

    public SetupMfaHandler(IMfaCredentialStore creds, IMfaCrypto crypto, ITotpService totp, IUserStore users)
    { _creds = creds; _crypto = crypto; _totp = totp; _users = users; }

    public async Task<MfaSetupResult> HandleAsync(SetupMfaCommand cmd, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(cmd.UserId, ct) ?? throw new InvalidOperationException("User not found.");
        var secret = _totp.GenerateSecret();
        await _creds.UpsertAsync(cmd.UserId, _crypto.Encrypt(secret), "v1", confirmed: false, ct);
        return new MfaSetupResult(secret, _totp.BuildOtpAuthUri(secret, user.Username));
    }
}

public sealed class VerifyMfaHandler
{
    private readonly IMfaCredentialStore _creds;
    private readonly IMfaCrypto _crypto;
    private readonly ITotpService _totp;
    private readonly IUserStore _users;
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;

    public VerifyMfaHandler(IMfaCredentialStore creds, IMfaCrypto crypto, ITotpService totp,
        IUserStore users, ISessionStore sessions, IClock clock)
    { _creds = creds; _crypto = crypto; _totp = totp; _users = users; _sessions = sessions; _clock = clock; }

    public async Task<VerifyMfaResult> HandleAsync(VerifyMfaCommand cmd, string accessChannel, CancellationToken ct)
    {
        var cred = await _creds.FindForUserAsync(cmd.UserId, ct) ?? throw new InvalidOperationException("MFA not enrolled.");
        var secret = _crypto.Decrypt(cred.SecretCiphertext);
        if (!_totp.Verify(secret, cmd.Code))
            return new VerifyMfaResult(false, null, null, null);

        var step = _totp.CurrentTimeStep();
        if (cred.LastAcceptedTimeStep is not null && step <= cred.LastAcceptedTimeStep)
            return new VerifyMfaResult(false, null, null, null);

        await _creds.RecordAcceptedTimeStepAsync(cmd.UserId, step, ct);

        var user = await _users.FindByIdAsync(cmd.UserId, ct) ?? throw new InvalidOperationException("User not found.");
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var hash = Sha256Hex(rawToken);
        var now = _clock.UtcNow;
        var lifetime = TimeSpan.FromHours(8);
        var sessionId = await _sessions.CreateAsync(user.Id, hash, null, accessChannel, lifetime, user.SecurityStamp, now, ct);
        return new VerifyMfaResult(true, rawToken, sessionId, now.Add(lifetime));
    }

    private static string Sha256Hex(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
}

public sealed record VerifyMfaResult(bool Success, string? SessionToken, Guid? SessionId, DateTimeOffset? ExpiresAt);
