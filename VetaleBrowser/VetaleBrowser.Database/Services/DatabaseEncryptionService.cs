using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Сервіс для AES шифрування даних бази
/// </summary>
public class DatabaseEncryptionService
{
    private readonly byte[] _encryptionKey;
    private readonly byte[] _iv;

    public DatabaseEncryptionService(string encryptionKey)
    {
        // Генеруємо ключ на основі переданого рядка
        using var sha256 = SHA256.Create();
        _encryptionKey = sha256.ComputeHash(Encoding.UTF8.GetBytes(encryptionKey));
        
        // Генеруємо IV (Initialization Vector) на основі ключа
        _iv = new byte[16];
        Array.Copy(_encryptionKey, _iv, 16);
    }

    /// <summary>
    /// Шифрує дані за допомогою AES
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
    /// Розшифровує дані за допомогою AES
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
    /// Шифрує текст
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
    /// Розшифровує текст
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

