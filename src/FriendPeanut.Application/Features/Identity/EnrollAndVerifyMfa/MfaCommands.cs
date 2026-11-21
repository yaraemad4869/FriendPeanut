namespace FriendPeanut.Application.Features.Identity.EnrollAndVerifyMfa;

public sealed record SetupMfaCommand(Guid UserId);
public sealed record MfaSetupResult(string SecretBase32, string OtpAuthUri);
public sealed record VerifyMfaCommand(Guid UserId, string Code);