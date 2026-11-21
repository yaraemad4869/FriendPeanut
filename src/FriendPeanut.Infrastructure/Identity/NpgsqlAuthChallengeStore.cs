using System.Security.Cryptography;
using System.Text;
using FriendPeanut.Application.Abstractions;
using Npgsql;

namespace FriendPeanut.Infrastructure.Persistence;

public sealed class NpgsqlAuthChallengeStore : IAuthChallengeStore
{
    private readonly NpgsqlDataSource _ds;
    public NpgsqlAuthChallengeStore(NpgsqlDataSource ds) { _ds = ds; }

    public async Task<(string token, AuthChallengeRecord record)> CreateAsync(Guid userId, string nextStep, TimeSpan lifetime, CancellationToken ct)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var hash = Sha256Hex(token);
        var id = Guid.NewGuid();
        await using var cmd = _ds.CreateCommand("insert into friend_peanut.auth_challenges (id,user_id,challenge_hash,next_step,expires_at) values (@id,@u,@h,@n,now()+@l)");
        cmd.Parameters.AddWithValue("id", id); cmd.Parameters.AddWithValue("u", userId); cmd.Parameters.AddWithValue("h", hash);
        cmd.Parameters.AddWithValue("n", nextStep); cmd.Parameters.AddWithValue("l", lifetime);
        await cmd.ExecuteNonQueryAsync(ct);
        return (token, new AuthChallengeRecord(id, userId, hash, nextStep, DateTimeOffset.UtcNow + lifetime, null, 0));
    }
    public async Task<AuthChallengeRecord?> ConsumeAsync(string token, CancellationToken ct)
    {
        var hash = Sha256Hex(token);
        await using var cmd = _ds.CreateCommand("update friend_peanut.auth_challenges set consumed_at=now() where challenge_hash=@h and consumed_at is null and expires_at > now() returning id,user_id,challenge_hash,next_step,expires_at,consumed_at,attempts");
        cmd.Parameters.AddWithValue("h", hash);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new AuthChallengeRecord(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3),
            r.GetFieldValue<DateTimeOffset>(4),
            r.IsDBNull(5) ? null : r.GetFieldValue<DateTimeOffset>(5), r.GetInt32(6));
    }
    public async Task RecordAttemptAsync(Guid challengeId, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("update friend_peanut.auth_challenges set attempts=attempts+1 where id=@id");
        cmd.Parameters.AddWithValue("id", challengeId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
    private static string Sha256Hex(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
}