using System;
using System.Collections.Generic;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Інтерфейс сервісу для роботи з історією чату Vetale AI
/// </summary>
public interface IVetaleAIChatDatabaseService : IDisposable
{
    /// <summary>
    /// Додає повідомлення в історію чату
    /// </summary>
    void AddMessage(string role, string message, string sessionId, int? tokensUsed = null);
    
    /// <summary>
    /// Отримує повідомлення з пагінацією
    /// </summary>
    List<VetaleAIChatMessage> GetMessages(string? sessionId = null, int page = 0, int pageSize = 100);
    
    /// <summary>
    /// Отримує останні N повідомлень для контексту
    /// </summary>
    List<VetaleAIChatMessage> GetLastMessages(int count, string? sessionId = null);
    
    /// <summary>
    /// Отримує всі сесії чату
    /// </summary>
    List<string> GetSessions();
    
    /// <summary>
    /// Видаляє повідомлення
    /// </summary>
    void DeleteMessage(int id);
    
    /// <summary>
    /// Очищає всю історію чату
    /// </summary>
    void ClearHistory();
    
    /// <summary>
    /// Очищує історію сесії
    /// </summary>
    void ClearSession(string sessionId);
    
    /// <summary>
    /// Очищує історію старше вказаної дати
    /// </summary>
    void ClearHistoryOlderThan(DateTime date);
    
    /// <summary>
    /// Пошук в історії чату
    /// </summary>
    List<VetaleAIChatMessage> SearchMessages(string query, int page = 0, int pageSize = 100);
}

