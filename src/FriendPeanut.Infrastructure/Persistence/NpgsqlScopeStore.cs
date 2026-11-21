using FriendPeanut.Application.Abstractions;
using Npgsql;

namespace FriendPeanut.Infrastructure.Persistence;

public sealed class NpgsqlScopeStore : IScopeStore
{
    private readonly NpgsqlDataSource _ds;
    public NpgsqlScopeStore(NpgsqlDataSource ds) { _ds = ds; }

    public async Task<IReadOnlyList<UserScopeRecord>> ListForUserAsync(Guid userId, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,user_id,scope_kind,warehouse_id,factory_id,department_code from friend_peanut.user_scopes where user_id=@u");
        cmd.Parameters.AddWithValue("u", userId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<UserScopeRecord>();
        while (await r.ReadAsync(ct))
            list.Add(new UserScopeRecord(r.GetGuid(0), r.GetGuid(1), r.GetString(2),
                r.IsDBNull(3) ? null : r.GetGuid(3),
                r.IsDBNull(4) ? null : r.GetGuid(4),
                r.IsDBNull(5) ? null : r.GetString(5)));
        return list;
    }
    public async Task ReplaceForUserAsync(Guid userId, IReadOnlyList<UserScopeRecord> scopes, CancellationToken ct)
    {
        await using (var del = _ds.CreateCommand("delete from friend_peanut.user_scopes where user_id=@u"))
        { del.Parameters.AddWithValue("u", userId); await del.ExecuteNonQueryAsync(ct); }
        foreach (var s in scopes)
        {
            await using var ins = _ds.CreateCommand("insert into friend_peanut.user_scopes (user_id,scope_kind,warehouse_id,factory_id,department_code) values (@u,@k,@w,@f,@d)");
            ins.Parameters.AddWithValue("u", userId); ins.Parameters.AddWithValue("k", s.ScopeKind);
            ins.Parameters.AddWithValue("w", (object?)s.WarehouseId ?? DBNull.Value);
            ins.Parameters.AddWithValue("f", (object?)s.FactoryId ?? DBNull.Value);
            ins.Parameters.AddWithValue("d", (object?)s.DepartmentCode ?? DBNull.Value);
            await ins.ExecuteNonQueryAsync(ct);
        }
    }
}