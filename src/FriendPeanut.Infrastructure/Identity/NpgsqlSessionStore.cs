using FriendPeanut.Application.Abstractions;
using Npgsql;

namespace FriendPeanut.Infrastructure.Persistence;

public sealed class NpgsqlSessionStore : ISessionStore
{
    private readonly NpgsqlDataSource _ds;
    public NpgsqlSessionStore(NpgsqlDataSource ds) { _ds = ds; }

    public async Task<SessionRecord?> FindByTokenHashAsync(string tokenHash, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,user_id,token_hash,device_id,access_channel,created_at,expires_at,revoked_at,security_stamp,mfa_verified_at from friend_peanut.auth_sessions where token_hash=@h and revoked_at is null and expires_at > now()");
        cmd.Parameters.AddWithValue("h", tokenHash);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new SessionRecord(r.GetGuid(0), r.GetGuid(1), r.GetString(2),
            r.IsDBNull(3) ? null : r.GetGuid(3), r.GetString(4),
            r.GetFieldValue<DateTimeOffset>(5), r.GetFieldValue<DateTimeOffset>(6),
            r.IsDBNull(7) ? null : r.GetFieldValue<DateTimeOffset>(7), r.GetGuid(8),
            r.IsDBNull(9) ? null : r.GetFieldValue<DateTimeOffset>(9));
    }
    public async Task<Guid> CreateAsync(Guid userId, string tokenHash, Guid? deviceId, string accessChannel, TimeSpan lifetime, Guid securityStamp, DateTimeOffset? mfaVerifiedAt, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        await using var cmd = _ds.CreateCommand("insert into friend_peanut.auth_sessions (id,user_id,token_hash,device_id,access_channel,created_at,expires_at,security_stamp,mfa_verified_at) values (@id,@u,@h,@d,@c,now(),now()+@l,@s,@m)");
        cmd.Parameters.AddWithValue("id", id); cmd.Parameters.AddWithValue("u", userId); cmd.Parameters.AddWithValue("h", tokenHash);
        cmd.Parameters.AddWithValue("d", (object?)deviceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("c", accessChannel); cmd.Parameters.AddWithValue("l", lifetime);
        cmd.Parameters.AddWithValue("s", securityStamp);
        cmd.Parameters.AddWithValue("m", (object?)mfaVerifiedAt ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return id;
    }
    public Task RevokeAsync(Guid sessionId, CancellationToken ct) => Exec("update friend_peanut.auth_sessions set revoked_at=now() where id=@id", ("id", sessionId), ct);
    public Task RevokeAllForUserAsync(Guid userId, CancellationToken ct) => Exec("update friend_peanut.auth_sessions set revoked_at=now() where user_id=@u and revoked_at is null", ("u", userId), ct);
    public Task MarkMfaVerifiedAsync(Guid sessionId, DateTimeOffset at, CancellationToken ct) => Exec("update friend_peanut.auth_sessions set mfa_verified_at=@t where id=@id", ("id", sessionId), ct, ("t", at));

    public async Task<IReadOnlyList<SessionRecord>> ListForUserAsync(Guid userId, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,user_id,token_hash,device_id,access_channel,created_at,expires_at,revoked_at,security_stamp,mfa_verified_at from friend_peanut.auth_sessions where user_id=@u order by created_at desc limit 50");
        cmd.Parameters.AddWithValue("u", userId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<SessionRecord>();
        while (await r.ReadAsync(ct))
            list.Add(new SessionRecord(r.GetGuid(0), r.GetGuid(1), r.GetString(2),
                r.IsDBNull(3) ? null : r.GetGuid(3), r.GetString(4),
                r.GetFieldValue<DateTimeOffset>(5), r.GetFieldValue<DateTimeOffset>(6),
                r.IsDBNull(7) ? null : r.GetFieldValue<DateTimeOffset>(7), r.GetGuid(8),
                r.IsDBNull(9) ? null : r.GetFieldValue<DateTimeOffset>(9)));
        return list;
    }

    private async Task Exec(string sql, (string k, object v) p, CancellationToken ct, params (string k, object v)[] more)
    {
        await using var cmd = _ds.CreateCommand(sql);
        cmd.Parameters.AddWithValue(p.k, p.v);
        foreach (var x in more) cmd.Parameters.AddWithValue(x.k, x.v);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}