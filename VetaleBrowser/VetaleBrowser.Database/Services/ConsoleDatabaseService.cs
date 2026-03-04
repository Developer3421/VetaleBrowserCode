using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Service for working with the console database with AES encryption
/// MEMORY OPTIMIZATION: Direct connection mode
/// </summary>
public class ConsoleDatabaseService : IDisposable
{
    private readonly LiteDatabase _database;
    private readonly DatabaseEncryptionService _encryptionService;
    private readonly ILiteCollection<ConsoleLogItem> _logsCollection;
    private readonly string _databasePath;
    
    // MEMORY OPTIMIZATION: Aggressively reduced limits
    private const int MaxLogItems = 1000;  // Reduced from 50000
    private const long MaxDatabaseSizeBytes = 5 * 1024 * 1024; // 5 MB instead of 100 MB

    public ConsoleDatabaseService(string databasePath, string encryptionKey)
    {
        _encryptionService = new DatabaseEncryptionService(encryptionKey);
        _databasePath = databasePath;
        
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // MEMORY OPTIMIZATION: Direct connection
        var connectionString = new ConnectionString
        {
            Filename = databasePath,
            Connection = ConnectionType.Shared
        };

        _database = new LiteDatabase(connectionString);
        try { _database.Checkpoint(); } catch { }
        
        _logsCollection = _database.GetCollection<ConsoleLogItem>("console_logs");
        // Minimum indexes
        _logsCollection.EnsureIndex(x => x.Timestamp);
        
        // Check database size
        CheckDatabaseSize();
    }

    /// <summary>
    /// Checks the database size and performs cleanup if needed
    /// </summary>
    private void CheckDatabaseSize()
    {
        var dbFileInfo = new FileInfo(_databasePath);
        if (dbFileInfo.Exists && dbFileInfo.Length > MaxDatabaseSizeBytes)
        {
            // Delete old records
            CleanupOldData();
            
            // Optimize the database
            _database.Rebuild();
        }
    }

    /// <summary>
    /// Clears old data from the database
    /// </summary>
    private void CleanupOldData()
    {
        var totalItems = _logsCollection.Count();
        if (totalItems > MaxLogItems)
        {
            var itemsToDelete = totalItems - MaxLogItems;
            var oldItems = _logsCollection
                .Query()
                .OrderBy(x => x.Timestamp)
                .Limit(itemsToDelete)
                .ToList();

            foreach (var item in oldItems)
            {
                _logsCollection.Delete(item.Id);
            }
        }
    }

    /// <summary>
    /// Adds a record to the console
    /// </summary>
    public void AddLog(string level, string message, string? source = null, string? stackTrace = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            // Encrypt sensitive data
            var encryptedMessage = _encryptionService.EncryptString(message);
            var encryptedSource = !string.IsNullOrWhiteSpace(source) 
                ? _encryptionService.EncryptString(source) 
                : null;
            var encryptedStackTrace = !string.IsNullOrWhiteSpace(stackTrace)
                ? _encryptionService.EncryptString(stackTrace)
                : null;

            var logItem = new ConsoleLogItem
            {
                Level = level,
                Message = encryptedMessage,
                Source = encryptedSource,
                StackTrace = encryptedStackTrace,
                Timestamp = DateTime.UtcNow
            };

            _logsCollection.Insert(logItem);
            
            // Check whether the limit has been exceeded
            CheckDatabaseSize();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConsoleDatabaseService] Error adding log: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets all logs or with a filter
    /// </summary>
    public List<ConsoleLogItem> GetLogs(DateTime? startDate = null, DateTime? endDate = null, string? level = null)
    {
        try
        {
            var query = _logsCollection.Query();

            if (startDate.HasValue)
                query = query.Where(x => x.Timestamp >= startDate.Value);
            
            if (endDate.HasValue)
                query = query.Where(x => x.Timestamp <= endDate.Value);

            if (!string.IsNullOrWhiteSpace(level))
                query = query.Where(x => x.Level == level);

            var items = query.OrderByDescending(x => x.Timestamp).ToList();

            // Decrypt data
            foreach (var item in items)
            {
                try
                {
                    item.Message = _encryptionService.DecryptString(item.Message);
                    if (!string.IsNullOrEmpty(item.Source))
                        item.Source = _encryptionService.DecryptString(item.Source);
                    if (!string.IsNullOrEmpty(item.StackTrace))
                        item.StackTrace = _encryptionService.DecryptString(item.StackTrace);
                }
                catch
                {
                    // If decryption fails, leave as is
                }
            }

            return items;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConsoleDatabaseService] Error getting logs: {ex.Message}");
            return new List<ConsoleLogItem>();
        }
    }

    /// <summary>
    /// Deletes a record from the console
    /// </summary>
    public void DeleteLog(int id)
    {
        try
        {
            _logsCollection.Delete(id);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConsoleDatabaseService] Error deleting log: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears all logs
    /// </summary>
    public void ClearLogs()
    {
        try
        {
            _logsCollection.DeleteAll();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConsoleDatabaseService] Error clearing logs: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears logs older than the specified date
    /// </summary>
    public void ClearLogsOlderThan(DateTime date)
    {
        try
        {
            var itemsToDelete = _logsCollection
                .Query()
                .Where(x => x.Timestamp < date)
                .ToList();

            foreach (var item in itemsToDelete)
            {
                _logsCollection.Delete(item.Id);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConsoleDatabaseService] Error clearing old logs: {ex.Message}");
        }
    }

    /// <summary>
    /// Searches in logs
    /// </summary>
    public List<ConsoleLogItem> SearchLogs(string query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
                return GetLogs();

            var allItems = GetLogs();
            var searchTermLower = query.ToLower();

            return allItems
                .Where(x => 
                    x.Message.ToLower().Contains(searchTermLower) || 
                    (x.Source != null && x.Source.ToLower().Contains(searchTermLower)))
                .ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConsoleDatabaseService] Error searching logs: {ex.Message}");
            return new List<ConsoleLogItem>();
        }
    }

    /// <summary>
    /// Gets the log count by level
    /// </summary>
    public Dictionary<string, int> GetLogCountByLevel()
    {
        try
        {
            var result = new Dictionary<string, int>
            {
                ["Info"] = _logsCollection.Count(x => x.Level == "Info"),
                ["Warning"] = _logsCollection.Count(x => x.Level == "Warning"),
                ["Error"] = _logsCollection.Count(x => x.Level == "Error"),
                ["Debug"] = _logsCollection.Count(x => x.Level == "Debug")
            };
            return result;
        }
        catch
        {
            return new Dictionary<string, int>();
        }
    }

    public void Dispose()
    {
        _database?.Dispose();
    }
}

