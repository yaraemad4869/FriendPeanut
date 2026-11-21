namespace FriendPeanut.Application.Abstractions;

public sealed record RoleRecord(Guid Id, string Code, string NameAr, long Version);
public sealed record PermissionRecord(Guid Id, string Code, string DescriptionAr, long Version);

public interface IRoleStore
{
    Task<IReadOnlyList<RoleRecord>> ListAsync(CancellationToken ct);
    Task<RoleRecord?> FindAsync(Guid id, CancellationToken ct);
    Task<Guid> CreateAsync(string code, string nameAr, CancellationToken ct);
    Task UpdateAsync(Guid id, string code, string nameAr, long expectedVersion, CancellationToken ct);
    Task SetPermissionsAsync(Guid roleId, IReadOnlyList<Guid> permissionIds, CancellationToken ct);
    Task<IReadOnlyList<PermissionRecord>> ListPermissionsAsync(CancellationToken ct);
    Task<IReadOnlyList<RoleRecord>> GetRolesForUserAsync(Guid userId, CancellationToken ct);
    Task SetRolesForUserAsync(Guid userId, IReadOnlyList<Guid> roleIds, CancellationToken ct);
    Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct);
}