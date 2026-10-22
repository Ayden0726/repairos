using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WorkshopOS.Infrastructure.Security;

/// <summary>
/// AES-256-GCM authenticated encryption for integration secrets.
/// Master key from Encryption:Key (preferred) — never regenerate on startup.
/// Ciphertext format: v{version}:{ivB64}:{tagB64}:{cipherB64}
/// </summary>
public sealed class SecretProtector
{
    public const int CurrentKeyVersion = 1;
    private readonly byte[] _key;
    private readonly int _keyVersion;
    private readonly ILogger<SecretProtector>? _log;

    public SecretProtector(IConfiguration config, ILogger<SecretProtector>? log = null)
    {
        _log = log;
        var dedicated = config["Encryption:Key"];
        var jwt = config["Jwt:SigningKey"];
        string raw;
        if (!string.IsNullOrWhiteSpace(dedicated))
        {
            raw = dedicated.Trim();
        }
        else if (!string.IsNullOrWhiteSpace(jwt))
        {
            _log?.LogWarning(
                "Encryption:Key is not set; deriving protector key from Jwt:SigningKey. " +
                "Set a dedicated Encryption:Key so rotating JWT does not break stored credentials.");
            raw = jwt.Trim();
        }
        else
        {
            throw new InvalidOperationException(
                "Encryption:Key is required (32+ characters). Do not rely on an auto-generated key.");
        }

        if (raw.Length < 32)
            throw new InvalidOperationException("Encryption:Key must be at least 32 characters.");

        _key = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        _keyVersion = config.GetValue("Encryption:KeyVersion", CurrentKeyVersion);
    }

    public int KeyVersion => _keyVersion;

    public string Encrypt(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);
        var iv = RandomNumberGenerator.GetBytes(12);
        using var aes = new AesGcm(_key, 16);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        aes.Encrypt(iv, plain, cipher, tag);
        return $"v{_keyVersion}:{Convert.ToBase64String(iv)}:{Convert.ToBase64String(tag)}:{Convert.ToBase64String(cipher)}";
    }

    public string Decrypt(string ciphertext)
    {
        if (!TryDecrypt(ciphertext, out var plain, out var error))
            throw new CryptographicException(error ?? "Decryption failed.");
        return plain!;
    }

    /// <summary>
    /// Attempts decryption. On failure returns false and never mutates stored data.
    /// Callers must not delete or overwrite ciphertext when this fails.
    /// </summary>
    public bool TryDecrypt(string? ciphertext, out string? plaintext, out string? error)
    {
        plaintext = null;
        error = null;
        if (string.IsNullOrWhiteSpace(ciphertext))
        {
            error = "Ciphertext is empty.";
            return false;
        }

        var parts = ciphertext.Split(':', 4);
        if (parts.Length != 4 || !parts[0].StartsWith('v') || !int.TryParse(parts[0].AsSpan(1), out var version))
        {
            error = "Ciphertext format is invalid (expected vN:iv:tag:cipher).";
            return false;
        }

        if (version != _keyVersion && version != CurrentKeyVersion)
        {
            // Same physical key material for now; version tag documents planned rotation.
            _log?.LogWarning("Decrypting credential encrypted with key version {Version}; current is {Current}.", version, _keyVersion);
        }

        try
        {
            var iv = Convert.FromBase64String(parts[1]);
            var tag = Convert.FromBase64String(parts[2]);
            var cipher = Convert.FromBase64String(parts[3]);
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(_key, 16);
            aes.Decrypt(iv, cipher, tag, plain);
            plaintext = Encoding.UTF8.GetString(plain);
            return true;
        }
        catch (Exception ex)
        {
            error =
                "Unable to decrypt credential. The Encryption:Key may have changed, or the ciphertext is corrupt. " +
                "Restore the original Encryption:Key or re-enter the secret. Existing ciphertext was not modified. " +
                $"({ex.GetType().Name})";
            _log?.LogError("Credential decryption failed (details omitted for security).");
            return false;
        }
    }

    public static string MaskSecret(string? secret, int visibleTail = 4)
    {
        if (string.IsNullOrEmpty(secret)) return string.Empty;
        if (secret.Length <= visibleTail) return new string('•', Math.Min(8, secret.Length));
        return new string('•', 8) + secret[^visibleTail..];
    }
}
