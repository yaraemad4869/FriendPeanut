using FriendPeanut.Application.Abstractions;
using FriendPeanut.Domain.Common;

namespace FriendPeanut.Application.Features.MasterData.HoldPendingUnits;

public sealed class HoldPendingUnitsHandler
{
    private readonly IPolicyDecisionStore _policies;
    public HoldPendingUnitsHandler(IPolicyDecisionStore policies) { _policies = policies; }

    public async Task<Result<bool>> HandleAsync(Guid itemId, CancellationToken ct)
    {
        if (!await _policies.IsApprovedAsync("packaging_units", ct))
            return Result<bool>.PolicyPending("packaging_units", "وحدات مواد التعبئة تنتظر اعتماد الإدارة.");
        return Result<bool>.Failure("NOT_IMPLEMENTED", "Approved; unit mappings must be defined before enabling.");
    }
}