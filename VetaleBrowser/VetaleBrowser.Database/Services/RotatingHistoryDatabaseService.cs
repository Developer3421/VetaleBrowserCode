using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Сервіс для роботи з ротацією баз даних історії
/// Автоматично створює нову БД при переповненні та читає з усіх послідовно
/// </summary>
public class RotatingHistoryDatabaseService : IHistoryDatabaseService
{
    private readonly string _baseDatabasePath;
    private readonly string _encryptionKey;
    private readonly List<HistoryDatabaseService> _databases = new();
    private HistoryDatabaseService _currentDatabase;
    private readonly object _lock = new object();
    private bool _disposed;
    
    // Параметри ротації
    private const long MaxDatabaseSizeBytes = 20 * 1024 * 1024; // 20 MB
    private const int MaxHistoryItemsPerDatabase = 5000;
    private const int MaxDatabaseFiles = 10; // Максимальна кількість файлів БД
    
    public RotatingHistoryDatabaseService(string baseDatabasePath, string encryptionKey)
    {
        _baseDatabasePath = baseDatabasePath;
        _encryptionKey = encryptionKey;
        
        // Завантажуємо всі існуючі бази даних
        LoadExistingDatabases();
        
        // Встановлюємо поточну БД (остання або створюємо нову)
        _currentDatabase = _databases.LastOrDefault() ?? CreateNewDatabase();
    }
    
    /// <summary>
    /// Завантажує всі існуючі бази даних історії
    /// </summary>
    private void LoadExistingDatabases()
    {
        var directory = Path.GetDirectoryName(_baseDatabasePath);
        var baseFileName = Path.GetFileNameWithoutExtension(_baseDatabasePath);
        var extension = Path.GetExtension(_baseDatabasePath);
        
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return;
        
        // Шукаємо файли з патерном: history.db, history_1.db, history_2.db, etc.
        var pattern = $"{baseFileName}*.{extension}";
        var files = Directory.GetFiles(directory, pattern)
            .OrderBy(f => f) // Сортуємо за назвою
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
    /// Створює нову базу даних для ротації
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
                // Перша БД - використовуємо оригінальний шлях
                newDbPath = _baseDatabasePath;
            }
            else
            {
                // Нова БД з індексом
                newDbPath = Path.Combine(directory!, $"{baseFileName}_{_databases.Count}{extension}");
            }
            
            var newDb = new HistoryDatabaseService(newDbPath, _encryptionKey);
            _databases.Add(newDb);
            
            Console.WriteLine($"[RotatingHistory] Created new database: {Path.GetFileName(newDbPath)}");
            
            // Перевіряємо чи не перевищено ліміт файлів БД
            CleanupOldDatabasesIfNeeded();
            
            return newDb;
        }
    }
    
    /// <summary>
    /// Видаляє найстаріші БД якщо перевищено ліміт
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
    /// Перевіряє чи потрібно створити нову БД для ротації
    /// </summary>
    private void CheckRotationNeeded()
    {
        lock (_lock)
        {
            var count = _currentDatabase.GetHistoryCount();
            
            // Перевіряємо кількість записів
            if (count >= MaxHistoryItemsPerDatabase)
            {
                Console.WriteLine($"[RotatingHistory] Rotation needed: {count} items in current database");
                _currentDatabase = CreateNewDatabase();
                return;
            }
            
            // Перевіряємо розмір файлу
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
    /// Додає або оновлює запис в історії
    /// </summary>
    public void AddOrUpdateHistoryItem(string url, string title, string? faviconUrl = null, byte[]? faviconData = null)
    {
        // Перевіряємо чи потрібна ротація перед додаванням
        CheckRotationNeeded();
        
        // Додаємо в поточну БД
        _currentDatabase.AddOrUpdateHistoryItem(url, title, faviconUrl, faviconData);
    }
    
    /// <summary>
    /// Отримує історію з усіх баз даних з пагінацією
    /// </summary>
    public List<HistoryItem> GetHistory(DateTime? startDate = null, DateTime? endDate = null, int page = 0, int pageSize = 100)
    {
        var allItems = new List<HistoryItem>();
        
        // Читаємо з усіх БД у зворотньому порядку (новіші першими)
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
        
        // Сортуємо по даті та застосовуємо пагінацію
        return allItems
            .OrderByDescending(x => x.VisitedAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToList();
    }
    
    /// <summary>
    /// Overload для сумісності
    /// </summary>
    public List<HistoryItem> GetHistory(DateTime? startDate = null, DateTime? endDate = null)
    {
        return GetHistory(startDate, endDate, 0, 100);
    }
    
    /// <summary>
    /// Видаляє запис з історії (шукає у всіх БД)
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
    /// Очищає всю історію (у всіх БД)
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
    /// Очищує історію старше вказаної дати (у всіх БД)
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
    /// Пошук в історії (у всіх БД) з пагінацією
    /// </summary>
    public List<HistoryItem> SearchHistory(string query, int page = 0, int pageSize = 100)
    {
        var allItems = new List<HistoryItem>();
        
        // Шукаємо у всіх БД
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
        
        // Сортуємо та застосовуємо пагінацію
        return allItems
            .OrderByDescending(x => x.VisitedAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToList();
    }
    
    /// <summary>
    /// Overload для сумісності
    /// </summary>
    public List<HistoryItem> SearchHistory(string query)
    {
        return SearchHistory(query, 0, 100);
    }
    
    /// <summary>
    /// Отримує загальну кількість записів в історії (з усіх БД)
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

