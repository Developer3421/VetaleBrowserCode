using System;
using System.IO;

namespace VetaleBrowser.VetaleBrowser.Database;

/// <summary>
/// Конфігурація для бази даних
/// </summary>
public class DatabaseConfiguration
{
    private static readonly string DefaultDatabasePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VetaleBrowser",
        "Data"
    );

    /// <summary>
    /// Шлях до файлу бази даних
    /// </summary>
    public string DatabasePath { get; set; } = Path.Combine(DefaultDatabasePath, "browser.db");

    /// <summary>
    /// Ключ шифрування (має бути захищений)
    /// </summary>
    public string EncryptionKey { get; set; } = GenerateDefaultKey();

    /// <summary>
    /// Автоматичне очищення старих даних
    /// </summary>
    public bool AutoCleanup { get; set; } = true;

    /// <summary>
    /// Інтервал очищення в днях
    /// </summary>
    public int CleanupIntervalDays { get; set; } = 30;

    /// <summary>
    /// Генерує ключ шифрування за замовчуванням
    /// </summary>
    private static string GenerateDefaultKey()
    {
        // В продакшн середовищі ключ має зберігатися безпечно (наприклад, в Windows Data Protection API)
        var machineId = Environment.MachineName;
        var userId = Environment.UserName;
        return $"VetaleBrowser_{machineId}_{userId}_2025_SecureKey";
    }

    /// <summary>
    /// Створює конфігурацію за замовчуванням
    /// </summary>
    public static DatabaseConfiguration CreateDefault()
    {
        return new DatabaseConfiguration();
    }

    /// <summary>
    /// Створює конфігурацію з кастомним ключем
    /// </summary>
    public static DatabaseConfiguration CreateWithKey(string encryptionKey)
    {
        return new DatabaseConfiguration
        {
            EncryptionKey = encryptionKey
        };
    }
}

