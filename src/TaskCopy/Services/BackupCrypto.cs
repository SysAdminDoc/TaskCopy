using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TaskCopy.Services;

/// <summary>
/// F49: AES-256-GCM at-rest encryption for the rotated `.bak.{N}.enc` backups.
/// Key is derived from a user password via PBKDF2(SHA-256, 600,000 iters) per
/// the OWASP 2023 recommendation. Salt is per-file (16 random bytes) so the
/// same password produces different ciphertexts every time.
///
/// File format on disk:
///   [magic 4 bytes "TCB1"][salt 16][nonce 12][ciphertext N][tag 16]
///
/// Password rules:
/// - The password itself is NEVER persisted. SettingsStore holds a small
///   PBKDF2 verification token (salt + iterations + 32-byte hash) so the
///   user-entered password can be checked against the stored one without
///   ever decrypting a real backup.
/// - Lost password = unrecoverable backups. There is no recovery key.
/// </summary>
public static class BackupCrypto
{
    private const int SaltLen = 16;
    private const int NonceLen = 12;
    private const int TagLen = 16;
    private const int KeyLen = 32;          // AES-256
    private const int Pbkdf2Iters = 600_000;
    private static readonly byte[] Magic = "TCB1"u8.ToArray();

    /// <summary>Encrypt <paramref name="sourcePath"/> → <paramref name="destPath"/>. Caller deletes source if it wants.</summary>
    public static void EncryptFile(string sourcePath, string destPath, string password)
    {
        if (string.IsNullOrEmpty(password)) throw new ArgumentException("Password required.", nameof(password));

        var plaintext = File.ReadAllBytes(sourcePath);
        var salt = RandomNumberGenerator.GetBytes(SaltLen);
        var nonce = RandomNumberGenerator.GetBytes(NonceLen);
        var key = DeriveKey(password, salt);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagLen];

        using (var aes = new AesGcm(key, TagLen))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        using var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
        fs.Write(Magic);
        fs.Write(salt);
        fs.Write(nonce);
        fs.Write(ciphertext);
        fs.Write(tag);
        fs.Flush(flushToDisk: true);

        // Best-effort zero the derived key in memory.
        CryptographicOperations.ZeroMemory(key);
    }

    /// <summary>
    /// Decrypt <paramref name="sourcePath"/> → <paramref name="destPath"/>. Returns true on success.
    /// Wrong password produces a CryptographicException internally → returns false (no exception leaks).
    /// </summary>
    public static bool TryDecryptFile(string sourcePath, string destPath, string password)
    {
        if (string.IsNullOrEmpty(password)) return false;
        try
        {
            var raw = File.ReadAllBytes(sourcePath);
            if (raw.Length < Magic.Length + SaltLen + NonceLen + TagLen) return false;
            if (!raw.AsSpan(0, Magic.Length).SequenceEqual(Magic)) return false;

            var salt = raw[Magic.Length..(Magic.Length + SaltLen)];
            var nonce = raw[(Magic.Length + SaltLen)..(Magic.Length + SaltLen + NonceLen)];
            var ctLen = raw.Length - Magic.Length - SaltLen - NonceLen - TagLen;
            var ciphertext = raw[(Magic.Length + SaltLen + NonceLen)..(Magic.Length + SaltLen + NonceLen + ctLen)];
            var tag = raw[(raw.Length - TagLen)..];

            var key = DeriveKey(password, salt);
            var plaintext = new byte[ctLen];
            try
            {
                using var aes = new AesGcm(key, TagLen);
                aes.Decrypt(nonce, ciphertext, tag, plaintext);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
            File.WriteAllBytes(destPath, plaintext);
            return true;
        }
        catch
        {
            try { if (File.Exists(destPath)) File.Delete(destPath); } catch { }
            // Any failure → false. Don't leak whether it was a bad password
            // vs a malformed file; both are "decryption refused."
            return false;
        }
    }

    /// <summary>
    /// Build a PBKDF2 verification token to persist alongside SettingsStore.
    /// Format: "v1:<base64-salt>:<iters>:<base64-hash>". Verify with VerifyPasswordToken.
    /// </summary>
    public static string MakePasswordToken(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLen);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, Pbkdf2Iters, HashAlgorithmName.SHA256, KeyLen);
        return $"v1:{Convert.ToBase64String(salt)}:{Pbkdf2Iters}:{Convert.ToBase64String(hash)}";
    }

    /// <summary>Verify a password against a previously stored token. Constant-time compare.</summary>
    public static bool VerifyPasswordToken(string token, string password)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(password)) return false;
        try
        {
            var parts = token.Split(':');
            if (parts.Length != 4 || parts[0] != "v1") return false;
            var salt = Convert.FromBase64String(parts[1]);
            if (!int.TryParse(parts[2], out var iters)) return false;
            var stored = Convert.FromBase64String(parts[3]);
            var candidate = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password), salt, iters, HashAlgorithmName.SHA256, stored.Length);
            try
            {
                return CryptographicOperations.FixedTimeEquals(stored, candidate);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(candidate);
            }
        }
        catch
        {
            return false;
        }
    }

    private static byte[] DeriveKey(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, Pbkdf2Iters, HashAlgorithmName.SHA256, KeyLen);

    /// <summary>True when the file looks like a TaskCopy encrypted backup (magic header match).</summary>
    public static bool IsEncryptedBackup(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buf = new byte[Magic.Length];
            return fs.Read(buf, 0, buf.Length) == Magic.Length && buf.AsSpan().SequenceEqual(Magic);
        }
        catch
        {
            return false;
        }
    }
}
