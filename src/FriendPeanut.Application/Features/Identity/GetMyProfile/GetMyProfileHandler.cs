using FriendPeanut.Application.Abstractions;

namespace FriendPeanut.Application.Features.Identity.GetMyProfile;

public sealed record MyProfileDto(Guid Id, int SlotNo, string Username, string DisplayName, string Status,
    bool MustChangePassword, bool RemoteAccessEnabled, bool MfaEnrolled,
    IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);

public sealed class GetMyProfileHandler
{
    private readonly IUserStore _users;
    private readonly IRoleStore _roles;
    private readonly IMfaCredentialStore _mfa;

    public GetMyProfileHandler(IUserStore users, IRoleStore roles, IMfaCredentialStore mfa)
    { _users = users; _roles = roles; _mfa = mfa; }

    public async Task<MyProfileDto?> HandleAsync(Guid userId, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(userId, ct);
        if (user is null) return null;
        var roles = await _roles.GetRolesForUserAsync(userId, ct);
        var permissions = await _roles.GetEffectivePermissionsAsync(userId, ct);
        var mfa = await _mfa.FindForUserAsync(userId, ct);
        return new MyProfileDto(user.Id, user.SlotNo, user.Username, user.DisplayName, user.Status,
            user.MustChangePassword, user.RemoteAccessEnabled, mfa?.IsConfirmed == true,
            roles.Select(r => r.Code).ToArray(), permissions.ToArray());
    }
}