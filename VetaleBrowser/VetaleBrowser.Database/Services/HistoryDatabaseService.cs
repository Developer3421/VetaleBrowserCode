using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using LiteDB;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Service for working with the browsing history database
/// MEMORY OPTIMIZED: Lazy loading, pagination, caching, batch operations
/// </summary>
public class HistoryDatabaseService : IHistoryDatabaseService, IDisposable
{
    private LiteDatabase? _database;
    private ILiteCollection<HistoryItem>? _historyCollection;
    private readonly DatabaseEncryptionService _encryptionService;
    private readonly string _databasePath;
    private readonly object _lock = new object();
    private bool _disposed;
    
    // MEMORY OPTIMIZATION: Aggressively reduced limits for AMD cards
    private const int MaxHistoryItems = 5000;      // Reduced from 50000
    private const int DefaultPageSize = 50;        // Reduced from 100
    private const long MaxDatabaseSizeBytes = 20 * 1024 * 1024; // 20 MB instead of 200 MB
    
    // MEMORY OPTIMIZATION: Minimal cache
    private readonly Dictionary<string, (HistoryItem item, DateTime cachedAt)> _urlCache = new(StringComparer.OrdinalIgnoreCase);
    private const int MaxCacheSize = 20;           // Reduced from 100

    // Lazy initialization flag
    private bool _isInitialized;

    public HistoryDatabaseService(string databasePath, string encryptionKey)
    {
        _encryptionService = new DatabaseEncryptionService(encryptionKey);
        _databasePath = databasePath;
        
        // MEMORY OPTIMIZATION: Don't initialize DB immediately - use lazy init
        // Database will be opened on first actual use
    }

    /// <summary>
    /// Lazy initialization of database connection
    /// </summary>
    private void EnsureInitialized()
    {
        if (_isInitialized) return;
        
        lock (_lock)
        {
            if (_isInitialized) return;
            
            // Retry logic for cases when the file is temporarily locked
            const int maxRetries = 3;
            const int retryDelayMs = 100;
            
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    // Create database directory if it doesn't exist
                    var directory = Path.GetDirectoryName(_databasePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // FIX: Use Shared to support multiple connections
                    var connectionString = new ConnectionString
                    {
                        Filename = _databasePath,
                        Connection = ConnectionType.Shared, // Allows multiple connections
                        ReadOnly = false,
                    };

                    _database = new LiteDatabase(connectionString);
                    
                    // Force checkpoint to free memory
                    try { _database.Checkpoint(); } catch { }
                    
                    _historyCollection = _database.GetCollection<HistoryItem>("history");
                    
                    // Only necessary indexes (fewer indexes = less RAM)
                    _historyCollection.EnsureIndex(x => x.VisitedAt);
                    
                    _isInitialized = true;
                    
                    // Check database size asynchronously
                    ThreadPool.QueueUserWorkItem(_ => CheckDatabaseSizeAsync());
                    
                    return; // Success - exit
                }
                catch (IOException ex) when (attempt < maxRetries - 1)
                {
                    // File is locked - wait and try again
                    Console.WriteLine($"[HistoryDB] Attempt {attempt + 1} failed, retrying: {ex.Message}");
                    Thread.Sleep(retryDelayMs * (attempt + 1));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error initializing history database: {ex.Message}");
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Asynchronous database size check
    /// </summary>
    private void CheckDatabaseSizeAsync()
    {
        try
        {
            var dbFileInfo = new FileInfo(_databasePath);
            if (dbFileInfo.Exists && dbFileInfo.Length > MaxDatabaseSizeBytes)
            {
                lock (_lock)
                {
                    CleanupOldData();
                    // MEMORY OPTIMIZATION: Rebuild only when really needed
                    if (dbFileInfo.Length > MaxDatabaseSizeBytes * 1.5)
                    {
                        _database?.Rebuild();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking database size: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears old data from the database
    /// </summary>
    private void CleanupOldData()
    {
        if (_historyCollection == null) return;
        
        var totalItems = _historyCollection.Count();
        if (totalItems > MaxHistoryItems)
        {
            var itemsToDelete = totalItems - MaxHistoryItems;
            // MEMORY OPTIMIZATION: Use batch delete with IDs only
            var idsToDelete = _historyCollection
                .Query()
                .OrderBy(x => x.VisitedAt)
                .Limit(itemsToDelete)
                .ToList()
                .Select(x => x.Id)
                .ToList();

            foreach (var id in idsToDelete)
            {
                _historyCollection.Delete(id);
            }
        }
        
        // Clear cache after cleanup
        ClearCache();
    }

    /// <summary>
    /// Adds or updates a record in the history
    /// </summary>
    public void AddOrUpdateHistoryItem(string url, string title, string? faviconUrl = null, byte[]? faviconData = null)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        
        EnsureInitialized();
        if (_historyCollection == null) return;

        try
        {
            // Encrypt sensitive data
            var encryptedUrl = _encryptionService.EncryptString(url);
            var encryptedTitle = !string.IsNullOrWhiteSpace(title) 
                ? _encryptionService.EncryptString(title) 
                : _encryptionService.EncryptString(url);

            lock (_lock)
            {
                // Check whether a record with this URL exists
                var existing = _historyCollection.FindOne(x => x.Url == encryptedUrl);
                
                if (existing != null)
                {
                    // Update the existing record
                    existing.Title = encryptedTitle;
                    existing.VisitedAt = DateTime.UtcNow;
                    existing.VisitCount++;
                    
                    // MEMORY OPTIMIZATION: Only update favicon if provided and different
                    if (faviconUrl != null && faviconUrl != existing.FaviconUrl)
                        existing.FaviconUrl = faviconUrl;
                    if (faviconData != null && (existing.FaviconData == null || !faviconData.SequenceEqual(existing.FaviconData)))
                        existing.FaviconData = faviconData;
                    
                    _historyCollection.Update(existing);
                    
                    // Update cache
                    UpdateCache(url, existing);
                }
                else
                {
                    // Create a new record
                    var historyItem = new HistoryItem
                    {
                        Url = encryptedUrl,
                        Title = encryptedTitle,
                        FaviconUrl = faviconUrl,
                        FaviconData = faviconData,
                        VisitedAt = DateTime.UtcNow,
                        VisitCount = 1
                    };

                    _historyCollection.Insert(historyItem);
                    UpdateCache(url, historyItem);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding/updating history item: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets history with pagination (MEMORY OPTIMIZED)
    /// </summary>
    public List<HistoryItem> GetHistory(DateTime? startDate = null, DateTime? endDate = null, int page = 0, int pageSize = DefaultPageSize)
    {
        EnsureInitialized();
        if (_historyCollection == null) return new List<HistoryItem>();

        try
        {
            var query = _historyCollection.Query();

            if (startDate.HasValue)
                query = query.Where(x => x.VisitedAt >= startDate.Value);
            
            if (endDate.HasValue)
                query = query.Where(x => x.VisitedAt <= endDate.Value);

            // MEMORY OPTIMIZATION: Use pagination
            var items = query
                .OrderByDescending(x => x.VisitedAt)
                .Skip(page * pageSize)
                .Limit(pageSize)
                .ToList();

            // Decrypt data
            DecryptHistoryItems(items);

            return items;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting history: {ex.Message}");
            return new List<HistoryItem>();
        }
    }

    /// <summary>
    /// Overload for compatibility - returns first page
    /// </summary>
    public List<HistoryItem> GetHistory(DateTime? startDate = null, DateTime? endDate = null)
    {
        return GetHistory(startDate, endDate, 0, DefaultPageSize);
    }

    /// <summary>
    /// Decrypts history items (extracted for reuse)
    /// </summary>
    private void DecryptHistoryItems(List<HistoryItem> items)
    {
        foreach (var item in items)
        {
            try
            {
                item.Url = _encryptionService.DecryptString(item.Url);
                item.Title = _encryptionService.DecryptString(item.Title);
            }
            catch
            {
                // If decryption fails, leave as is
            }
        }
    }

    /// <summary>
    /// Deletes a record from the history
    /// </summary>
    public void DeleteHistoryItem(int id)
    {
        EnsureInitialized();
        if (_historyCollection == null) return;

        try
        {
            lock (_lock)
            {
                _historyCollection.Delete(id);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting history item: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears the entire history
    /// </summary>
    public void ClearHistory()
    {
        EnsureInitialized();
        if (_historyCollection == null) return;

        try
        {
            lock (_lock)
            {
                _historyCollection.DeleteAll();
                ClearCache();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing history: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears history older than the specified date (OPTIMIZED: batch delete)
    /// </summary>
    public void ClearHistoryOlderThan(DateTime date)
    {
        EnsureInitialized();
        if (_historyCollection == null) return;

        try
        {
            lock (_lock)
            {
                // MEMORY OPTIMIZATION: Use batch delete by expression
                _historyCollection.DeleteMany(x => x.VisitedAt < date);
                ClearCache();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing old history: {ex.Message}");
        }
    }

    /// <summary>
    /// Searches in history (OPTIMIZED: with pagination)
    /// </summary>
    public List<HistoryItem> SearchHistory(string query, int page = 0, int pageSize = DefaultPageSize)
    {
        if (string.IsNullOrWhiteSpace(query))
            return GetHistory(null, null, page, pageSize);

        EnsureInitialized();
        if (_historyCollection == null) return new List<HistoryItem>();

        try
        {
            // MEMORY OPTIMIZATION: Get paginated results then filter in-memory
            // This is still efficient because we limit the batch size
            var allItems = GetHistory(null, null, page, pageSize * 3); // Get more items to have enough after filtering
            var searchTermLower = query.ToLower();

            return allItems
                .Where(x => 
                    x.Url.ToLower().Contains(searchTermLower) || 
                    x.Title.ToLower().Contains(searchTermLower))
                .Take(pageSize)
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error searching history: {ex.Message}");
            return new List<HistoryItem>();
        }
    }

    /// <summary>
    /// Overload for compatibility
    /// </summary>
    public List<HistoryItem> SearchHistory(string query)
    {
        return SearchHistory(query, 0, DefaultPageSize);
    }

    /// <summary>
    /// MEMORY OPTIMIZATION: Cache management
    /// </summary>
    private void UpdateCache(string url, HistoryItem item)
    {
        if (_urlCache.Count >= MaxCacheSize)
        {
            // Remove oldest entries
            var toRemove = _urlCache
                .OrderBy(x => x.Value.cachedAt)
                .Take(MaxCacheSize / 4)
                .Select(x => x.Key)
                .ToList();
            
            foreach (var key in toRemove)
            {
                _urlCache.Remove(key);
            }
        }
        
        _urlCache[url] = (item, DateTime.UtcNow);
    }

    private void ClearCache()
    {
        _urlCache.Clear();
    }

    /// <summary>
    /// Gets the number of records in the history
    /// </summary>
    public int GetHistoryCount()
    {
        EnsureInitialized();
        return _historyCollection?.Count() ?? 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        lock (_lock)
        {
            ClearCache();
            _database?.Dispose();
            _database = null;
            _historyCollection = null;
        }
    }
}
