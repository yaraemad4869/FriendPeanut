namespace FriendPeanut.Application.Features.Identity.ChangeFirstPassword;

public sealed record ChangeFirstPasswordCommand(string PreAuthToken, string NewPassword);
public sealed record ChangeFirstPasswordResult(string NextStep, string PreAuthToken);