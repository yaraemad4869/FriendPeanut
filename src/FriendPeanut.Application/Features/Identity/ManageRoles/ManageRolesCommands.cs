namespace FriendPeanut.Application.Features.Identity.ManageRoles;

public sealed record CreateRoleCommand(string Code, string NameAr);
public sealed record UpdateRoleCommand(Guid Id, string Code, string NameAr, long ExpectedVersion);
public sealed record SetRolePermissionsCommand(Guid RoleId, IReadOnlyList<Guid> PermissionIds);
public sealed record SetUserRolesCommand(Guid UserId, IReadOnlyList<Guid> RoleIds);