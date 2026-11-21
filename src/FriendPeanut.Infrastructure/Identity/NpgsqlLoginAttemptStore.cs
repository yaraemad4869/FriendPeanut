using FriendPeanut.Application.Abstractions;
using Npgsql;

namespace FriendPeanut.Infrastructure.Persistence;

public sealed class NpgsqlLoginAttemptStore : ILoginAttemptStore
{
    private readonly NpgsqlDataSource _ds;
    public NpgsqlLoginAttemptStore(NpgsqlDataSource ds) { _ds = ds; }

    public async Task RecordAsync(Guid? userId, bool succeeded, string? failureCode, string? sourceIp, string correlationId, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("insert into friend_peanut.login_attempts (user_id,occurred_at,succeeded,failure_code,source_ip,correlation_id) values (@u,now(),@s,@f,@ip,@c)");
        cmd.Parameters.AddWithValue("u", (object?)userId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("s", succeeded);
        cmd.Parameters.AddWithValue("f", (object?)failureCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ip", (object?)sourceIp ?? DBNull.Value);
        cmd.Parameters.AddWithValue("c", correlationId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
    public async Task<IReadOnlyList<LoginAttemptRecord>> ListRecentAsync(int take, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,user_id,occurred_at,succeeded,failure_code,source_ip,correlation_id from friend_peanut.login_attempts order by occurred_at desc limit @t");
        cmd.Parameters.AddWithValue("t", take);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<LoginAttemptRecord>();
        while (await r.ReadAsync(ct))
            list.Add(new LoginAttemptRecord(r.GetGuid(0), r.IsDBNull(1) ? null : r.GetGuid(1),
                r.GetFieldValue<DateTimeOffset>(2), r.GetBoolean(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetFieldValue<string>(5),
                r.GetString(6)));
        return list;
    }
}