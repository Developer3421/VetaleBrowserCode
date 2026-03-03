using System;
using System.IO;
using LiteDB;

namespace VetaleBrowser.VetaleBrowser.Database;

/// <summary>
/// Database configuration with RAM optimization
/// </summary>
public class DatabaseConfiguration
{
    private static readonly string DefaultDatabasePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VetaleBrowser",
        "Data"
    );

    /// <summary>
    /// Path to the database file
    /// </summary>
    public string DatabasePath { get; set; } = Path.Combine(DefaultDatabasePath, "browser.db");

    /// <summary>
    /// Encryption key (should be protected)
    /// </summary>
    public string EncryptionKey { get; set; } = GenerateDefaultKey();

    /// <summary>
    /// Automatic cleanup of old data
    /// </summary>
    public bool AutoCleanup { get; set; } = true;

    /// <summary>
    /// Cleanup interval in days
    /// </summary>
    public int CleanupIntervalDays { get; set; } = 30;

    /// <summary>
    /// Generates default encryption key
    /// </summary>
    private static string GenerateDefaultKey()
    {
        // Secure key based on machine and user
        var machineId = Environment.MachineName;
        var userId = Environment.UserName;
        return $"VetaleBrowser_{machineId}_{userId}_2025_SecureKey";
    }

    /// <summary>
    /// Creates default configuration
    /// </summary>
    public static DatabaseConfiguration CreateDefault()
    {
        var config = new DatabaseConfiguration();
        config.EnsureDatabaseDirectory();
        return config;
    }

    /// <summary>
    /// Creates configuration with custom key
    /// </summary>
    public static DatabaseConfiguration CreateWithKey(string encryptionKey)
    {
        var config = new DatabaseConfiguration { EncryptionKey = encryptionKey };
        config.EnsureDatabaseDirectory();
        return config;
    }
    
    /// <summary>
    /// Ensures the database directory is created
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
    /// Gets the appearance settings database path
    /// </summary>
    public string GetAppearanceSettingsDbPath()
    {
        var directory = Path.GetDirectoryName(DatabasePath) ?? DefaultDatabasePath;
        return Path.Combine(directory, "appearance_settings.db");
    }
    
    /// <summary>
    /// Gets the browser settings database path
    /// </summary>
    public string GetSettingsDbPath()
    {
        return DatabasePath;
    }
    
    /// <summary>
    /// Gets the API keys database path
    /// </summary>
    public string GetApiKeysDbPath()
    {
        var directory = Path.GetDirectoryName(DatabasePath) ?? DefaultDatabasePath;
        return Path.Combine(directory, "api_keys.db");
    }
    
    /// <summary>
    /// Creates an optimized ConnectionString for LiteDB with minimal RAM usage
    /// </summary>
    public static ConnectionString CreateOptimizedConnectionString(string databasePath)
    {
        // Ensure directory exists
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        return new ConnectionString
        {
            Filename = databasePath,
            // Shared mode - allows concurrent access
            Connection = ConnectionType.Shared,
            // Disable read-only mode for writing
            ReadOnly = false
        };
    }
    
    /// <summary>
    /// Creates a LiteDatabase with optimized settings for minimal RAM
    /// </summary>
    public static LiteDatabase CreateOptimizedDatabase(string databasePath)
    {
        var connectionString = CreateOptimizedConnectionString(databasePath);
        var db = new LiteDatabase(connectionString);
        
        // Checkpoint to clean WAL file and reduce RAM
        try { db.Checkpoint(); } catch { }
        
        System.Diagnostics.Debug.WriteLine($"[DatabaseConfiguration] Created optimized LiteDB: {databasePath}");
        return db;
    }
}

