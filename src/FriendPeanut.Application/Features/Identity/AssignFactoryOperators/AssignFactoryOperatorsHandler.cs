using FriendPeanut.Application.Abstractions;
using FriendPeanut.Domain.Common;

namespace FriendPeanut.Application.Features.Identity.AssignFactoryOperators;

public sealed record AssignFactoryOperatorCommand(Guid UserId, Guid FactoryId);

public sealed class AssignFactoryOperatorsHandler
{
    private readonly IPolicyDecisionStore _policies;
    private readonly IScopeStore _scopes;
    private readonly ISessionStore _sessions;

    public AssignFactoryOperatorsHandler(IPolicyDecisionStore policies, IScopeStore scopes, ISessionStore sessions)
    { _policies = policies; _scopes = scopes; _sessions = sessions; }

    public async Task<Result<bool>> HandleAsync(AssignFactoryOperatorCommand cmd, CancellationToken ct)
    {
        if (!await _policies.IsApprovedAsync("factory_operator_assignments", ct))
            return Result<bool>.PolicyPending("factory_operator_assignments",
                "توزيع صلاحيات مشغلي المصانع ينتظر قرار الإدارة.");

        var current = await _scopes.ListForUserAsync(cmd.UserId, ct);
        var next = current.Append(new UserScopeRecord(Guid.NewGuid(), cmd.UserId, "FACTORY", null, cmd.FactoryId, null)).ToArray();
        await _scopes.ReplaceForUserAsync(cmd.UserId, next, ct);
        await _sessions.RevokeAllForUserAsync(cmd.UserId, ct);
        return Result<bool>.Success(true);
    }
}