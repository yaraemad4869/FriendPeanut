using FriendPeanut.Application.Abstractions;
using FriendPeanut.Domain.Common;

namespace FriendPeanut.Application.Features.Identity.ConfigureMainUsers;

public sealed record ConfigureMainUsersCommand(IReadOnlyList<Guid> UserIds, IReadOnlyList<Guid> RoleIds);

public sealed class ConfigureMainUsersHandler
{
    private readonly IPolicyDecisionStore _policies;
    public ConfigureMainUsersHandler(IPolicyDecisionStore policies) { _policies = policies; }

    public async Task<Result<bool>> HandleAsync(ConfigureMainUsersCommand cmd, CancellationToken ct)
    {
        if (!await _policies.IsApprovedAsync("main_user_permissions", ct))
            return Result<bool>.PolicyPending("main_user_permissions",
                "مصفوفة صلاحيات المستخدمين الرئيسيين تنتظر قرار الإدارة.");
        return Result<bool>.Failure("NOT_IMPLEMENTED", "Approved; main-user matrix must be defined before enabling.");
    }
}