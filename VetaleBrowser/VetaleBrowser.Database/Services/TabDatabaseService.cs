using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Сервіс для роботи з базою даних вкладок
/// </summary>
public class TabDatabaseService : ITabDatabaseService, IDisposable
{
    private readonly LiteDatabase _database;
    private readonly DatabaseEncryptionService _encryptionService;
    private readonly ILiteCollection<TabModel> _tabsCollection;
    private readonly ILiteCollection<BrowserSession> _sessionsCollection;
    private readonly ILiteCollection<Bookmark> _bookmarksCollection;
    private readonly string _databasePath;
    
    // MEMORY OPTIMIZATION: Зменшені ліміти
    private const int MaxTabsPerSession = 100; // Зменшено з 1000
    private const int MaxTotalTabs = 500;      // Зменшено з 10000
    private const int MaxSessions = 20;        // Зменшено з 100
    private const int MaxBookmarks = 5000;     // Зменшено з 50000
    private const long MaxDatabaseSizeBytes = 50 * 1024 * 1024; // 50 MB замість 500 MB

    public TabDatabaseService(string databasePath, string encryptionKey)
    {
        _encryptionService = new DatabaseEncryptionService(encryptionKey);
        _databasePath = databasePath;
        
        // Створюємо директорію для бази даних якщо не існує
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // MEMORY OPTIMIZATION: Direct mode замість Shared
        var connectionString = new ConnectionString
        {
            Filename = databasePath,
            Connection = ConnectionType.Direct // Менше RAM
        };

        _database = new LiteDatabase(connectionString);
        
        // MEMORY OPTIMIZATION: Checkpoint для звільнення пам'яті
        try { _database.Checkpoint(); } catch { }
        
        // Отримуємо колекції
        _tabsCollection = _database.GetCollection<TabModel>("tabs");
        _sessionsCollection = _database.GetCollection<BrowserSession>("sessions");
        _bookmarksCollection = _database.GetCollection<Bookmark>("bookmarks");
        
        // MEMORY OPTIMIZATION: Мінімальна кількість індексів
        _tabsCollection.EnsureIndex(x => x.SessionId);
        _sessionsCollection.EnsureIndex(x => x.IsCurrent);
        
        // Перевіряємо розмір бази даних
        CheckDatabaseSize();
    }

    /// <summary>
    /// Перевіряє розмір бази даних та виконує очищення якщо потрібно
    /// </summary>
    private void CheckDatabaseSize()
    {
        var dbFileInfo = new FileInfo(_databasePath);
        if (dbFileInfo.Exists && dbFileInfo.Length > MaxDatabaseSizeBytes)
        {
            // Видаляємо старі сесії та неактивні вкладки
            CleanupOldData();
            
            // Оптимізуємо базу даних
            _database.Rebuild();
            
            // MEMORY OPTIMIZATION: Примусовий GC після rebuild
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    /// <summary>
    /// Очищає старі дані з бази
    /// </summary>
    private void CleanupOldData()
    {
        // Видаляємо старі сесії (залишаємо тільки останні MaxSessions)
        var sessionsToKeep = _sessionsCollection
            .Query()
            .OrderByDescending(x => x.StartedAt)
            .Limit(MaxSessions)
            .ToList()
            .Select(x => x.Id)
            .ToList();

        var sessionsToDelete = _sessionsCollection
            .Query()
            .Where(x => !sessionsToKeep.Contains(x.Id))
            .ToList();

        foreach (var session in sessionsToDelete)
        {
            DeleteSession(session.Id);
        }

        // Видаляємо найстаріші вкладки якщо їх забагато
        var totalTabs = _tabsCollection.Count();
        if (totalTabs > MaxTotalTabs)
        {
            var tabsToDelete = totalTabs - MaxTotalTabs;
            var oldTabs = _tabsCollection
                .Query()
                .OrderBy(x => x.LastAccessedAt)
                .Limit(tabsToDelete)
                .ToList();

            foreach (var tab in oldTabs)
            {
                _tabsCollection.Delete(tab.Id);
            }
        }
    }

    /// <summary>
    /// Створює нову сесію браузера
    /// </summary>
    public int CreateSession()
    {
        // Закриваємо попередню поточну сесію
        var currentSession = _sessionsCollection.FindOne(x => x.IsCurrent);
        if (currentSession != null)
        {
            currentSession.IsCurrent = false;
            currentSession.EndedAt = DateTime.UtcNow;
            _sessionsCollection.Update(currentSession);
        }

        var session = new BrowserSession
        {
            StartedAt = DateTime.UtcNow,
            IsCurrent = true,
            TabCount = 0
        };

        return _sessionsCollection.Insert(session);
    }

    /// <summary>
    /// Отримує поточну сесію
    /// </summary>
    public BrowserSession? GetCurrentSession()
    {
        return _sessionsCollection.FindOne(x => x.IsCurrent);
    }

    /// <summary>
    /// Додає вкладку до сесії
    /// </summary>
    public int AddTab(int sessionId, string url, string title, bool isActive = false)
    {
        // Перевіряємо обмеження
        var tabsInSession = _tabsCollection.Count(x => x.SessionId == sessionId);
        if (tabsInSession >= MaxTabsPerSession)
        {
            throw new InvalidOperationException($"Досягнуто максимальну кількість вкладок у сесії ({MaxTabsPerSession})");
        }

        var totalTabs = _tabsCollection.Count();
        if (totalTabs >= MaxTotalTabs)
        {
            CleanupOldData();
        }

        // Шифруємо чутливі дані
        var encryptedUrl = _encryptionService.EncryptString(url);
        var encryptedTitle = _encryptionService.EncryptString(title);

        var tab = new TabModel
        {
            Url = encryptedUrl,
            Title = encryptedTitle,
            SessionId = sessionId,
            CreatedAt = DateTime.UtcNow,
            LastAccessedAt = DateTime.UtcNow,
            IsActive = isActive,
            Order = tabsInSession
        };

        var id = _tabsCollection.Insert(tab);

        // Оновлюємо лічильник вкладок в сесії
        var session = _sessionsCollection.FindById(sessionId);
        if (session != null)
        {
            session.TabCount = _tabsCollection.Count(x => x.SessionId == sessionId);
            _sessionsCollection.Update(session);
        }

        return id;
    }

    /// <summary>
    /// Оновлює вкладку
    /// </summary>
    public void UpdateTab(int tabId, string? url = null, string? title = null, bool? isActive = null)
    {
        var tab = _tabsCollection.FindById(tabId);
        if (tab == null) return;

        if (url != null)
            tab.Url = _encryptionService.EncryptString(url);
        
        if (title != null)
            tab.Title = _encryptionService.EncryptString(title);
        
        if (isActive.HasValue)
            tab.IsActive = isActive.Value;

        tab.LastAccessedAt = DateTime.UtcNow;
        _tabsCollection.Update(tab);
    }

    /// <summary>
    /// Отримує всі вкладки сесії
    /// </summary>
    public List<TabModel> GetSessionTabs(int sessionId)
    {
        var tabs = _tabsCollection
            .Find(x => x.SessionId == sessionId)
            .OrderBy(x => x.Order)
            .ToList();

        // Розшифровуємо дані
        foreach (var tab in tabs)
        {
            try
            {
                tab.Url = _encryptionService.DecryptString(tab.Url);
                tab.Title = _encryptionService.DecryptString(tab.Title);
            }
            catch
            {
                // Якщо не вдалося розшифрувати, залишаємо як є
            }
        }

        return tabs;
    }

    /// <summary>
    /// Видаляє вкладку
    /// </summary>
    public void DeleteTab(int tabId)
    {
        var tab = _tabsCollection.FindById(tabId);
        if (tab != null)
        {
            _tabsCollection.Delete(tabId);
            
            // Оновлюємо лічильник
            var session = _sessionsCollection.FindById(tab.SessionId);
            if (session != null)
            {
                session.TabCount = _tabsCollection.Count(x => x.SessionId == tab.SessionId);
                _sessionsCollection.Update(session);
            }
        }
    }

    /// <summary>
    /// Видаляє сесію з усіма вкладками
    /// </summary>
    public void DeleteSession(int sessionId)
    {
        // Видаляємо всі вкладки сесії
        _tabsCollection.DeleteMany(x => x.SessionId == sessionId);
        
        // Видаляємо сесію
        _sessionsCollection.Delete(sessionId);
    }

    /// <summary>
    /// Додає закладку
    /// </summary>
    public int AddBookmark(string url, string title, string folder = "Закладки")
    {
        // Перевіряємо обмеження
        var totalBookmarks = _bookmarksCollection.Count();
        if (totalBookmarks >= MaxBookmarks)
        {
            throw new InvalidOperationException($"Досягнуто максимальну кількість закладок ({MaxBookmarks})");
        }

        // Шифруємо дані
        var encryptedUrl = _encryptionService.EncryptString(url);
        var encryptedTitle = _encryptionService.EncryptString(title);

        var bookmark = new Bookmark
        {
            Url = encryptedUrl,
            Title = encryptedTitle,
            Folder = folder,
            CreatedAt = DateTime.UtcNow,
            Order = _bookmarksCollection.Count(x => x.Folder == folder)
        };

        return _bookmarksCollection.Insert(bookmark);
    }

    /// <summary>
    /// Отримує всі закладки
    /// </summary>
    public List<Bookmark> GetBookmarks(string? folder = null)
    {
        var query = folder != null
            ? _bookmarksCollection.Find(x => x.Folder == folder)
            : _bookmarksCollection.FindAll();

        var bookmarks = query.OrderBy(x => x.Order).ToList();

        // Розшифровуємо дані
        foreach (var bookmark in bookmarks)
        {
            try
            {
                bookmark.Url = _encryptionService.DecryptString(bookmark.Url);
                bookmark.Title = _encryptionService.DecryptString(bookmark.Title);
            }
            catch
            {
                // Якщо не вдалося розшифрувати, залишаємо як є
            }
        }

        return bookmarks;
    }

    /// <summary>
    /// Видаляє закладку
    /// </summary>
    public void DeleteBookmark(int bookmarkId)
    {
        _bookmarksCollection.Delete(bookmarkId);
    }

    /// <summary>
    /// Отримує статистику бази даних
    /// </summary>
    public DatabaseStats GetStats()
    {
        return new DatabaseStats
        {
            TotalTabs = _tabsCollection.Count(),
            TotalSessions = _sessionsCollection.Count(),
            TotalBookmarks = _bookmarksCollection.Count(),
            DatabaseSizeBytes = new FileInfo(_databasePath).Length,
            MaxDatabaseSizeBytes = MaxDatabaseSizeBytes
        };
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}

/// <summary>
/// Статистика бази даних
/// </summary>
public class DatabaseStats
{
    public int TotalTabs { get; set; }
    public int TotalSessions { get; set; }
    public int TotalBookmarks { get; set; }
    public long DatabaseSizeBytes { get; set; }
    public long MaxDatabaseSizeBytes { get; set; }
    public double UsagePercentage => (double)DatabaseSizeBytes / MaxDatabaseSizeBytes * 100;
}

