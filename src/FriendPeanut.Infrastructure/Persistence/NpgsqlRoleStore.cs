using FriendPeanut.Application.Abstractions;
using Npgsql;

namespace FriendPeanut.Infrastructure.Persistence;

public sealed class NpgsqlRoleStore : IRoleStore
{
    private readonly NpgsqlDataSource _ds;
    public NpgsqlRoleStore(NpgsqlDataSource ds) { _ds = ds; }

    public async Task<IReadOnlyList<RoleRecord>> ListAsync(CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,code,name_ar,version from friend_peanut.roles order by code");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<RoleRecord>();
        while (await r.ReadAsync(ct)) list.Add(new RoleRecord(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetInt64(3)));
        return list;
    }
    public async Task<RoleRecord?> FindAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,code,name_ar,version from friend_peanut.roles where id=@id");
        cmd.Parameters.AddWithValue("id", id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new RoleRecord(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetInt64(3)) : null;
    }
    public async Task<Guid> CreateAsync(string code, string nameAr, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        await using var cmd = _ds.CreateCommand("insert into friend_peanut.roles (id,code,name_ar) values (@id,@c,@n)");
        cmd.Parameters.AddWithValue("id", id); cmd.Parameters.AddWithValue("c", code); cmd.Parameters.AddWithValue("n", nameAr);
        await cmd.ExecuteNonQueryAsync(ct);
        return id;
    }
    public async Task UpdateAsync(Guid id, string code, string nameAr, long expectedVersion, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("update friend_peanut.roles set code=@c, name_ar=@n, version=version+1 where id=@id and version=@v");
        cmd.Parameters.AddWithValue("id", id); cmd.Parameters.AddWithValue("c", code); cmd.Parameters.AddWithValue("n", nameAr); cmd.Parameters.AddWithValue("v", expectedVersion);
        var n = await cmd.ExecuteNonQueryAsync(ct); if (n == 0) throw new InvalidOperationException("CONFLICT_VERSION");
    }
    public async Task SetPermissionsAsync(Guid roleId, IReadOnlyList<Guid> permissionIds, CancellationToken ct)
    {
        await using (var del = _ds.CreateCommand("delete from friend_peanut.role_permissions where role_id=@r"))
        { del.Parameters.AddWithValue("r", roleId); await del.ExecuteNonQueryAsync(ct); }
        foreach (var pid in permissionIds)
        {
            await using var ins = _ds.CreateCommand("insert into friend_peanut.role_permissions (role_id,permission_id) values (@r,@p) on conflict do nothing");
            ins.Parameters.AddWithValue("r", roleId); ins.Parameters.AddWithValue("p", pid);
            await ins.ExecuteNonQueryAsync(ct);
        }
    }
    public async Task<IReadOnlyList<PermissionRecord>> ListPermissionsAsync(CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,code,description_ar,version from friend_peanut.permissions order by code");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<PermissionRecord>();
        while (await r.ReadAsync(ct)) list.Add(new PermissionRecord(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetInt64(3)));
        return list;
    }
    public async Task<IReadOnlyList<RoleRecord>> GetRolesForUserAsync(Guid userId, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select r.id,r.code,r.name_ar,r.version from friend_peanut.roles r join friend_peanut.user_roles ur on ur.role_id=r.id where ur.user_id=@u order by r.code");
        cmd.Parameters.AddWithValue("u", userId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<RoleRecord>();
        while (await r.ReadAsync(ct)) list.Add(new RoleRecord(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetInt64(3)));
        return list;
    }
    public async Task SetRolesForUserAsync(Guid userId, IReadOnlyList<Guid> roleIds, CancellationToken ct)
    {
        await using (var del = _ds.CreateCommand("delete from friend_peanut.user_roles where user_id=@u"))
        { del.Parameters.AddWithValue("u", userId); await del.ExecuteNonQueryAsync(ct); }
        foreach (var rid in roleIds)
        {
            await using var ins = _ds.CreateCommand("insert into friend_peanut.user_roles (user_id,role_id) values (@u,@r) on conflict do nothing");
            ins.Parameters.AddWithValue("u", userId); ins.Parameters.AddWithValue("r", rid);
            await ins.ExecuteNonQueryAsync(ct);
        }
    }
    public async Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select distinct p.code from friend_peanut.permissions p join friend_peanut.role_permissions rp on rp.permission_id=p.id join friend_peanut.user_roles ur on ur.role_id=rp.role_id where ur.user_id=@u");
        cmd.Parameters.AddWithValue("u", userId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var set = new HashSet<string>(StringComparer.Ordinal);
        while (await r.ReadAsync(ct)) set.Add(r.GetString(0));
        return set;
    }
}