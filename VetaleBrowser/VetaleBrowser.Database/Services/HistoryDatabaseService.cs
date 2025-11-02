using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Сервіс для роботи з базою даних історії переглядів
/// </summary>
public class HistoryDatabaseService : IHistoryDatabaseService, IDisposable
{
    private readonly LiteDatabase _database;
    private readonly DatabaseEncryptionService _encryptionService;
    private readonly ILiteCollection<HistoryItem> _historyCollection;
    private readonly string _databasePath;
    
    // Обмеження для запобігання переповнення
    private const int MaxHistoryItems = 100000;
    private const long MaxDatabaseSizeBytes = 500 * 1024 * 1024; // 500 MB

    public HistoryDatabaseService(string databasePath, string encryptionKey)
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
        _historyCollection = _database.GetCollection<HistoryItem>("history");
        
        // Створюємо індекси для оптимізації
        _historyCollection.EnsureIndex(x => x.Url);
        _historyCollection.EnsureIndex(x => x.VisitedAt);
        _historyCollection.EnsureIndex(x => x.Title);
        
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
        var totalItems = _historyCollection.Count();
        if (totalItems > MaxHistoryItems)
        {
            var itemsToDelete = totalItems - MaxHistoryItems;
            var oldItems = _historyCollection
                .Query()
                .OrderBy(x => x.VisitedAt)
                .Limit(itemsToDelete)
                .ToList();

            foreach (var item in oldItems)
            {
                _historyCollection.Delete(item.Id);
            }
        }
    }

    /// <summary>
    /// Додає або оновлює запис в історії
    /// </summary>
    public void AddOrUpdateHistoryItem(string url, string title, string? faviconUrl = null, byte[]? faviconData = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            // Шифруємо чутливі дані
            var encryptedUrl = _encryptionService.EncryptString(url);
            var encryptedTitle = !string.IsNullOrWhiteSpace(title) 
                ? _encryptionService.EncryptString(title) 
                : _encryptionService.EncryptString(url);

            // Перевіряємо чи існує запис з таким URL
            var existing = _historyCollection.FindOne(x => x.Url == encryptedUrl);
            
            if (existing != null)
            {
                // Оновлюємо існуючий запис
                existing.Title = encryptedTitle;
                existing.VisitedAt = DateTime.UtcNow;
                existing.VisitCount++;
                
                if (faviconUrl != null)
                    existing.FaviconUrl = faviconUrl;
                if (faviconData != null)
                    existing.FaviconData = faviconData;
                
                _historyCollection.Update(existing);
            }
            else
            {
                // Створюємо новий запис
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
            }
            
            // Перевіряємо чи не перевищено ліміт
            CheckDatabaseSize();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding/updating history item: {ex.Message}");
        }
    }

    /// <summary>
    /// Отримує всю історію або з фільтром по даті
    /// </summary>
    public List<HistoryItem> GetHistory(DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var query = _historyCollection.Query();

            if (startDate.HasValue)
                query = query.Where(x => x.VisitedAt >= startDate.Value);
            
            if (endDate.HasValue)
                query = query.Where(x => x.VisitedAt <= endDate.Value);

            var items = query.OrderByDescending(x => x.VisitedAt).ToList();

            // Розшифровуємо дані
            foreach (var item in items)
            {
                try
                {
                    item.Url = _encryptionService.DecryptString(item.Url);
                    item.Title = _encryptionService.DecryptString(item.Title);
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
            Console.WriteLine($"Error getting history: {ex.Message}");
            return new List<HistoryItem>();
        }
    }

    /// <summary>
    /// Видаляє запис з історії
    /// </summary>
    public void DeleteHistoryItem(int id)
    {
        try
        {
            _historyCollection.Delete(id);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting history item: {ex.Message}");
        }
    }

    /// <summary>
    /// Очищає всю історію
    /// </summary>
    public void ClearHistory()
    {
        try
        {
            _historyCollection.DeleteAll();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing history: {ex.Message}");
        }
    }

    /// <summary>
    /// Очищує історію старше вказаної дати
    /// </summary>
    public void ClearHistoryOlderThan(DateTime date)
    {
        try
        {
            var itemsToDelete = _historyCollection
                .Query()
                .Where(x => x.VisitedAt < date)
                .ToList();

            foreach (var item in itemsToDelete)
            {
                _historyCollection.Delete(item.Id);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing old history: {ex.Message}");
        }
    }

    /// <summary>
    /// Пошук в історії
    /// </summary>
    public List<HistoryItem> SearchHistory(string query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
                return GetHistory();

            var allItems = GetHistory();
            var searchTermLower = query.ToLower();

            return allItems
                .Where(x => 
                    x.Url.ToLower().Contains(searchTermLower) || 
                    x.Title.ToLower().Contains(searchTermLower))
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error searching history: {ex.Message}");
            return new List<HistoryItem>();
        }
    }

    public void Dispose()
    {
        _database?.Dispose();
    }
}

