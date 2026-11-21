using System.Security.Cryptography;
using System.Text;
using FriendPeanut.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace FriendPeanut.Infrastructure.Identity;

public sealed class MfaCrypto : IMfaCrypto
{
    private readonly byte[] _key;
    public MfaCrypto(IConfiguration config)
    {
        var raw = config["Mfa:EncryptionKey"] ?? throw new InvalidOperationException("Mfa:EncryptionKey missing in configuration.");
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
    }
    public string Encrypt(string plaintext)
    {
        using var aes = Aes.Create(); aes.Key = _key; aes.GenerateIV();
        using var enc = aes.CreateEncryptor();
        var pt = Encoding.UTF8.GetBytes(plaintext);
        var ct = enc.TransformFinalBlock(pt, 0, pt.Length);
        var combined = new byte[aes.IV.Length + ct.Length];
        Buffer.BlockCopy(aes.IV, 0, combined, 0, aes.IV.Length);
        Buffer.BlockCopy(ct, 0, combined, aes.IV.Length, ct.Length);
        return Convert.ToBase64String(combined);
    }
    public string Decrypt(string ciphertext)
    {
        var combined = Convert.FromBase64String(ciphertext);
        using var aes = Aes.Create(); aes.Key = _key;
        var iv = new byte[aes.BlockSize / 8];
        Buffer.BlockCopy(combined, 0, iv, 0, iv.Length);
        aes.IV = iv;
        using var dec = aes.CreateDecryptor();
        var pt = dec.TransformFinalBlock(combined, iv.Length, combined.Length - iv.Length);
        return Encoding.UTF8.GetString(pt);
    }
}