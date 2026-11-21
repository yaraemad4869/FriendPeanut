using FriendPeanut.Application.Abstractions;

namespace FriendPeanut.Application.Features.Identity.LockAccountAndLoginHistory;

public sealed class LoginHistoryQuery
{
    private readonly ILoginAttemptStore _attempts;
    public LoginHistoryQuery(ILoginAttemptStore attempts) { _attempts = attempts; }
    public Task<IReadOnlyList<LoginAttemptRecord>> HandleAsync(int take, CancellationToken ct)
        => _attempts.ListRecentAsync(take is < 1 or > 500 ? 25 : take, ct);
}