namespace FriendPeanut.Application.Abstractions;

public sealed record MfaCredentialRecord(Guid Id, Guid UserId, string SecretCiphertext, string EncryptionKeyId, bool IsConfirmed, long? LastAcceptedTimeStep);

public interface IMfaCredentialStore
{
    Task<MfaCredentialRecord?> FindForUserAsync(Guid userId, CancellationToken ct);
    Task UpsertAsync(Guid userId, string secretCiphertext, string encryptionKeyId, bool confirmed, CancellationToken ct);
    Task RecordAcceptedTimeStepAsync(Guid userId, long timeStep, CancellationToken ct);
}