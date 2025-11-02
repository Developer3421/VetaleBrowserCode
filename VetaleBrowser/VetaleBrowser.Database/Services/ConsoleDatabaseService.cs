using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Сервіс для роботи з базою даних консолі з AES шифруванням
/// </summary>
public class ConsoleDatabaseService : IDisposable
{
    private readonly LiteDatabase _database;
    private readonly DatabaseEncryptionService _encryptionService;
    private readonly ILiteCollection<ConsoleLogItem> _logsCollection;
    private readonly string _databasePath;
    
    // Обмеження для запобігання переповнення
    private const int MaxLogItems = 50000;
    private const long MaxDatabaseSizeBytes = 100 * 1024 * 1024; // 100 MB

    public ConsoleDatabaseService(string databasePath, string encryptionKey)
    {
        _encryptionService = new DatabaseEncryptionService(encryptionKey);
        _databasePath = databasePath;
        
        // Створюємо директорію для бази даних якщо не існує
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Ініціалізуємо базу даних з шифруванням
        var connectionString = new ConnectionString
        {
            Filename = databasePath,
            Connection = ConnectionType.Shared
        };

        _database = new LiteDatabase(connectionString);
        
        // Отримуємо колекцію
        _logsCollection = _database.GetCollection<ConsoleLogItem>("console_logs");
        
        // Створюємо індекси для оптимізації
        _logsCollection.EnsureIndex(x => x.Timestamp);
        _logsCollection.EnsureIndex(x => x.Level);
        _logsCollection.EnsureIndex(x => x.Source);
        
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
            // Видаляємо старі записи
            CleanupOldData();
            
            // Оптимізуємо базу даних
            _database.Rebuild();
        }
    }

    /// <summary>
    /// Очищає старі дані з бази
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
    /// Додає запис в консоль
    /// </summary>
    public void AddLog(string level, string message, string? source = null, string? stackTrace = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            // Шифруємо чутливі дані
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
            
            // Перевіряємо чи не перевищено ліміт
            CheckDatabaseSize();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConsoleDatabaseService] Error adding log: {ex.Message}");
        }
    }

    /// <summary>
    /// Отримує всі логи або з фільтром
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

            // Розшифровуємо дані
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
                    // Якщо не вдалося розшифрувати, залишаємо як є
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
    /// Видаляє запис з консолі
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
    /// Очищає всі логи
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
    /// Очищає логи старше вказаної дати
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
    /// Пошук в логах
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
    /// Отримує кількість логів за рівнем
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

