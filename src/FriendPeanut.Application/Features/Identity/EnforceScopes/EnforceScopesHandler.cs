using FriendPeanut.Application.Abstractions;

namespace FriendPeanut.Application.Features.Identity.EnforceScopes;

public sealed record SetUserScopesCommand(Guid UserId, IReadOnlyList<UserScopeRecord> Scopes);

public sealed class EnforceScopesHandler
{
    private readonly IScopeStore _scopes;
    private readonly ISessionStore _sessions;
    public EnforceScopesHandler(IScopeStore scopes, ISessionStore sessions) { _scopes = scopes; _sessions = sessions; }

    public Task<IReadOnlyList<UserScopeRecord>> ListAsync(Guid userId, CancellationToken ct) => _scopes.ListForUserAsync(userId, ct);

    public async Task HandleAsync(SetUserScopesCommand cmd, CancellationToken ct)
    {
        await _scopes.ReplaceForUserAsync(cmd.UserId, cmd.Scopes, ct);
        await _sessions.RevokeAllForUserAsync(cmd.UserId, ct);
    }
}