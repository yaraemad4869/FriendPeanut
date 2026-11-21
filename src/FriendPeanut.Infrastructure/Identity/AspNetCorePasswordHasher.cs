using FriendPeanut.Application.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace FriendPeanut.Infrastructure.Identity;

public sealed class AspNetCorePasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _inner = new();
    private static readonly object Subject = new();
    public string Hash(string plaintext) => _inner.HashPassword(Subject, plaintext);
    public bool Verify(string plaintext, string hash) =>
        _inner.VerifyHashedPassword(Subject, hash, plaintext) != PasswordVerificationResult.Failed;
}