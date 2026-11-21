namespace FriendPeanut.Application.Features.Identity.AuthenticateUser;

public sealed record AuthenticateUserCommand(string Username, string Password);
public sealed record AuthenticateUserResult(string NextStep, string PreAuthToken, Guid? SessionId, string AccessChannel, DateTimeOffset? ExpiresAt);