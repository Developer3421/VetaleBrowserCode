using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Service for AES encryption of database data
/// </summary>
public class DatabaseEncryptionService
{
    private readonly byte[] _encryptionKey;
    private readonly byte[] _iv;

    public DatabaseEncryptionService(string encryptionKey)
    {
        // Generate key based on the provided string
        using var sha256 = SHA256.Create();
        _encryptionKey = sha256.ComputeHash(Encoding.UTF8.GetBytes(encryptionKey));
        
        // Generate IV (Initialization Vector) based on the key
        _iv = new byte[16];
        Array.Copy(_encryptionKey, _iv, 16);
    }

    /// <summary>
    /// Encrypts data using AES
    /// </summary>
    public byte[] Encrypt(byte[] plainData)
    {
        if (plainData == null || plainData.Length == 0)
            return Array.Empty<byte>();

        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.IV = _iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        using var msEncrypt = new MemoryStream();
        using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
        
        csEncrypt.Write(plainData, 0, plainData.Length);
        csEncrypt.FlushFinalBlock();
        
        return msEncrypt.ToArray();
    }

    /// <summary>
    /// Decrypts data using AES
    /// </summary>
    public byte[] Decrypt(byte[] encryptedData)
    {
        if (encryptedData == null || encryptedData.Length == 0)
            return Array.Empty<byte>();

        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.IV = _iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        using var msDecrypt = new MemoryStream(encryptedData);
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var msPlain = new MemoryStream();
        
        csDecrypt.CopyTo(msPlain);
        return msPlain.ToArray();
    }

    /// <summary>
    /// Encrypts a string
    /// </summary>
    public string EncryptString(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = Encrypt(plainBytes);
        return Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// Decrypts a string
    /// </summary>
    public string DecryptString(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
            return string.Empty;

        var encryptedBytes = Convert.FromBase64String(encryptedText);
        var plainBytes = Decrypt(encryptedBytes);
        return Encoding.UTF8.GetString(plainBytes);
    }
}

