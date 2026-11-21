namespace FriendPeanut.Application.Abstractions;

public interface ITotpService
{
    string GenerateSecret();
    string BuildOtpAuthUri(string secret, string username);
    bool Verify(string secret, string code, long timeStepTolerance = 1);
    long CurrentTimeStep();
}