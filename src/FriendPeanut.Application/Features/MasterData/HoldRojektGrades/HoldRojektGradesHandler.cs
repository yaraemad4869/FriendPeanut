using FriendPeanut.Application.Abstractions;
using FriendPeanut.Domain.Common;

namespace FriendPeanut.Application.Features.MasterData.HoldRojektGrades;

public sealed record RojektGradeRequest(string Code, string NameAr, Guid CategoryId);

public sealed class HoldRojektGradesHandler
{
    private readonly IPolicyDecisionStore _policies;
    public HoldRojektGradesHandler(IPolicyDecisionStore policies) { _policies = policies; }

    public async Task<Result<bool>> HandleAsync(RojektGradeRequest req, CancellationToken ct)
    {
        if (!await _policies.IsApprovedAsync("rojekt_grades", ct))
            return Result<bool>.PolicyPending("rojekt_grades", "درجات الروجيكت تنتظر اعتماد الإدارة.");
        return Result<bool>.Failure("NOT_IMPLEMENTED", "Approved; grades must be defined before enabling.");
    }
}