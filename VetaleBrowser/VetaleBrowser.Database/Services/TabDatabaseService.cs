using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Service for working with the tab database
/// </summary>
public class TabDatabaseService : ITabDatabaseService, IDisposable
{
    private readonly LiteDatabase _database;
    private readonly DatabaseEncryptionService _encryptionService;
    private readonly ILiteCollection<TabModel> _tabsCollection;
    private readonly ILiteCollection<BrowserSession> _sessionsCollection;
    private readonly ILiteCollection<Bookmark> _bookmarksCollection;
    private readonly string _databasePath;
    
    // MEMORY OPTIMIZATION: Reduced limits
    private const int MaxTabsPerSession = 100; // Reduced from 1000
    private const int MaxTotalTabs = 500;      // Reduced from 10000
    private const int MaxSessions = 20;        // Reduced from 100
    private const int MaxBookmarks = 5000;     // Reduced from 50000
    private const long MaxDatabaseSizeBytes = 50 * 1024 * 1024; // 50 MB instead of 500 MB

    public TabDatabaseService(string databasePath, string encryptionKey)
    {
        _encryptionService = new DatabaseEncryptionService(encryptionKey);
        _databasePath = databasePath;
        
        // Create directory for database if it doesn't exist
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // MEMORY OPTIMIZATION: Shared mode to allow concurrent access
        var connectionString = new ConnectionString
        {
            Filename = databasePath,
            Connection = ConnectionType.Shared // Allows access from different places
        };

        _database = new LiteDatabase(connectionString);
        
        // MEMORY OPTIMIZATION: Checkpoint to free memory
        try { _database.Checkpoint(); } catch { }
        
        // Get collections
        _tabsCollection = _database.GetCollection<TabModel>("tabs");
        _sessionsCollection = _database.GetCollection<BrowserSession>("sessions");
        _bookmarksCollection = _database.GetCollection<Bookmark>("bookmarks");
        
        // MEMORY OPTIMIZATION: Minimal number of indexes
        _tabsCollection.EnsureIndex(x => x.SessionId);
        _sessionsCollection.EnsureIndex(x => x.IsCurrent);
        
        // Check database size
        CheckDatabaseSize();
    }

    /// <summary>
    /// Checks database size and performs cleanup if needed
    /// </summary>
    private void CheckDatabaseSize()
    {
        var dbFileInfo = new FileInfo(_databasePath);
        if (dbFileInfo.Exists && dbFileInfo.Length > MaxDatabaseSizeBytes)
        {
            // Delete old sessions and inactive tabs
            CleanupOldData();
            
            // Optimize database
            _database.Rebuild();
            
            // MEMORY OPTIMIZATION: Force GC after rebuild
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    /// <summary>
    /// Cleans up old data from the database
    /// </summary>
    private void CleanupOldData()
    {
        // Delete old sessions (keep only the last MaxSessions)
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

        // Delete oldest tabs if there are too many
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
    /// Creates a new browser session
    /// </summary>
    public int CreateSession()
    {
        // Close previous current session
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
    /// Gets the current session
    /// </summary>
    public BrowserSession? GetCurrentSession()
    {
        return _sessionsCollection.FindOne(x => x.IsCurrent);
    }

    /// <summary>
    /// Adds a tab to a session
    /// </summary>
    public int AddTab(int sessionId, string url, string title, bool isActive = false)
    {
        // Check limits
        var tabsInSession = _tabsCollection.Count(x => x.SessionId == sessionId);
        if (tabsInSession >= MaxTabsPerSession)
        {
            throw new InvalidOperationException($"Maximum number of tabs per session reached ({MaxTabsPerSession})");
        }

        var totalTabs = _tabsCollection.Count();
        if (totalTabs >= MaxTotalTabs)
        {
            CleanupOldData();
        }

        // Encrypt sensitive data
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

        // Update tab count in session
        var session = _sessionsCollection.FindById(sessionId);
        if (session != null)
        {
            session.TabCount = _tabsCollection.Count(x => x.SessionId == sessionId);
            _sessionsCollection.Update(session);
        }

        return id;
    }

    /// <summary>
    /// Updates a tab
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
    /// Gets all tabs for a session
    /// </summary>
    public List<TabModel> GetSessionTabs(int sessionId)
    {
        var tabs = _tabsCollection
            .Find(x => x.SessionId == sessionId)
            .OrderBy(x => x.Order)
            .ToList();

        // Decrypt data
        foreach (var tab in tabs)
        {
            try
            {
                tab.Url = _encryptionService.DecryptString(tab.Url);
                tab.Title = _encryptionService.DecryptString(tab.Title);
            }
            catch
            {
                // If decryption failed, leave as is
            }
        }

        return tabs;
    }

    /// <summary>
    /// Deletes a tab
    /// </summary>
    public void DeleteTab(int tabId)
    {
        var tab = _tabsCollection.FindById(tabId);
        if (tab != null)
        {
            _tabsCollection.Delete(tabId);
            
            // Update counter
            var session = _sessionsCollection.FindById(tab.SessionId);
            if (session != null)
            {
                session.TabCount = _tabsCollection.Count(x => x.SessionId == tab.SessionId);
                _sessionsCollection.Update(session);
            }
        }
    }

    /// <summary>
    /// Deletes a session with all its tabs
    /// </summary>
    public void DeleteSession(int sessionId)
    {
        // Delete all session tabs
        _tabsCollection.DeleteMany(x => x.SessionId == sessionId);
        
        // Delete session
        _sessionsCollection.Delete(sessionId);
    }

    /// <summary>
    /// Adds a bookmark
    /// </summary>
    public int AddBookmark(string url, string title, string folder = "Bookmarks")
    {
        // Check limits
        var totalBookmarks = _bookmarksCollection.Count();
        if (totalBookmarks >= MaxBookmarks)
        {
            throw new InvalidOperationException($"Maximum number of bookmarks reached ({MaxBookmarks})");
        }

        // Encrypt data
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
    /// Gets all bookmarks
    /// </summary>
    public List<Bookmark> GetBookmarks(string? folder = null)
    {
        var query = folder != null
            ? _bookmarksCollection.Find(x => x.Folder == folder)
            : _bookmarksCollection.FindAll();

        var bookmarks = query.OrderBy(x => x.Order).ToList();

        // Decrypt data
        foreach (var bookmark in bookmarks)
        {
            try
            {
                bookmark.Url = _encryptionService.DecryptString(bookmark.Url);
                bookmark.Title = _encryptionService.DecryptString(bookmark.Title);
            }
            catch
            {
                // If decryption failed, leave as is
            }
        }

        return bookmarks;
    }

    /// <summary>
    /// Deletes a bookmark
    /// </summary>
    public void DeleteBookmark(int bookmarkId)
    {
        _bookmarksCollection.Delete(bookmarkId);
    }

    /// <summary>
    /// Gets database statistics
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
/// Database statistics
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
