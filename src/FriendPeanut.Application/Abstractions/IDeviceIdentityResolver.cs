namespace FriendPeanut.Application.Abstractions;

/// <summary>
/// Trusted adapter for resolving the external device identity. The resolver reads
/// from the trusted gateway's identity, not from a client-supplied header. Fail closed
/// when identity cannot be verified.
/// </summary>
public interface IDeviceIdentityResolver
{
    Task<string?> ResolveExternalKeyAsync(CancellationToken ct);
}