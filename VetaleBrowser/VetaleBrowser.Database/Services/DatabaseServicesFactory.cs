using System;
using System.IO;
using LiteDB;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Фабрика для створення сервісів баз даних з гарантією ініціалізації та оптимізацією RAM
/// </summary>
public static class DatabaseServicesFactory
{
    private static readonly object _lock = new();
    
    private static ISettingsService? _settingsService;
    private static IAppearanceSettingsService? _appearanceSettingsService;
    private static bool _initialized;
    
    /// <summary>
    /// Ініціалізує всі бази даних та сервіси
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
                
                // Гарантуємо створення директорії
                EnsureDirectoryExists(dbDirectory);
                
                // Створюємо файли баз даних якщо не існують
                EnsureDatabaseFileExists(config.GetSettingsDbPath());
                EnsureDatabaseFileExists(config.GetAppearanceSettingsDbPath());
                
                // Створюємо сервіси
                _settingsService = CreateSettingsServiceInternal(config);
                _appearanceSettingsService = CreateAppearanceSettingsServiceInternal(config);
                
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
    /// Гарантує існування директорії
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
    /// Гарантує існування файлу бази даних, створюючи порожню LiteDB якщо потрібно
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
            
            // Гарантуємо директорію
            var directory = Path.GetDirectoryName(databasePath);
            EnsureDirectoryExists(directory);
            
            // Створюємо порожню базу даних з оптимізованими налаштуваннями
            System.Diagnostics.Debug.WriteLine($"[DatabaseServicesFactory] Creating new database: {databasePath}");
            
            using var db = DatabaseConfiguration.CreateOptimizedDatabase(databasePath);
            // Створюємо тестову колекцію щоб файл точно створився
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
    /// Створює SettingsService
    /// </summary>
    private static ISettingsService CreateSettingsServiceInternal(DatabaseConfiguration config)
    {
        var dbPath = config.GetSettingsDbPath();
        System.Diagnostics.Debug.WriteLine($"[DatabaseServicesFactory] Creating SettingsService: {dbPath}");
        return new SettingsService(dbPath, config.EncryptionKey);
    }
    
    /// <summary>
    /// Створює AppearanceSettingsService
    /// </summary>
    private static IAppearanceSettingsService CreateAppearanceSettingsServiceInternal(DatabaseConfiguration config)
    {
        var dbPath = config.GetAppearanceSettingsDbPath();
        System.Diagnostics.Debug.WriteLine($"[DatabaseServicesFactory] Creating AppearanceSettingsService: {dbPath}");
        return new AppearanceSettingsService(dbPath, config.EncryptionKey);
    }
    
    /// <summary>
    /// Отримує або створює сервіс налаштувань браузера
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
                // Fallback - створюємо напряму
                var config = DatabaseConfiguration.CreateDefault();
                EnsureDatabaseFileExists(config.GetSettingsDbPath());
                _settingsService = CreateSettingsServiceInternal(config);
            }
        }
        
        return _settingsService;
    }
    
    /// <summary>
    /// Отримує або створює сервіс налаштувань вигляду
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
                // Fallback - створюємо напряму
                var config = DatabaseConfiguration.CreateDefault();
                EnsureDatabaseFileExists(config.GetAppearanceSettingsDbPath());
                _appearanceSettingsService = CreateAppearanceSettingsServiceInternal(config);
            }
        }
        
        return _appearanceSettingsService;
    }
    
    /// <summary>
    /// Спробувати отримати сервіс налаштувань (без викидання помилок)
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
    /// Спробувати отримати сервіс вигляду (без викидання помилок)
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
    /// Скидає фабрику (для тестування)
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

