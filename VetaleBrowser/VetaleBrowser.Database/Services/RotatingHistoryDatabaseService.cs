using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Service for working with history database rotation.
/// Automatically creates a new DB when full and reads from all sequentially.
/// </summary>
public class RotatingHistoryDatabaseService : IHistoryDatabaseService
{
    private readonly string _baseDatabasePath;
    private readonly string _encryptionKey;
    private readonly List<HistoryDatabaseService> _databases = new();
    private HistoryDatabaseService _currentDatabase;
    private readonly object _lock = new object();
    private bool _disposed;
    
    // Rotation parameters
    private const long MaxDatabaseSizeBytes = 20 * 1024 * 1024; // 20 MB
    private const int MaxHistoryItemsPerDatabase = 5000;
    private const int MaxDatabaseFiles = 10; // Maximum number of DB files
    
    public RotatingHistoryDatabaseService(string baseDatabasePath, string encryptionKey)
    {
        _baseDatabasePath = baseDatabasePath;
        _encryptionKey = encryptionKey;
        
        // Load all existing databases
        LoadExistingDatabases();
        
        // Set current DB (last one or create new)
        _currentDatabase = _databases.LastOrDefault() ?? CreateNewDatabase();
    }
    
    /// <summary>
    /// Loads all existing history databases
    /// </summary>
    private void LoadExistingDatabases()
    {
        var directory = Path.GetDirectoryName(_baseDatabasePath);
        var baseFileName = Path.GetFileNameWithoutExtension(_baseDatabasePath);
        var extension = Path.GetExtension(_baseDatabasePath);
        
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return;
        
        // Look for files with pattern: history.db, history_1.db, history_2.db, etc.
        var pattern = $"{baseFileName}*.{extension}";
        var files = Directory.GetFiles(directory, pattern)
            .OrderBy(f => f) // Sort by name
            .ToList();
        
        foreach (var file in files)
        {
            try
            {
                var db = new HistoryDatabaseService(file, _encryptionKey);
                _databases.Add(db);
                Console.WriteLine($"[RotatingHistory] Loaded database: {Path.GetFileName(file)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RotatingHistory] Error loading database {file}: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Creates a new database for rotation
    /// </summary>
    private HistoryDatabaseService CreateNewDatabase()
    {
        lock (_lock)
        {
            var directory = Path.GetDirectoryName(_baseDatabasePath);
            var baseFileName = Path.GetFileNameWithoutExtension(_baseDatabasePath);
            var extension = Path.GetExtension(_baseDatabasePath);
            
            string newDbPath;
            if (_databases.Count == 0)
            {
                // First DB - use original path
                newDbPath = _baseDatabasePath;
            }
            else
            {
                // New DB with index
                newDbPath = Path.Combine(directory!, $"{baseFileName}_{_databases.Count}{extension}");
            }
            
            var newDb = new HistoryDatabaseService(newDbPath, _encryptionKey);
            _databases.Add(newDb);
            
            Console.WriteLine($"[RotatingHistory] Created new database: {Path.GetFileName(newDbPath)}");
            
            // Check whether the DB file limit has been exceeded
            CleanupOldDatabasesIfNeeded();
            
            return newDb;
        }
    }
    
    /// <summary>
    /// Deletes oldest DBs if limit exceeded
    /// </summary>
    private void CleanupOldDatabasesIfNeeded()
    {
        if (_databases.Count <= MaxDatabaseFiles)
            return;
        
        var databasesToRemove = _databases.Count - MaxDatabaseFiles;
        
        for (int i = 0; i < databasesToRemove; i++)
        {
            var oldDb = _databases[0];
            _databases.RemoveAt(0);
            
            try
            {
                var dbPath = oldDb.GetType().GetField("_databasePath", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(oldDb) as string;
                
                oldDb.Dispose();
                
                if (!string.IsNullOrEmpty(dbPath) && File.Exists(dbPath))
                {
                    File.Delete(dbPath);
                    Console.WriteLine($"[RotatingHistory] Deleted old database: {Path.GetFileName(dbPath)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RotatingHistory] Error deleting old database: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Checks if a new DB needs to be created for rotation
    /// </summary>
    private void CheckRotationNeeded()
    {
        lock (_lock)
        {
            var count = _currentDatabase.GetHistoryCount();
            
            // Check record count
            if (count >= MaxHistoryItemsPerDatabase)
            {
                Console.WriteLine($"[RotatingHistory] Rotation needed: {count} items in current database");
                _currentDatabase = CreateNewDatabase();
                return;
            }
            
            // Check file size
            try
            {
                var dbPath = _currentDatabase.GetType().GetField("_databasePath", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(_currentDatabase) as string;
                
                if (!string.IsNullOrEmpty(dbPath) && File.Exists(dbPath))
                {
                    var fileInfo = new FileInfo(dbPath);
                    if (fileInfo.Length >= MaxDatabaseSizeBytes)
                    {
                        Console.WriteLine($"[RotatingHistory] Rotation needed: {fileInfo.Length / 1024 / 1024}MB database size");
                        _currentDatabase = CreateNewDatabase();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RotatingHistory] Error checking database size: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Adds or updates a record in the history
    /// </summary>
    public void AddOrUpdateHistoryItem(string url, string title, string? faviconUrl = null, byte[]? faviconData = null)
    {
        // Check if rotation needed before adding
        CheckRotationNeeded();
        
        // Add to current DB
        _currentDatabase.AddOrUpdateHistoryItem(url, title, faviconUrl, faviconData);
    }
    
    /// <summary>
    /// Gets history from all databases with pagination
    /// </summary>
    public List<HistoryItem> GetHistory(DateTime? startDate = null, DateTime? endDate = null, int page = 0, int pageSize = 100)
    {
        var allItems = new List<HistoryItem>();
        
        // Read from all DBs in reverse order (newest first)
        for (int i = _databases.Count - 1; i >= 0; i--)
        {
            try
            {
                var items = _databases[i].GetHistory(startDate, endDate, 0, int.MaxValue);
                allItems.AddRange(items);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RotatingHistory] Error reading from database {i}: {ex.Message}");
            }
        }
        
        // Sort by date and apply pagination
        return allItems
            .OrderByDescending(x => x.VisitedAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToList();
    }
    
    /// <summary>
    /// Overload for compatibility
    /// </summary>
    public List<HistoryItem> GetHistory(DateTime? startDate = null, DateTime? endDate = null)
    {
        return GetHistory(startDate, endDate, 0, 100);
    }
    
    /// <summary>
    /// Deletes a record from the history (searches in all DBs)
    /// </summary>
    public void DeleteHistoryItem(int id)
    {
        foreach (var db in _databases)
        {
            try
            {
                db.DeleteHistoryItem(id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RotatingHistory] Error deleting item from database: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Clears the entire history (in all DBs)
    /// </summary>
    public void ClearHistory()
    {
        foreach (var db in _databases)
        {
            try
            {
                db.ClearHistory();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RotatingHistory] Error clearing database: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Clears history older than the specified date (in all DBs)
    /// </summary>
    public void ClearHistoryOlderThan(DateTime date)
    {
        foreach (var db in _databases)
        {
            try
            {
                db.ClearHistoryOlderThan(date);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RotatingHistory] Error clearing old history: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Searches in history (in all DBs) with pagination
    /// </summary>
    public List<HistoryItem> SearchHistory(string query, int page = 0, int pageSize = 100)
    {
        var allItems = new List<HistoryItem>();
        
        // Search in all DBs
        for (int i = _databases.Count - 1; i >= 0; i--)
        {
            try
            {
                var items = _databases[i].SearchHistory(query, 0, int.MaxValue);
                allItems.AddRange(items);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RotatingHistory] Error searching in database {i}: {ex.Message}");
            }
        }
        
        // Sort and apply pagination
        return allItems
            .OrderByDescending(x => x.VisitedAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToList();
    }
    
    /// <summary>
    /// Overload for compatibility
    /// </summary>
    public List<HistoryItem> SearchHistory(string query)
    {
        return SearchHistory(query, 0, 100);
    }
    
    /// <summary>
    /// Gets the total number of records in the history (from all DBs)
    /// </summary>
    public int GetHistoryCount()
    {
        var totalCount = 0;
        
        foreach (var db in _databases)
        {
            try
            {
                totalCount += db.GetHistoryCount();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RotatingHistory] Error getting count from database: {ex.Message}");
            }
        }
        
        return totalCount;
    }


    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        foreach (var db in _databases)
        {
            try
            {
                db?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RotatingHistory] Error disposing database: {ex.Message}");
            }
        }
        
        _databases.Clear();
    }
}

