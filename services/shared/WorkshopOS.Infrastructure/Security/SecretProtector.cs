using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WorkshopOS.Infrastructure.Security;

/// <summary>
/// AES-256-GCM authenticated encryption for integration secrets.
/// Master key from Encryption:Key (preferred) — never regenerate on startup.
/// Ciphertext format: v{version}:{ivB64}:{tagB64}:{cipherB64}
/// <para>
/// Decrypt tries the primary Encryption:Key first, then optional
/// Encryption:PreviousKey (planned rotation), then Jwt:SigningKey (legacy installs
/// that encrypted before a dedicated Encryption:Key existed). Successful legacy
/// decrypts can be re-wrapped with <see cref="RewrapIfNeeded"/>.
/// </para>
/// </summary>
public sealed class SecretProtector
{
    public const int CurrentKeyVersion = 1;
    private readonly byte[] _key;
    private readonly List<(string Label, byte[] Material)> _fallbacks = new();
    private readonly int _keyVersion;
    private readonly ILogger<SecretProtector>? _log;

    public SecretProtector(IConfiguration config, ILogger<SecretProtector>? log = null)
    {
        _log = log;
        var dedicated = config["Encryption:Key"];
        var previous = config["Encryption:PreviousKey"];
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

        // Previous dedicated key (planned rotation) — try after primary fails.
        if (!string.IsNullOrWhiteSpace(previous) &&
            previous.Trim().Length >= 32 &&
            !string.Equals(previous.Trim(), raw, StringComparison.Ordinal))
        {
            _fallbacks.Add(("Encryption:PreviousKey", SHA256.HashData(Encoding.UTF8.GetBytes(previous.Trim()))));
        }

        // Legacy: secrets encrypted when Encryption:Key was missing and Jwt was used.
        // Only useful when a dedicated Encryption:Key is now set and differs from JWT.
        if (!string.IsNullOrWhiteSpace(dedicated) &&
            !string.IsNullOrWhiteSpace(jwt) &&
            jwt.Trim().Length >= 32 &&
            !string.Equals(jwt.Trim(), raw, StringComparison.Ordinal) &&
            (string.IsNullOrWhiteSpace(previous) ||
             !string.Equals(jwt.Trim(), previous.Trim(), StringComparison.Ordinal)))
        {
            _fallbacks.Add(("Jwt:SigningKey (legacy)", SHA256.HashData(Encoding.UTF8.GetBytes(jwt.Trim()))));
        }
    }

    public int KeyVersion => _keyVersion;

    public string Encrypt(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);
        return EncryptWith(_key, plaintext);
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
    public bool TryDecrypt(string? ciphertext, out string? plaintext, out string? error) =>
        TryDecrypt(ciphertext, out plaintext, out error, out _);

    /// <summary>
    /// Attempts decryption with the primary key, then configured fallbacks.
    /// <paramref name="usedLegacyKey"/> is true when a fallback (previous or JWT) succeeded.
    /// </summary>
    public bool TryDecrypt(string? ciphertext, out string? plaintext, out string? error, out bool usedLegacyKey)
    {
        plaintext = null;
        error = null;
        usedLegacyKey = false;
        if (string.IsNullOrWhiteSpace(ciphertext))
        {
            error = "Ciphertext is empty.";
            return false;
        }

        if (!TryParseCipher(ciphertext, out var version, out var iv, out var tag, out var cipher, out error))
            return false;

        if (version != _keyVersion && version != CurrentKeyVersion)
        {
            _log?.LogWarning("Decrypting credential encrypted with key version {Version}; current is {Current}.", version, _keyVersion);
        }

        if (TryDecryptWith(_key, iv, tag, cipher, out plaintext))
            return true;

        foreach (var (label, material) in _fallbacks)
        {
            if (!TryDecryptWith(material, iv, tag, cipher, out plaintext))
                continue;

            usedLegacyKey = true;
            _log?.LogWarning(
                "Credential decrypted with fallback key ({Label}). Re-save or rewrap so it uses Encryption:Key.",
                label);
            return true;
        }

        error =
            "Unable to decrypt credential. The Encryption:Key may have changed, or the ciphertext is corrupt. " +
            "Restore the original Encryption:Key (or Encryption:PreviousKey), or re-enter the secret. " +
            "Existing ciphertext was not modified.";
        _log?.LogError("Credential decryption failed (details omitted for security).");
        return false;
    }

    /// <summary>
    /// If <paramref name="ciphertext"/> decrypts only via a legacy/previous key,
    /// returns a new ciphertext under the primary Encryption:Key. If already under
    /// the primary key, returns the original string. Returns null when decrypt fails.
    /// </summary>
    public string? RewrapIfNeeded(string? ciphertext)
    {
        if (!TryDecrypt(ciphertext, out var plain, out _, out var usedLegacy) || plain is null)
            return null;
        return usedLegacy ? Encrypt(plain) : ciphertext;
    }

    public static string MaskSecret(string? secret, int visibleTail = 4)
    {
        if (string.IsNullOrEmpty(secret)) return string.Empty;
        if (secret.Length <= visibleTail) return new string('•', Math.Min(8, secret.Length));
        return new string('•', 8) + secret[^visibleTail..];
    }

    private static bool TryParseCipher(
        string ciphertext,
        out int version,
        out byte[] iv,
        out byte[] tag,
        out byte[] cipher,
        out string? error)
    {
        version = 0;
        iv = tag = cipher = Array.Empty<byte>();
        error = null;
        var parts = ciphertext.Split(':', 4);
        if (parts.Length != 4 || !parts[0].StartsWith('v') || !int.TryParse(parts[0].AsSpan(1), out version))
        {
            error = "Ciphertext format is invalid (expected vN:iv:tag:cipher).";
            return false;
        }

        try
        {
            iv = Convert.FromBase64String(parts[1]);
            tag = Convert.FromBase64String(parts[2]);
            cipher = Convert.FromBase64String(parts[3]);
            return true;
        }
        catch (Exception)
        {
            error = "Ciphertext format is invalid (base64 decode failed).";
            return false;
        }
    }

    private static bool TryDecryptWith(byte[] key, byte[] iv, byte[] tag, byte[] cipher, out string? plaintext)
    {
        plaintext = null;
        try
        {
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(iv, cipher, tag, plain);
            plaintext = Encoding.UTF8.GetString(plain);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private string EncryptWith(byte[] key, string plaintext)
    {
        var iv = RandomNumberGenerator.GetBytes(12);
        using var aes = new AesGcm(key, 16);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        aes.Encrypt(iv, plain, cipher, tag);
        return $"v{_keyVersion}:{Convert.ToBase64String(iv)}:{Convert.ToBase64String(tag)}:{Convert.ToBase64String(cipher)}";
    }
}
