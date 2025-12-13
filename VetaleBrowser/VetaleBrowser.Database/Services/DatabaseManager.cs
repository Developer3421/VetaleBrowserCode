using System;
using System.IO;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Централізований менеджер сервісів баз даних для оптимізації використання пам'яті.
/// MEMORY OPTIMIZATION: Singleton pattern with lazy initialization and proper disposal.
/// </summary>
public sealed class DatabaseServiceManager : IDisposable
{
    private static DatabaseServiceManager? _instance;
    private static readonly object _lock = new object();
    
    private readonly DatabaseConfiguration _configuration;
    private readonly string _basePath;
    
    // Lazy-initialized services
    private HistoryDatabaseService? _historyService;
    private TabDatabaseService? _tabService;
    private SettingsService? _settingsService;
    private AppearanceSettingsService? _appearanceService;
    private DownloadDatabaseService? _downloadService;
    private SearchIndexService? _searchIndexService;
    private ConsoleDatabaseService? _consoleService;
    
    private bool _disposed;
    
    // Lock objects for thread-safe lazy init
    private readonly object _historyLock = new object();
    private readonly object _tabLock = new object();
    private readonly object _settingsLock = new object();
    private readonly object _appearanceLock = new object();
    private readonly object _downloadLock = new object();
    private readonly object _searchLock = new object();
    private readonly object _consoleLock = new object();

    private DatabaseServiceManager()
    {
        _configuration = DatabaseConfiguration.CreateDefault();
        _basePath = Path.GetDirectoryName(_configuration.DatabasePath) ?? 
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VetaleBrowser", "Data");
        
        // Ensure base directory exists
        try
        {
            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseServiceManager] Failed to create base directory: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the singleton instance of DatabaseServiceManager
    /// </summary>
    public static DatabaseServiceManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new DatabaseServiceManager();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Gets or creates the History Database Service (lazy initialized)
    /// </summary>
    public IHistoryDatabaseService GetHistoryService()
    {
        if (_historyService != null) return _historyService;
        
        lock (_historyLock)
        {
            if (_historyService != null) return _historyService;
            
            var historyPath = Path.Combine(_basePath, "history.db");
            _historyService = new HistoryDatabaseService(historyPath, _configuration.EncryptionKey);
            Console.WriteLine("[DatabaseServiceManager] History service initialized (lazy)");
            return _historyService;
        }
    }

    /// <summary>
    /// Gets or creates the Tab Database Service (lazy initialized)
    /// </summary>
    public ITabDatabaseService GetTabService()
    {
        if (_tabService != null) return _tabService;
        
        lock (_tabLock)
        {
            if (_tabService != null) return _tabService;
            
            var tabPath = Path.Combine(_basePath, "tabs.db");
            _tabService = new TabDatabaseService(tabPath, _configuration.EncryptionKey);
            Console.WriteLine("[DatabaseServiceManager] Tab service initialized (lazy)");
            return _tabService;
        }
    }

    /// <summary>
    /// Gets or creates the Settings Service (lazy initialized)
    /// </summary>
    public ISettingsService GetSettingsService()
    {
        if (_settingsService != null) return _settingsService;
        
        lock (_settingsLock)
        {
            if (_settingsService != null) return _settingsService;
            
            var settingsPath = Path.Combine(_basePath, "settings.db");
            System.Diagnostics.Debug.WriteLine($"[DatabaseServiceManager] Creating SettingsService at: {settingsPath}");
            System.Diagnostics.Debug.WriteLine($"[DatabaseServiceManager] BasePath: {_basePath}");
            _settingsService = new SettingsService(settingsPath, _configuration.EncryptionKey);
            Console.WriteLine("[DatabaseServiceManager] Settings service initialized (lazy)");
            return _settingsService;
        }
    }

    /// <summary>
    /// Gets or creates the Appearance Settings Service (lazy initialized)
    /// </summary>
    public IAppearanceSettingsService GetAppearanceService()
    {
        if (_appearanceService != null) return _appearanceService;
        
        lock (_appearanceLock)
        {
            if (_appearanceService != null) return _appearanceService;
            
            var appearancePath = Path.Combine(_basePath, "appearance_settings.db");
            _appearanceService = new AppearanceSettingsService(appearancePath, _configuration.EncryptionKey);
            Console.WriteLine("[DatabaseServiceManager] Appearance service initialized (lazy)");
            return _appearanceService;
        }
    }

    /// <summary>
    /// Gets or creates the Download Database Service (lazy initialized)
    /// </summary>
    public DownloadDatabaseService GetDownloadService()
    {
        if (_downloadService != null) return _downloadService;
        
        lock (_downloadLock)
        {
            if (_downloadService != null) return _downloadService;
            
            var downloadPath = Path.Combine(_basePath, "downloads.db");
            _downloadService = new DownloadDatabaseService(downloadPath, _configuration.EncryptionKey);
            
            // Set global accessor for compatibility
            ServicesAccessor.DownloadDatabase = _downloadService;
            
            Console.WriteLine("[DatabaseServiceManager] Download service initialized (lazy)");
            return _downloadService;
        }
    }

    /// <summary>
    /// Gets or creates the Search Index Service (lazy initialized)
    /// </summary>
    public ISearchIndexService GetSearchIndexService()
    {
        if (_searchIndexService != null) return _searchIndexService;
        
        lock (_searchLock)
        {
            if (_searchIndexService != null) return _searchIndexService;
            
            var searchPath = Path.Combine(_basePath, "search_index.db");
            _searchIndexService = new SearchIndexService(searchPath);
            Console.WriteLine("[DatabaseServiceManager] Search index service initialized (lazy)");
            return _searchIndexService;
        }
    }

    /// <summary>
    /// Gets or creates the Console Database Service (lazy initialized)
    /// </summary>
    public ConsoleDatabaseService GetConsoleService()
    {
        if (_consoleService != null) return _consoleService;
        
        lock (_consoleLock)
        {
            if (_consoleService != null) return _consoleService;
            
            var consolePath = Path.Combine(_basePath, "console_logs.db");
            _consoleService = new ConsoleDatabaseService(consolePath, _configuration.EncryptionKey);
            Console.WriteLine("[DatabaseServiceManager] Console service initialized (lazy)");
            return _consoleService;
        }
    }

    /// <summary>
    /// Gets the database configuration
    /// </summary>
    public DatabaseConfiguration Configuration => _configuration;

    /// <summary>
    /// Gets the base path for all databases
    /// </summary>
    public string BasePath => _basePath;

    /// <summary>
    /// MEMORY OPTIMIZATION: Perform garbage collection on all databases
    /// </summary>
    public void OptimizeMemory()
    {
        try
        {
            Console.WriteLine("[DatabaseServiceManager] Starting memory optimization...");
            
            // Force GC collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            Console.WriteLine("[DatabaseServiceManager] Memory optimization complete");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseServiceManager] Memory optimization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// MEMORY OPTIMIZATION: Clear unused database connections
    /// Called periodically or when memory pressure is detected
    /// </summary>
    public void ReleasePressure()
    {
        try
        {
            // For now, just trigger GC - in future could close idle connections
            OptimizeMemory();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseServiceManager] Release pressure failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        Console.WriteLine("[DatabaseServiceManager] Disposing all services...");
        
        try { _historyService?.Dispose(); } catch { }
        try { (_tabService as IDisposable)?.Dispose(); } catch { }
        try { (_settingsService as IDisposable)?.Dispose(); } catch { }
        try { (_appearanceService as IDisposable)?.Dispose(); } catch { }
        try { _downloadService?.Dispose(); } catch { }
        try { (_searchIndexService as IDisposable)?.Dispose(); } catch { }
        try { _consoleService?.Dispose(); } catch { }
        
        _historyService = null;
        _tabService = null;
        _settingsService = null;
        _appearanceService = null;
        _downloadService = null;
        _searchIndexService = null;
        _consoleService = null;
        
        Console.WriteLine("[DatabaseServiceManager] All services disposed");
    }

    /// <summary>
    /// Static method to shutdown the singleton
    /// </summary>
    public static void Shutdown()
    {
        lock (_lock)
        {
            _instance?.Dispose();
            _instance = null;
        }
    }
}

