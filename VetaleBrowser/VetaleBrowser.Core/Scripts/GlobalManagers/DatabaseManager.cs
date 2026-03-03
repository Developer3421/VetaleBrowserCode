using System;
using System.IO;
using VetaleBrowser.VetaleBrowser.Database.Services;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.GlobalManagers;

/// <summary>
/// Global database manager
/// </summary>
public static class DatabaseManager
{
    private static TabDatabaseService? _instance;
    private static HistoryDatabaseService? _historyInstance;
    private static ConsoleDatabaseService? _consoleInstance;
    private static readonly object _lock = new object();
    private static readonly object _historyLock = new object();
    private static readonly object _consoleLock = new object();

    /// <summary>
    /// Gets the database service instance
    /// </summary>
    public static ITabDatabaseService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    // Double-check locking
                    if (_instance == null)
                    {
                        InitializeInternal();
                    }
                }
            }
            return _instance!;
        }
    }
    
    /// <summary>
    /// Internal initialization (without re-calling if instance already exists)
    /// </summary>
    private static void InitializeInternal()
    {
        if (_instance != null) return;
        
        // Determine database path
        var dbPath = GetDefaultDatabasePath();
        
        // Determine encryption key
        var encryptionKey = GenerateEncryptionKey();

        // Create new instance
        _instance = new TabDatabaseService(dbPath, encryptionKey);

        // Create initial session if there is no current one
        var currentSession = _instance.GetCurrentSession();
        if (currentSession == null)
        {
            _instance.CreateSession();
        }
    }

    /// <summary>
    /// Gets the history database service instance
    /// </summary>
    public static IHistoryDatabaseService HistoryInstance
    {
        get
        {
            if (_historyInstance == null)
            {
                lock (_historyLock)
                {
                    if (_historyInstance == null)
                    {
                        InitializeHistory();
                    }
                }
            }
            return _historyInstance!;
        }
    }

    /// <summary>
    /// Gets the console database service instance
    /// </summary>
    public static ConsoleDatabaseService ConsoleInstance
    {
        get
        {
            if (_consoleInstance == null)
            {
                lock (_consoleLock)
                {
                    if (_consoleInstance == null)
                    {
                        InitializeConsole();
                    }
                }
            }
            return _consoleInstance!;
        }
    }

    /// <summary>
    /// Initializes the database
    /// </summary>
    public static void Initialize(string? customPath = null, string? customKey = null)
    {
        lock (_lock)
        {
            // If already initialized and no custom parameters - don't re-initialize
            if (_instance != null && customPath == null && customKey == null)
            {
                return;
            }
            
            // Close previous instance only if custom parameters are provided
            if (customPath != null || customKey != null)
            {
                _instance?.Dispose();
                _instance = null;
            }
            
            if (_instance != null) return;

            // Determine database path
            var dbPath = customPath ?? GetDefaultDatabasePath();
            
            // Determine encryption key
            var encryptionKey = customKey ?? GenerateEncryptionKey();

            // Create new instance
            _instance = new TabDatabaseService(dbPath, encryptionKey);

            // Create initial session if there is no current one
            var currentSession = _instance.GetCurrentSession();
            if (currentSession == null)
            {
                _instance.CreateSession();
            }
        }
    }

    /// <summary>
    /// Initializes the history database
    /// </summary>
    public static void InitializeHistory(string? customPath = null, string? customKey = null)
    {
        lock (_historyLock)
        {
            // Close previous instance if exists
            _historyInstance?.Dispose();

            // Determine history database path
            var dbPath = customPath ?? GetDefaultHistoryDatabasePath();
            
            // Determine encryption key
            var encryptionKey = customKey ?? GenerateEncryptionKey();

            // Create new instance
            _historyInstance = new HistoryDatabaseService(dbPath, encryptionKey);
        }
    }

    /// <summary>
    /// Initializes the console database
    /// </summary>
    public static void InitializeConsole(string? customPath = null, string? customKey = null)
    {
        lock (_consoleLock)
        {
            // Close previous instance if exists
            _consoleInstance?.Dispose();

            // Determine console database path
            var dbPath = customPath ?? GetDefaultConsoleDatabasePath();
            
            // Determine encryption key
            var encryptionKey = customKey ?? GenerateEncryptionKey();

            // Create new instance
            _consoleInstance = new ConsoleDatabaseService(dbPath, encryptionKey);
        }
    }

    /// <summary>
    /// Gets the default database path
    /// </summary>
    private static string GetDefaultDatabasePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var browserDataPath = Path.Combine(appDataPath, "VetaleBrowser", "Data");
        
        // Create directory if it doesn't exist
        if (!Directory.Exists(browserDataPath))
        {
            Directory.CreateDirectory(browserDataPath);
        }

        return Path.Combine(browserDataPath, "browser.db");
    }

    /// <summary>
    /// Gets the default history database path
    /// </summary>
    private static string GetDefaultHistoryDatabasePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var browserDataPath = Path.Combine(appDataPath, "VetaleBrowser", "Data");
        
        // Create directory if it doesn't exist
        if (!Directory.Exists(browserDataPath))
        {
            Directory.CreateDirectory(browserDataPath);
        }

        return Path.Combine(browserDataPath, "history.db");
    }

    /// <summary>
    /// Gets the default console database path
    /// </summary>
    private static string GetDefaultConsoleDatabasePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var browserDataPath = Path.Combine(appDataPath, "VetaleBrowser", "Data");
        
        // Create directory if it doesn't exist
        if (!Directory.Exists(browserDataPath))
        {
            Directory.CreateDirectory(browserDataPath);
        }

        return Path.Combine(browserDataPath, "console.db");
    }

    /// <summary>
    /// Generates encryption key
    /// </summary>
    private static string GenerateEncryptionKey()
    {
        // In production environment the key should be stored securely
        // For example, using Windows Data Protection API (DPAPI)
        var machineId = Environment.MachineName;
        var userId = Environment.UserName;
        var uniqueId = $"{machineId}_{userId}";
        
        return $"VetaleBrowser_Secure_2025_{uniqueId}_AES256_Key";
    }

    /// <summary>
    /// Creates a new browser session
    /// </summary>
    public static int CreateNewSession()
    {
        return Instance.CreateSession();
    }

    /// <summary>
    /// Adds a tab to the current session
    /// </summary>
    public static int AddTabToCurrentSession(string url, string title, bool isActive = false)
    {
        var session = Instance.GetCurrentSession();
        if (session == null)
        {
            session = Instance.GetCurrentSession();
            if (session == null)
            {
                var sessionId = CreateNewSession();
                return Instance.AddTab(sessionId, url, title, isActive);
            }
        }

        return Instance.AddTab(session.Id, url, title, isActive);
    }

    /// <summary>
    /// Shuts down the database
    /// </summary>
    public static void Shutdown()
    {
        lock (_lock)
        {
            _instance?.Dispose();
            _instance = null;
        }
        
        lock (_historyLock)
        {
            _historyInstance?.Dispose();
            _historyInstance = null;
        }
        
        lock (_consoleLock)
        {
            _consoleInstance?.Dispose();
            _consoleInstance = null;
        }
    }
}

