using FriendPeanut.Application.Abstractions;
using Npgsql;

namespace FriendPeanut.Infrastructure.Persistence;

public sealed class NpgsqlUserStore : IUserStore
{
    private readonly NpgsqlDataSource _ds;
    public NpgsqlUserStore(NpgsqlDataSource ds) { _ds = ds; }

    public async Task<UserRecord?> FindByUsernameAsync(string username, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,slot_no,username,display_name,password_hash,status,must_change_password,remote_access_enabled,failed_login_count,security_stamp,version,created_at from friend_peanut.app_users where username=@u");
        cmd.Parameters.AddWithValue("u", username);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? ReadUser(r) : null;
    }
    public async Task<UserRecord?> FindByIdAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,slot_no,username,display_name,password_hash,status,must_change_password,remote_access_enabled,failed_login_count,security_stamp,version,created_at from friend_peanut.app_users where id=@id");
        cmd.Parameters.AddWithValue("id", id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? ReadUser(r) : null;
    }
    public async Task<IReadOnlyList<UserRecord>> ListAsync(CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,slot_no,username,display_name,password_hash,status,must_change_password,remote_access_enabled,failed_login_count,security_stamp,version,created_at from friend_peanut.app_users order by slot_no");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<UserRecord>();
        while (await r.ReadAsync(ct)) list.Add(ReadUser(r));
        return list;
    }
    public Task IncrementFailedLoginAsync(Guid userId, CancellationToken ct) => Exec("update friend_peanut.app_users set failed_login_count=failed_login_count+1, version=version+1 where id=@id", ("id", userId), ct);
    public Task ResetFailedLoginAsync(Guid userId, CancellationToken ct) => Exec("update friend_peanut.app_users set failed_login_count=0, version=version+1 where id=@id", ("id", userId), ct);
    public Task LockAsync(Guid userId, CancellationToken ct) => Exec("update friend_peanut.app_users set status='LOCKED', version=version+1 where id=@id", ("id", userId), ct);
    public Task SetStatusAsync(Guid userId, string status, CancellationToken ct) => Exec("update friend_peanut.app_users set status=@s, version=version+1 where id=@id", ("id", userId), ct, ("s", status));
    public Task SetPasswordHashAsync(Guid userId, string hash, bool mustChange, CancellationToken ct) => Exec("update friend_peanut.app_users set password_hash=@h, must_change_password=@m, version=version+1 where id=@id", ("id", userId), ct, ("h", hash), ("m", mustChange));
    public Task RotateSecurityStampAsync(Guid userId, CancellationToken ct) => Exec("update friend_peanut.app_users set security_stamp=gen_random_uuid(), version=version+1 where id=@id", ("id", userId), ct);
    public Task SetRemoteAccessAsync(Guid userId, bool enabled, CancellationToken ct) => Exec("update friend_peanut.app_users set remote_access_enabled=@e, version=version+1 where id=@id", ("id", userId), ct, ("e", enabled));

    private async Task Exec(string sql, (string k, object v) p, CancellationToken ct, params (string k, object v)[] more)
    {
        await using var cmd = _ds.CreateCommand(sql);
        cmd.Parameters.AddWithValue(p.k, p.v);
        foreach (var x in more) cmd.Parameters.AddWithValue(x.k, x.v);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static UserRecord ReadUser(NpgsqlDataReader r) => new(
        r.GetGuid(0), r.GetInt16(1), r.GetString(2), r.GetString(3),
        r.IsDBNull(4) ? null : r.GetString(4), r.GetString(5),
        r.GetBoolean(6), r.GetBoolean(7), r.GetInt32(8), r.GetGuid(9), r.GetInt64(10),
        r.GetFieldValue<DateTimeOffset>(11));
}