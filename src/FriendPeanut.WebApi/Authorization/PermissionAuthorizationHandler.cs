using FriendPeanut.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;

namespace FriendPeanut.WebApi.Authorization;

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }
    public PermissionRequirement(string permission) { Permission = permission; }
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ICurrentUser _user;
    public PermissionAuthorizationHandler(ICurrentUser user) { _user = user; }
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (_user.IsAuthenticated && _user.HasPermission(requirement.Permission)) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}