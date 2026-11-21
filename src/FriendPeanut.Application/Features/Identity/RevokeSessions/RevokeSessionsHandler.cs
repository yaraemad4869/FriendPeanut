using FriendPeanut.Application.Abstractions;

namespace FriendPeanut.Application.Features.Identity.RevokeSessions;

public sealed record RevokeSessionsCommand(Guid UserId);

public sealed class RevokeSessionsHandler
{
    private readonly ISessionStore _sessions;
    public RevokeSessionsHandler(ISessionStore sessions) { _sessions = sessions; }
    public Task HandleAsync(RevokeSessionsCommand cmd, CancellationToken ct) => _sessions.RevokeAllForUserAsync(cmd.UserId, ct);
}