using System;
using System.Collections.Generic;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Інтерфейс сервісу бази даних історії переглядів
/// </summary>
public interface IHistoryDatabaseService
{
    /// <summary>
    /// Додає або оновлює запис в історії
    /// </summary>
    void AddOrUpdateHistoryItem(string url, string title, string? faviconUrl = null, byte[]? faviconData = null);
    
    /// <summary>
    /// Отримує всю історію або з фільтром по даті
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
    /// Пошук в історії
    /// </summary>
    List<HistoryItem> SearchHistory(string query);
}

