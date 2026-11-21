namespace FriendPeanut.Application.Features.Identity.ManageAccountStatus;

public sealed record SetUserStatusCommand(Guid UserId, string Status);
public sealed record SetRemoteAccessCommand(Guid UserId, bool Enabled);
public sealed record AdminResetPasswordCommand(Guid UserId, string NewPassword, string Reason);