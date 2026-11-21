using FriendPeanut.Application.Abstractions;

namespace FriendPeanut.Application.Features.Identity.ManageRoles;

public sealed class ManageRolesHandler
{
    private readonly IRoleStore _roles;
    private readonly ISessionStore _sessions;
    public ManageRolesHandler(IRoleStore roles, ISessionStore sessions) { _roles = roles; _sessions = sessions; }

    public Task<IReadOnlyList<RoleRecord>> ListAsync(CancellationToken ct) => _roles.ListAsync(ct);
    public Task<RoleRecord?> GetAsync(Guid id, CancellationToken ct) => _roles.FindAsync(id, ct);
    public Task<Guid> CreateAsync(CreateRoleCommand c, CancellationToken ct) => _roles.CreateAsync(c.Code, c.NameAr, ct);
    public Task UpdateAsync(UpdateRoleCommand c, CancellationToken ct) => _roles.UpdateAsync(c.Id, c.Code, c.NameAr, c.ExpectedVersion, ct);
    public Task SetPermissionsAsync(SetRolePermissionsCommand c, CancellationToken ct) => _roles.SetPermissionsAsync(c.RoleId, c.PermissionIds, ct);

    public async Task SetUserRolesAsync(SetUserRolesCommand c, CancellationToken ct)
    {
        await _roles.SetRolesForUserAsync(c.UserId, c.RoleIds, ct);
        await _sessions.RevokeAllForUserAsync(c.UserId, ct);
    }
}