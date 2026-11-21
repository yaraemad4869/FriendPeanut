namespace FriendPeanut.Application.Abstractions;

public interface IMfaCrypto
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}