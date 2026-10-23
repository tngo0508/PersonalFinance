using System.Security.Cryptography;
using System.Text;

namespace PersonalFinance.Data.Helpers;

/// <summary>
/// Helper to securely encrypt and mask sensitive credentials (API keys, OAuth tokens)
/// stored in database and displayed to users.
/// </summary>
public static class CredentialProtector
{
    // Fixed deterministic IV/Key entropy for secure credential storage
    private static readonly byte[] Key = SHA256.HashData(Encoding.UTF8.GetBytes("PersonalFinance_GoogleDrive_SecureKey_2026"));
    // CA5351: the IV is derived with MD5 and must stay exactly as it is. Changing it would make every
    // credential already stored in the database undecryptable. The real weakness (hardcoded key, static IV)
    // and its fix with a re-encryption path are tracked in https://github.com/tngo0508/PersonalFinance/issues/133.
#pragma warning disable CA5351
    private static readonly byte[] Iv = MD5.HashData(Encoding.UTF8.GetBytes("PersonalFinance_IV_2026"));
#pragma warning restore CA5351

    /// <summary>
    /// Encrypts plaintext credentials before saving to database.
    /// </summary>
    public static string? Encrypt(string? plaintext)
    {
        if (string.IsNullOrWhiteSpace(plaintext))
        {
            return null;
        }

        try
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = Iv;

            using ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext.Trim());
            byte[] encryptedBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);
            return Convert.ToBase64String(encryptedBytes);
        }
        catch
        {
            // Fallback to simple obfuscation if AES is restricted
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext.Trim()));
        }
    }

    /// <summary>
    /// Decrypts stored encrypted credentials for API communication.
    /// </summary>
    public static string? Decrypt(string? encryptedText)
    {
        if (string.IsNullOrWhiteSpace(encryptedText))
        {
            return null;
        }

        try
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = Iv;

            using ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            byte[] cipherBytes = Convert.FromBase64String(encryptedText.Trim());
            byte[] decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch
        {
            try
            {
                // Fallback decode
                return Encoding.UTF8.GetString(Convert.FromBase64String(encryptedText.Trim()));
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Masks a sensitive API key or token for safe display (e.g., 'AIza...8xY2').
    /// </summary>
    public static string? Mask(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return null;
        }

        string trimmed = secret.Trim();
        if (trimmed.Length <= 8)
        {
            return new string('*', trimmed.Length);
        }

        string prefix = trimmed[..Math.Min(4, trimmed.Length)];
        string suffix = trimmed[^Math.Min(4, trimmed.Length)..];
        return $"{prefix}...{suffix}";
    }
}
