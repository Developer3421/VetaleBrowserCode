using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Сервіс для роботи з ротацією баз даних історії чату Vetale AI
/// Автоматично створює нову БД при переповненні та читає з усіх послідовно
/// </summary>
public class RotatingVetaleAIChatDatabaseService : IVetaleAIChatDatabaseService
{
    private readonly string _baseDatabasePath;
    private readonly string _encryptionKey;
    private readonly List<VetaleAIChatDatabaseService> _databases = new();
    private VetaleAIChatDatabaseService _currentDatabase;
    private readonly object _lock = new object();
    private bool _disposed;
    
    // Параметри ротації для чату (менші ніж для історії браузера)
    private const long MaxDatabaseSizeBytes = 15 * 1024 * 1024; // 15 MB
    private const int MaxMessagesPerDatabase = 3000;
    private const int MaxDatabaseFiles = 10; // Максимальна кількість файлів БД
    
    public RotatingVetaleAIChatDatabaseService(string baseDatabasePath, string encryptionKey)
    {
        _baseDatabasePath = baseDatabasePath;
        _encryptionKey = encryptionKey;
        
        // Завантажуємо всі існуючі бази даних
        LoadExistingDatabases();
        
        // Встановлюємо поточну БД (остання або створюємо нову)
        _currentDatabase = _databases.LastOrDefault() ?? CreateNewDatabase();
    }
    
    /// <summary>
    /// Завантажує всі існуючі бази даних чату
    /// </summary>
    private void LoadExistingDatabases()
    {
        var directory = Path.GetDirectoryName(_baseDatabasePath);
        var baseFileName = Path.GetFileNameWithoutExtension(_baseDatabasePath);
        var extension = Path.GetExtension(_baseDatabasePath);
        
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return;
        
        // Шукаємо файли з патерном: vetale_chat.db, vetale_chat_1.db, vetale_chat_2.db, etc.
        var pattern = $"{baseFileName}*{extension}";
        var files = Directory.GetFiles(directory, pattern)
            .OrderBy(f => f)
            .ToList();
        
        foreach (var file in files)
        {
            try
            {
                var db = new VetaleAIChatDatabaseService(file, _encryptionKey);
                _databases.Add(db);
                Console.WriteLine($"[RotatingVetaleAIChat] Loaded database: {Path.GetFileName(file)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RotatingVetaleAIChat] Error loading database {file}: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Створює нову базу даних для ротації
    /// </summary>
    private VetaleAIChatDatabaseService CreateNewDatabase()
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
            
            var newDb = new VetaleAIChatDatabaseService(newDbPath, _encryptionKey);
            _databases.Add(newDb);
            
            Console.WriteLine($"[RotatingVetaleAIChat] Created new database: {Path.GetFileName(newDbPath)}");
            
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
                    Console.WriteLine($"[RotatingVetaleAIChat] Deleted old database: {Path.GetFileName(dbPath)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RotatingVetaleAIChat] Error deleting old database: {ex.Message}");
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
            var count = _currentDatabase.GetMessageCount();
            
            // Перевіряємо кількість повідомлень
            if (count >= MaxMessagesPerDatabase)
            {
                Console.WriteLine($"[RotatingVetaleAIChat] Rotation needed: {count} messages in current database");
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
                        Console.WriteLine($"[RotatingVetaleAIChat] Rotation needed: {fileInfo.Length / 1024 / 1024}MB database size");
                        _currentDatabase = CreateNewDatabase();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RotatingVetaleAIChat] Error checking database size: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Додає повідомлення в історію чату
    /// </summary>
    public void AddMessage(string role, string message, string sessionId, int? tokensUsed = null)
    {
        // Перевіряємо чи потрібна ротація перед додаванням
        CheckRotationNeeded();
        
        // Додаємо в поточну БД
        _currentDatabase.AddMessage(role, message, sessionId, tokensUsed);
    }
    
    /// <summary>
    /// Отримує повідомлення з усіх баз даних з пагінацією
    /// </summary>
    public List<VetaleAIChatMessage> GetMessages(string? sessionId = null, int page = 0, int pageSize = 100)
    {
        var allMessages = new List<VetaleAIChatMessage>();
        
        // Читаємо з усіх БД у зворотньому порядку (новіші першими)
        for (int i = _databases.Count - 1; i >= 0; i--)
        {
            try
            {
                var messages = _databases[i].GetMessages(sessionId, 0, int.MaxValue);
                allMessages.AddRange(messages);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RotatingVetaleAIChat] Error reading from database {i}: {ex.Message}");
            }
        }
        
        // Сортуємо по даті та застосовуємо пагінацію
        return allMessages
            .OrderByDescending(x => x.CreatedAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToList();
    }
    
    /// <summary>
    /// Отримує останні N повідомлень для контексту
    /// </summary>
    public List<VetaleAIChatMessage> GetLastMessages(int count, string? sessionId = null)
    {
        var allMessages = new List<VetaleAIChatMessage>();
        
        // Читаємо з усіх БД у зворотньому порядку
        for (int i = _databases.Count - 1; i >= 0; i--)
        {
            try
            {
                var messages = _databases[i].GetMessages(sessionId, 0, int.MaxValue);
                allMessages.AddRange(messages);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RotatingVetaleAIChat] Error reading from database {i}: {ex.Message}");
            }
        }
        
        // Сортуємо по даті та беремо останні
        var result = allMessages
            .OrderByDescending(x => x.CreatedAt)
            .Take(count)
            .OrderBy(x => x.CreatedAt) // Повертаємо в хронологічному порядку
            .ToList();
        
        return result;
    }
    
    /// <summary>
    /// Отримує всі сесії чату з усіх БД
    /// </summary>
    public List<string> GetSessions()
    {
        var allSessions = new HashSet<string>();
        
        foreach (var db in _databases)
        {
            try
            {
                var sessions = db.GetSessions();
                foreach (var session in sessions)
                {
                    allSessions.Add(session);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RotatingVetaleAIChat] Error getting sessions: {ex.Message}");
            }
        }
        
        return allSessions.OrderByDescending(x => x).ToList();
    }
    
    /// <summary>
    /// Видаляє повідомлення (шукає у всіх БД)
    /// </summary>
    public void DeleteMessage(int id)
    {
        foreach (var db in _databases)
        {
            try
            {
                db.DeleteMessage(id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RotatingVetaleAIChat] Error deleting message: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Очищає всю історію чату (у всіх БД)
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
                Console.WriteLine($"[RotatingVetaleAIChat] Error clearing history: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Очищує історію сесії (у всіх БД)
    /// </summary>
    public void ClearSession(string sessionId)
    {
        foreach (var db in _databases)
        {
            try
            {
                db.ClearSession(sessionId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RotatingVetaleAIChat] Error clearing session: {ex.Message}");
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
                Console.WriteLine($"[RotatingVetaleAIChat] Error clearing old history: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Пошук в історії чату (у всіх БД) з пагінацією
    /// </summary>
    public List<VetaleAIChatMessage> SearchMessages(string query, int page = 0, int pageSize = 100)
    {
        var allMessages = new List<VetaleAIChatMessage>();
        
        // Шукаємо у всіх БД
        for (int i = _databases.Count - 1; i >= 0; i--)
        {
            try
            {
                var messages = _databases[i].SearchMessages(query, 0, int.MaxValue);
                allMessages.AddRange(messages);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RotatingVetaleAIChat] Error searching in database {i}: {ex.Message}");
            }
        }
        
        // Сортуємо та застосовуємо пагінацію
        return allMessages
            .OrderByDescending(x => x.CreatedAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToList();
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
                Console.WriteLine($"[RotatingVetaleAIChat] Error disposing database: {ex.Message}");
            }
        }
        
        _databases.Clear();
    }
}

