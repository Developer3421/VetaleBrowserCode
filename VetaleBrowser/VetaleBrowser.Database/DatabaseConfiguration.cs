using System;
using System.IO;
using LiteDB;

namespace VetaleBrowser.VetaleBrowser.Database;

/// <summary>
/// Конфігурація для бази даних з оптимізацією RAM
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
        // Безпечний ключ на основі машини та користувача
        var machineId = Environment.MachineName;
        var userId = Environment.UserName;
        return $"VetaleBrowser_{machineId}_{userId}_2025_SecureKey";
    }

    /// <summary>
    /// Створює конфігурацію за замовчуванням
    /// </summary>
    public static DatabaseConfiguration CreateDefault()
    {
        var config = new DatabaseConfiguration();
        config.EnsureDatabaseDirectory();
        return config;
    }

    /// <summary>
    /// Створює конфігурацію з кастомним ключем
    /// </summary>
    public static DatabaseConfiguration CreateWithKey(string encryptionKey)
    {
        var config = new DatabaseConfiguration { EncryptionKey = encryptionKey };
        config.EnsureDatabaseDirectory();
        return config;
    }
    
    /// <summary>
    /// Гарантує створення директорії для баз даних
    /// </summary>
    public void EnsureDatabaseDirectory()
    {
        try
        {
            var directory = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                System.Diagnostics.Debug.WriteLine($"[DatabaseConfiguration] Created directory: {directory}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DatabaseConfiguration] ERROR creating directory: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Отримує шлях до бази даних налаштувань вигляду
    /// </summary>
    public string GetAppearanceSettingsDbPath()
    {
        var directory = Path.GetDirectoryName(DatabasePath) ?? DefaultDatabasePath;
        return Path.Combine(directory, "appearance_settings.db");
    }
    
    /// <summary>
    /// Отримує шлях до бази даних налаштувань браузера
    /// </summary>
    public string GetSettingsDbPath()
    {
        return DatabasePath;
    }
    
    /// <summary>
    /// Створює оптимізований ConnectionString для LiteDB з мінімальним споживанням RAM
    /// </summary>
    public static ConnectionString CreateOptimizedConnectionString(string databasePath)
    {
        // Гарантуємо існування директорії
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        return new ConnectionString
        {
            Filename = databasePath,
            // Direct mode - мінімальне споживання RAM, без кешування в пам'яті
            Connection = ConnectionType.Direct,
            // Вимикаємо read-only режим для запису
            ReadOnly = false
        };
    }
    
    /// <summary>
    /// Створює LiteDatabase з оптимізованими налаштуваннями для мінімального RAM
    /// </summary>
    public static LiteDatabase CreateOptimizedDatabase(string databasePath)
    {
        var connectionString = CreateOptimizedConnectionString(databasePath);
        var db = new LiteDatabase(connectionString);
        
        // Checkpoint для очищення WAL файлу та зменшення RAM
        try { db.Checkpoint(); } catch { }
        
        System.Diagnostics.Debug.WriteLine($"[DatabaseConfiguration] Created optimized LiteDB: {databasePath}");
        return db;
    }
}

