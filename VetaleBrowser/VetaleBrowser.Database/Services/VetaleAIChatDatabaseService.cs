using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using LiteDB;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Service for working with the Vetale AI chat history database
/// </summary>
public class VetaleAIChatDatabaseService : IVetaleAIChatDatabaseService, IDisposable
{
    private LiteDatabase? _database;
    private ILiteCollection<VetaleAIChatMessage>? _messageCollection;
    private readonly DatabaseEncryptionService _encryptionService;
    private readonly string _databasePath;
    private readonly object _lock = new object();
    private bool _disposed;
    private bool _isInitialized;
    
    // Optimized limits
    private const int MaxMessagesPerDatabase = 3000; // Less than browser history
    private const int DefaultPageSize = 50;
    private const long MaxDatabaseSizeBytes = 15 * 1024 * 1024; // 15 MB
    
    public VetaleAIChatDatabaseService(string databasePath, string encryptionKey)
    {
        _encryptionService = new DatabaseEncryptionService(encryptionKey);
        _databasePath = databasePath;
    }
    
    private void EnsureInitialized()
    {
        if (_isInitialized) return;
        
        lock (_lock)
        {
            if (_isInitialized) return;
            
            const int maxRetries = 3;
            const int retryDelayMs = 100;
            
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    var directory = Path.GetDirectoryName(_databasePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    
                    var connectionString = new ConnectionString
                    {
                        Filename = _databasePath,
                        Connection = ConnectionType.Shared,
                        ReadOnly = false,
                    };
                    
                    _database = new LiteDatabase(connectionString);
                    
                    try { _database.Checkpoint(); } catch { }
                    
                    _messageCollection = _database.GetCollection<VetaleAIChatMessage>("chat_messages");
                    
                    // Indexes for fast search
                    _messageCollection.EnsureIndex(x => x.CreatedAt);
                    _messageCollection.EnsureIndex(x => x.SessionId);
                    
                    _isInitialized = true;
                    
                    // Async size check
                    ThreadPool.QueueUserWorkItem(_ => CheckDatabaseSizeAsync());
                    
                    return;
                }
                catch (IOException ex) when (attempt < maxRetries - 1)
                {
                    Console.WriteLine($"[VetaleAIChatDB] Attempt {attempt + 1} failed, retrying: {ex.Message}");
                    Thread.Sleep(retryDelayMs * (attempt + 1));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error initializing VetaleAI chat database: {ex.Message}");
                    return;
                }
            }
        }
    }
    
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
                    if (dbFileInfo.Length > MaxDatabaseSizeBytes * 1.5)
                    {
                        _database?.Rebuild();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking VetaleAI chat database size: {ex.Message}");
        }
    }
    
    private void CleanupOldData()
    {
        if (_messageCollection == null) return;
        
        var totalMessages = _messageCollection.Count();
        if (totalMessages > MaxMessagesPerDatabase)
        {
            var messagesToDelete = totalMessages - MaxMessagesPerDatabase;
            var idsToDelete = _messageCollection
                .Query()
                .OrderBy(x => x.CreatedAt)
                .Limit(messagesToDelete)
                .ToList()
                .Select(x => x.Id)
                .ToList();
            
            foreach (var id in idsToDelete)
            {
                _messageCollection.Delete(id);
            }
        }
    }
    
    public void AddMessage(string role, string message, string sessionId, int? tokensUsed = null)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        
        EnsureInitialized();
        if (_messageCollection == null) return;
        
        try
        {
            var encryptedMessage = _encryptionService.EncryptString(message);
            
            lock (_lock)
            {
                var chatMessage = new VetaleAIChatMessage
                {
                    Role = role,
                    Message = encryptedMessage,
                    SessionId = sessionId,
                    CreatedAt = DateTime.UtcNow,
                    TokensUsed = tokensUsed
                };
                
                _messageCollection.Insert(chatMessage);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding chat message: {ex.Message}");
        }
    }
    
    public List<VetaleAIChatMessage> GetMessages(string? sessionId = null, int page = 0, int pageSize = DefaultPageSize)
    {
        EnsureInitialized();
        if (_messageCollection == null) return new List<VetaleAIChatMessage>();
        
        try
        {
            var query = _messageCollection.Query();
            
            if (!string.IsNullOrEmpty(sessionId))
                query = query.Where(x => x.SessionId == sessionId);
            
            var messages = query
                .OrderByDescending(x => x.CreatedAt)
                .Skip(page * pageSize)
                .Limit(pageSize)
                .ToList();
            
            DecryptMessages(messages);
            
            return messages;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting chat messages: {ex.Message}");
            return new List<VetaleAIChatMessage>();
        }
    }
    
    public List<VetaleAIChatMessage> GetLastMessages(int count, string? sessionId = null)
    {
        EnsureInitialized();
        if (_messageCollection == null) return new List<VetaleAIChatMessage>();
        
        try
        {
            var query = _messageCollection.Query();
            
            if (!string.IsNullOrEmpty(sessionId))
                query = query.Where(x => x.SessionId == sessionId);
            
            var messages = query
                .OrderByDescending(x => x.CreatedAt)
                .Limit(count)
                .ToList();
            
            DecryptMessages(messages);
            
            // Return in chronological order
            messages.Reverse();
            
            return messages;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting last messages: {ex.Message}");
            return new List<VetaleAIChatMessage>();
        }
    }
    
    public List<string> GetSessions()
    {
        EnsureInitialized();
        if (_messageCollection == null) return new List<string>();
        
        try
        {
            return _messageCollection
                .Query()
                .Select(x => x.SessionId)
                .ToList()
                .Distinct()
                .OrderByDescending(x => x)
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting sessions: {ex.Message}");
            return new List<string>();
        }
    }
    
    public void DeleteMessage(int id)
    {
        EnsureInitialized();
        if (_messageCollection == null) return;
        
        try
        {
            lock (_lock)
            {
                _messageCollection.Delete(id);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting message: {ex.Message}");
        }
    }
    
    public void ClearHistory()
    {
        EnsureInitialized();
        if (_messageCollection == null) return;
        
        try
        {
            lock (_lock)
            {
                _messageCollection.DeleteAll();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing chat history: {ex.Message}");
        }
    }
    
    public void ClearSession(string sessionId)
    {
        EnsureInitialized();
        if (_messageCollection == null) return;
        
        try
        {
            lock (_lock)
            {
                _messageCollection.DeleteMany(x => x.SessionId == sessionId);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing session: {ex.Message}");
        }
    }
    
    public void ClearHistoryOlderThan(DateTime date)
    {
        EnsureInitialized();
        if (_messageCollection == null) return;
        
        try
        {
            lock (_lock)
            {
                _messageCollection.DeleteMany(x => x.CreatedAt < date);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing old chat history: {ex.Message}");
        }
    }
    
    public List<VetaleAIChatMessage> SearchMessages(string query, int page = 0, int pageSize = DefaultPageSize)
    {
        if (string.IsNullOrWhiteSpace(query))
            return GetMessages(null, page, pageSize);
        
        EnsureInitialized();
        if (_messageCollection == null) return new List<VetaleAIChatMessage>();
        
        try
        {
            var allMessages = GetMessages(null, page, pageSize * 3);
            var searchTermLower = query.ToLower();
            
            return allMessages
                .Where(x => x.Message.ToLower().Contains(searchTermLower))
                .Take(pageSize)
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error searching messages: {ex.Message}");
            return new List<VetaleAIChatMessage>();
        }
    }
    
    private void DecryptMessages(List<VetaleAIChatMessage> messages)
    {
        foreach (var msg in messages)
        {
            try
            {
                msg.Message = _encryptionService.DecryptString(msg.Message);
            }
            catch
            {
                // If decryption failed, leave as is
            }
        }
    }
    
    public int GetMessageCount()
    {
        EnsureInitialized();
        return _messageCollection?.Count() ?? 0;
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        lock (_lock)
        {
            _database?.Dispose();
            _database = null;
            _messageCollection = null;
        }
    }
}

