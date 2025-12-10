using System;
using System.Collections.Generic;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Інтерфейс сервісу бази даних історії переглядів
/// MEMORY OPTIMIZED: Added pagination support
/// </summary>
public interface IHistoryDatabaseService : IDisposable
{
    /// <summary>
    /// Додає або оновлює запис в історії
    /// </summary>
    void AddOrUpdateHistoryItem(string url, string title, string? faviconUrl = null, byte[]? faviconData = null);
    
    /// <summary>
    /// Отримує історію з пагінацією
    /// </summary>
    List<HistoryItem> GetHistory(DateTime? startDate = null, DateTime? endDate = null, int page = 0, int pageSize = 100);
    
    /// <summary>
    /// Отримує історію (перша сторінка для сумісності)
    /// </summary>
    List<HistoryItem> GetHistory(DateTime? startDate = null, DateTime? endDate = null);
    
    /// <summary>
    /// Видаляє запис з історії
    /// </summary>
    void DeleteHistoryItem(int id);
    
    /// <summary>
    /// Очищає всю історію
    /// </summary>
    void ClearHistory();
    
    /// <summary>
    /// Очищує історію старше вказаної дати
    /// </summary>
    void ClearHistoryOlderThan(DateTime date);
    
    /// <summary>
    /// Пошук в історії з пагінацією
    /// </summary>
    List<HistoryItem> SearchHistory(string query, int page = 0, int pageSize = 100);
    
    /// <summary>
    /// Пошук в історії (перша сторінка для сумісності)
    /// </summary>
    List<HistoryItem> SearchHistory(string query);
    
    /// <summary>
    /// Отримує загальну кількість записів в історії
    /// </summary>
    int GetHistoryCount();
}

