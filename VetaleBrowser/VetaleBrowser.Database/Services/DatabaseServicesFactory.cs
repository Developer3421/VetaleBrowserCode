using System;
using System.IO;
using LiteDB;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Factory for creating database services with guaranteed initialization and RAM optimization
/// </summary>
public static class DatabaseServicesFactory
{
    private static readonly object _lock = new();
    
    private static ISettingsService? _settingsService;
    private static IAppearanceSettingsService? _appearanceSettingsService;
    private static IApiKeysService? _apiKeysService;
    private static bool _initialized;
    
    /// <summary>
    /// Initializes all databases and services
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        
        lock (_lock)
        {
            if (_initialized) return;
            
            try
            {
                System.Diagnostics.Debug.WriteLine("[DatabaseServicesFactory] Starting initialization...");
                
                var config = DatabaseConfiguration.CreateDefault();
                var dbDirectory = Path.GetDirectoryName(config.DatabasePath);
                
                System.Diagnostics.Debug.WriteLine($"[DatabaseServicesFactory] Database directory: {dbDirectory}");
                
                // Ensure directory creation
                EnsureDirectoryExists(dbDirectory);
                
                // Create database files if they don't exist
                EnsureDatabaseFileExists(config.GetSettingsDbPath());
                EnsureDatabaseFileExists(config.GetAppearanceSettingsDbPath());
                EnsureDatabaseFileExists(config.GetApiKeysDbPath());
                
                // Create services
                _settingsService = CreateSettingsServiceInternal(config);
                _appearanceSettingsService = CreateAppearanceSettingsServiceInternal(config);
                _apiKeysService = CreateApiKeysServiceInternal(config);
                
                _initialized = true;
                System.Diagnostics.Debug.WriteLine("[DatabaseServicesFactory] All databases initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseServicesFactory] ERROR during initialization: {ex}");
            }
        }
    }
    
    /// <summary>
    /// Ensures directory exists
    /// </summary>
    private static void EnsureDirectoryExists(string? directory)
    {
        if (string.IsNullOrEmpty(directory)) return;
        
        try
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                System.Diagnostics.Debug.WriteLine($"[DatabaseServicesFactory] Created directory: {directory}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DatabaseServicesFactory] ERROR creating directory: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Ensures database file exists, creating an empty LiteDB if needed
    /// </summary>
    private static void EnsureDatabaseFileExists(string databasePath)
    {
        try
        {
            if (File.Exists(databasePath))
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseServicesFactory] Database exists: {databasePath}");
                return;
            }
            
            // Ensure directory
            var directory = Path.GetDirectoryName(databasePath);
            EnsureDirectoryExists(directory);
            
            // Create empty database with optimized settings
            System.Diagnostics.Debug.WriteLine($"[DatabaseServicesFactory] Creating new database: {databasePath}");
            
            using var db = DatabaseConfiguration.CreateOptimizedDatabase(databasePath);
            // Create test collection to ensure file is actually created
            var testCollection = db.GetCollection<BsonDocument>("_init");
            testCollection.Insert(new BsonDocument { ["created"] = DateTime.UtcNow });
            testCollection.DeleteAll();
            db.Checkpoint();
            
            System.Diagnostics.Debug.WriteLine($"[DatabaseServicesFactory] Database created successfully: {databasePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DatabaseServicesFactory] ERROR creating database file: {ex}");
            throw;
        }
    }
    
    /// <summary>
    /// Creates SettingsService
    /// </summary>
    private static ISettingsService CreateSettingsServiceInternal(DatabaseConfiguration config)
    {
        var dbPath = config.GetSettingsDbPath();
        System.Diagnostics.Debug.WriteLine($"[DatabaseServicesFactory] Creating SettingsService: {dbPath}");
        return new SettingsService(dbPath, config.EncryptionKey);
    }
    
    /// <summary>
    /// Creates AppearanceSettingsService
    /// </summary>
    private static IAppearanceSettingsService CreateAppearanceSettingsServiceInternal(DatabaseConfiguration config)
    {
        var dbPath = config.GetAppearanceSettingsDbPath();
        System.Diagnostics.Debug.WriteLine($"[DatabaseServicesFactory] Creating AppearanceSettingsService: {dbPath}");
        return new AppearanceSettingsService(dbPath, config.EncryptionKey);
    }
    
    /// <summary>
    /// Creates ApiKeysService
    /// </summary>
    private static IApiKeysService CreateApiKeysServiceInternal(DatabaseConfiguration config)
    {
        var dbPath = config.GetApiKeysDbPath();
        System.Diagnostics.Debug.WriteLine($"[DatabaseServicesFactory] Creating ApiKeysService: {dbPath}");
        return new ApiKeysService(dbPath, config.EncryptionKey);
    }
    
    /// <summary>
    /// Gets or creates browser settings service
    /// </summary>
    public static ISettingsService GetSettingsService()
    {
        if (_settingsService != null) return _settingsService;
        
        lock (_lock)
        {
            if (_settingsService != null) return _settingsService;
            
            Initialize();
            
            if (_settingsService == null)
            {
                // Fallback - create directly
                var config = DatabaseConfiguration.CreateDefault();
                EnsureDatabaseFileExists(config.GetSettingsDbPath());
                _settingsService = CreateSettingsServiceInternal(config);
            }
        }
        
        return _settingsService;
    }
    
    /// <summary>
    /// Gets or creates appearance settings service
    /// </summary>
    public static IAppearanceSettingsService GetAppearanceSettingsService()
    {
        if (_appearanceSettingsService != null) return _appearanceSettingsService;
        
        lock (_lock)
        {
            if (_appearanceSettingsService != null) return _appearanceSettingsService;
            
            Initialize();
            
            if (_appearanceSettingsService == null)
            {
                // Fallback - create directly
                var config = DatabaseConfiguration.CreateDefault();
                EnsureDatabaseFileExists(config.GetAppearanceSettingsDbPath());
                _appearanceSettingsService = CreateAppearanceSettingsServiceInternal(config);
            }
        }
        
        return _appearanceSettingsService;
    }
    
    /// <summary>
    /// Try to get settings service (without throwing errors)
    /// </summary>
    public static ISettingsService? TryGetSettingsService()
    {
        try
        {
            return GetSettingsService();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DatabaseServicesFactory] TryGetSettingsService failed: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Try to get appearance service (without throwing errors)
    /// </summary>
    public static IAppearanceSettingsService? TryGetAppearanceSettingsService()
    {
        try
        {
            return GetAppearanceSettingsService();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DatabaseServicesFactory] TryGetAppearanceSettingsService failed: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Gets or creates API keys service
    /// </summary>
    public static IApiKeysService GetApiKeysService()
    {
        if (_apiKeysService != null) return _apiKeysService;
        
        lock (_lock)
        {
            if (_apiKeysService != null) return _apiKeysService;
            
            Initialize();
            
            if (_apiKeysService == null)
            {
                // Fallback - create directly
                var config = DatabaseConfiguration.CreateDefault();
                EnsureDatabaseFileExists(config.GetApiKeysDbPath());
                _apiKeysService = CreateApiKeysServiceInternal(config);
            }
        }
        
        return _apiKeysService;
    }
    
    /// <summary>
    /// Try to get API keys service (without throwing errors)
    /// </summary>
    public static IApiKeysService? TryGetApiKeysService()
    {
        try
        {
            return GetApiKeysService();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DatabaseServicesFactory] TryGetApiKeysService failed: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Resets the factory (for testing)
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            (_settingsService as IDisposable)?.Dispose();
            (_appearanceSettingsService as IDisposable)?.Dispose();
            
            _settingsService = null;
            _appearanceSettingsService = null;
            _initialized = false;
        }
    }
}

