using System.Security.Cryptography;
using System.Text;

namespace TaskCopy.Services;

/// <summary>
/// Password-derived AES-256-GCM helpers for encrypting sensitive snippet-store
/// payloads while keeping the existing SQLite provider.
/// </summary>
public static class StoreCrypto
{
    private const int SaltLen = 16;
    private const int NonceLen = 12;
    private const int TagLen = 16;
    private const int KeyLen = 32;
    private const int Pbkdf2Iters = 600_000;
    private const string TextPrefix = "tcenc:v1:";

    private static readonly byte[] BlobMagic = "TCS1"u8.ToArray();
    private static readonly byte[] TokenPurpose = "TaskCopy store encryption token v1"u8.ToArray();
    private static readonly byte[] TextAad = "TaskCopy store text v1"u8.ToArray();
    private static readonly byte[] BlobAad = "TaskCopy store blob v1"u8.ToArray();

    public static string MakePasswordToken(string password)
    {
        if (string.IsNullOrEmpty(password)) throw new ArgumentException("Password required.", nameof(password));

        var salt = RandomNumberGenerator.GetBytes(SaltLen);
        var key = DeriveKey(password, salt, Pbkdf2Iters);
        try
        {
            var verifier = HMACSHA256.HashData(key, TokenPurpose);
            return $"v1:{Convert.ToBase64String(salt)}:{Pbkdf2Iters}:{Convert.ToBase64String(verifier)}";
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public static bool TryDeriveKey(string token, string password, out byte[] key)
    {
        key = Array.Empty<byte>();
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(password)) return false;

        try
        {
            var parts = token.Split(':');
            if (parts.Length != 4 || parts[0] != "v1") return false;

            var salt = Convert.FromBase64String(parts[1]);
            if (!int.TryParse(parts[2], out var iters)) return false;
            var expected = Convert.FromBase64String(parts[3]);
            var candidateKey = DeriveKey(password, salt, iters);
            var verifier = HMACSHA256.HashData(candidateKey, TokenPurpose);
            if (!CryptographicOperations.FixedTimeEquals(expected, verifier))
            {
                CryptographicOperations.ZeroMemory(candidateKey);
                return false;
            }

            key = candidateKey;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsEncryptedText(string? value)
        => value?.StartsWith(TextPrefix, StringComparison.Ordinal) == true;

    public static string EncryptText(string plaintext, byte[] key)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceLen);
        var plainBytes = Encoding.UTF8.GetBytes(plaintext ?? string.Empty);
        var ciphertext = new byte[plainBytes.Length];
        var tag = new byte[TagLen];

        using (var aes = new AesGcm(key, TagLen))
        {
            aes.Encrypt(nonce, plainBytes, ciphertext, tag, TextAad);
        }

        CryptographicOperations.ZeroMemory(plainBytes);
        return TextPrefix
            + Convert.ToBase64String(nonce)
            + ":"
            + Convert.ToBase64String(ciphertext)
            + ":"
            + Convert.ToBase64String(tag);
    }

    public static string DecryptText(string value, byte[] key)
    {
        if (!IsEncryptedText(value)) return value;

        var parts = value[TextPrefix.Length..].Split(':');
        if (parts.Length != 3) throw new CryptographicException("Malformed encrypted text payload.");

        var nonce = Convert.FromBase64String(parts[0]);
        var ciphertext = Convert.FromBase64String(parts[1]);
        var tag = Convert.FromBase64String(parts[2]);
        var plaintext = new byte[ciphertext.Length];

        using (var aes = new AesGcm(key, TagLen))
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintext, TextAad);
        }

        try
        {
            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public static bool IsEncryptedBlob(byte[]? value)
        => value is not null
           && value.Length >= BlobMagic.Length + NonceLen + TagLen
           && value.AsSpan(0, BlobMagic.Length).SequenceEqual(BlobMagic);

    public static byte[] EncryptBytes(byte[] plaintext, byte[] key)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceLen);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagLen];
        using (var aes = new AesGcm(key, TagLen))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag, BlobAad);
        }

        var output = new byte[BlobMagic.Length + NonceLen + ciphertext.Length + TagLen];
        Buffer.BlockCopy(BlobMagic, 0, output, 0, BlobMagic.Length);
        Buffer.BlockCopy(nonce, 0, output, BlobMagic.Length, NonceLen);
        Buffer.BlockCopy(ciphertext, 0, output, BlobMagic.Length + NonceLen, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, output, BlobMagic.Length + NonceLen + ciphertext.Length, TagLen);
        return output;
    }

    public static byte[] DecryptBytes(byte[] value, byte[] key)
    {
        if (!IsEncryptedBlob(value)) return value;

        var ctLen = value.Length - BlobMagic.Length - NonceLen - TagLen;
        if (ctLen < 0) throw new CryptographicException("Malformed encrypted blob payload.");

        var nonce = value.AsSpan(BlobMagic.Length, NonceLen).ToArray();
        var ciphertext = value.AsSpan(BlobMagic.Length + NonceLen, ctLen).ToArray();
        var tag = value.AsSpan(value.Length - TagLen, TagLen).ToArray();
        var plaintext = new byte[ctLen];

        using var aes = new AesGcm(key, TagLen);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, BlobAad);
        return plaintext;
    }

    private static byte[] DeriveKey(string password, byte[] salt, int iterations)
        => Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            KeyLen);
}
