namespace FriendPeanut.Application.Abstractions;

public sealed record PolicyDecisionRecord(string PolicyKey, string Status, string? ApprovedConfigurationJson, Guid? ApprovedBy, DateTimeOffset? ApprovedAt);

public interface IPolicyDecisionStore
{
    Task<PolicyDecisionRecord?> FindAsync(string policyKey, CancellationToken ct);
    Task<bool> IsApprovedAsync(string policyKey, CancellationToken ct);
}