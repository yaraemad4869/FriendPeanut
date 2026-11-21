using System.Globalization;
using System.Security.Cryptography;
using FriendPeanut.Application.Abstractions;

namespace FriendPeanut.Infrastructure.Identity;

public sealed class TotpService : ITotpService
{
    private static readonly byte[] Alphabet = System.Text.Encoding.ASCII.GetBytes("ABCDEFGHIJKLMNOPQRSTUVWXYZ234567");

    public string GenerateSecret() => Base32Encode(RandomNumberGenerator.GetBytes(20));

    public string BuildOtpAuthUri(string secret, string username)
        => $"otpauth://totp/FriendPeanut:{Uri.EscapeDataString(username)}?secret={secret}&issuer=FriendPeanut&algorithm=SHA1&digits=6&period=30";

    public bool Verify(string secret, string code, long timeStepTolerance = 1)
    {
        if (string.IsNullOrWhiteSpace(secret) || code.Length != 6 || !int.TryParse(code, out _)) return false;
        var current = CurrentTimeStep();
        for (long off = -timeStepTolerance; off <= timeStepTolerance; off++)
            if (Compute(secret, current + off) == code) return true;
        return false;
    }

    public long CurrentTimeStep() => DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

    // CA5350 (HMAC-SHA1) is suppressed project-wide in Directory.Build.props:
    // RFC 6238 §1.2 mandates HMAC-SHA1 for TOTP interoperability with authenticator apps.
    private static string Compute(string secret, long timeStep)
    {
        var key = Base32Decode(secret);
        var counter = BitConverter.GetBytes(timeStep);
        if (BitConverter.IsLittleEndian) Array.Reverse(counter);
        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counter);
        var offset = hash[^1] & 0x0F;
        var bin = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (bin % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static string Base32Encode(byte[] data)
    {
        var sb = new System.Text.StringBuilder();
        int buf = 0, bits = 0;
        foreach (var b in data)
        {
            buf = (buf << 8) | b; bits += 8;
            while (bits >= 5) { sb.Append((char)Alphabet[(buf >> (bits - 5)) & 0x1F]); bits -= 5; }
        }
        if (bits > 0) sb.Append((char)Alphabet[(buf << (5 - bits)) & 0x1F]);
        return sb.ToString();
    }

    private static byte[] Base32Decode(string s)
    {
        var outBytes = new List<byte>();
        int buf = 0, bits = 0;
        foreach (var c in s.TrimEnd('=').ToUpperInvariant())
        {
            var v = Array.IndexOf(Alphabet, (byte)c);
            if (v < 0) throw new FormatException("Invalid base32.");
            buf = (buf << 5) | v; bits += 5;
            if (bits >= 8) { outBytes.Add((byte)((buf >> (bits - 8)) & 0xFF)); bits -= 8; }
        }
        return outBytes.ToArray();
    }
}