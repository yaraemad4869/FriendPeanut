using FriendPeanut.Application.Abstractions;
using Npgsql;

namespace FriendPeanut.Infrastructure.Persistence;

public sealed class NpgsqlPolicyDecisionStore : IPolicyDecisionStore
{
    private readonly NpgsqlDataSource _ds;
    public NpgsqlPolicyDecisionStore(NpgsqlDataSource ds) { _ds = ds; }

    public async Task<PolicyDecisionRecord?> FindAsync(string policyKey, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select policy_key,status,approved_configuration::text,approved_by,approved_at from friend_peanut.policy_decisions where policy_key=@k");
        cmd.Parameters.AddWithValue("k", policyKey);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new PolicyDecisionRecord(r.GetString(0), r.GetString(1),
            r.IsDBNull(2) ? null : r.GetString(2),
            r.IsDBNull(3) ? null : r.GetGuid(3),
            r.IsDBNull(4) ? null : r.GetFieldValue<DateTimeOffset>(4));
    }
    public async Task<bool> IsApprovedAsync(string policyKey, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select exists(select 1 from friend_peanut.policy_decisions where policy_key=@k and status='APPROVED')");
        cmd.Parameters.AddWithValue("k", policyKey);
        return (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
    }
}