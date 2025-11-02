using System;
using System.IO;
using VetaleBrowser.VetaleBrowser.Database.Services;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.GlobalManagers;

/// <summary>
/// Глобальний менеджер бази даних
/// </summary>
public static class DatabaseManager
{
    private static TabDatabaseService? _instance;
    private static HistoryDatabaseService? _historyInstance;
    private static readonly object _lock = new object();
    private static readonly object _historyLock = new object();

    /// <summary>
    /// Отримує екземпляр сервісу бази даних
    /// </summary>
    public static ITabDatabaseService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        Initialize();
                    }
                }
            }
            return _instance!;
        }
    }

    /// <summary>
    /// Отримує екземпляр сервісу бази даних історії
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
    /// Ініціалізує базу даних
    /// </summary>
    public static void Initialize(string? customPath = null, string? customKey = null)
    {
        lock (_lock)
        {
            // Закриваємо попередній екземпляр якщо існує
            _instance?.Dispose();

            // Визначаємо шлях до бази даних
            var dbPath = customPath ?? GetDefaultDatabasePath();
            
            // Визначаємо ключ шифрування
            var encryptionKey = customKey ?? GenerateEncryptionKey();

            // Створюємо новий екземпляр
            _instance = new TabDatabaseService(dbPath, encryptionKey);

            // Створюємо початкову сесію якщо немає поточної
            var currentSession = _instance.GetCurrentSession();
            if (currentSession == null)
            {
                _instance.CreateSession();
            }
        }
    }

    /// <summary>
    /// Ініціалізує базу даних історії
    /// </summary>
    public static void InitializeHistory(string? customPath = null, string? customKey = null)
    {
        lock (_historyLock)
        {
            // Закриваємо попередній екземпляр якщо існує
            _historyInstance?.Dispose();

            // Визначаємо шлях до бази даних історії
            var dbPath = customPath ?? GetDefaultHistoryDatabasePath();
            
            // Визначаємо ключ шифрування
            var encryptionKey = customKey ?? GenerateEncryptionKey();

            // Створюємо новий екземпляр
            _historyInstance = new HistoryDatabaseService(dbPath, encryptionKey);
        }
    }

    /// <summary>
    /// Отримує шлях до бази даних за замовчуванням
    /// </summary>
    private static string GetDefaultDatabasePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var browserDataPath = Path.Combine(appDataPath, "VetaleBrowser", "Data");
        
        // Створюємо директорію якщо не існує
        if (!Directory.Exists(browserDataPath))
        {
            Directory.CreateDirectory(browserDataPath);
        }

        return Path.Combine(browserDataPath, "browser.db");
    }

    /// <summary>
    /// Отримує шлях до бази даних історії за замовчуванням
    /// </summary>
    private static string GetDefaultHistoryDatabasePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var browserDataPath = Path.Combine(appDataPath, "VetaleBrowser", "Data");
        
        // Створюємо директорію якщо не існує
        if (!Directory.Exists(browserDataPath))
        {
            Directory.CreateDirectory(browserDataPath);
        }

        return Path.Combine(browserDataPath, "history.db");
    }

    /// <summary>
    /// Генерує ключ шифрування
    /// </summary>
    private static string GenerateEncryptionKey()
    {
        // В продакшн середовищі ключ має зберігатися безпечно
        // Наприклад, використовуючи Windows Data Protection API (DPAPI)
        var machineId = Environment.MachineName;
        var userId = Environment.UserName;
        var uniqueId = $"{machineId}_{userId}";
        
        return $"VetaleBrowser_Secure_2025_{uniqueId}_AES256_Key";
    }

    /// <summary>
    /// Створює нову сесію браузера
    /// </summary>
    public static int CreateNewSession()
    {
        return Instance.CreateSession();
    }

    /// <summary>
    /// Додає вкладку до поточної сесії
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
    /// Закриває базу даних
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

