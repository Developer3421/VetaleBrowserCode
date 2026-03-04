using System;
using System.Collections.Generic;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Interface for the browsing history database service
/// MEMORY OPTIMIZED: Added pagination support
/// </summary>
public interface IHistoryDatabaseService : IDisposable
{
    /// <summary>
    /// Adds or updates a record in the history
    /// </summary>
    void AddOrUpdateHistoryItem(string url, string title, string? faviconUrl = null, byte[]? faviconData = null);
    
    /// <summary>
    /// Gets history with pagination
    /// </summary>
    List<HistoryItem> GetHistory(DateTime? startDate = null, DateTime? endDate = null, int page = 0, int pageSize = 100);
    
    /// <summary>
    /// Gets history (first page for compatibility)
    /// </summary>
    List<HistoryItem> GetHistory(DateTime? startDate = null, DateTime? endDate = null);
    
    /// <summary>
    /// Deletes a record from the history
    /// </summary>
    void DeleteHistoryItem(int id);
    
    /// <summary>
    /// Clears the entire history
    /// </summary>
    void ClearHistory();
    
    /// <summary>
    /// Clears history older than the specified date
    /// </summary>
    void ClearHistoryOlderThan(DateTime date);
    
    /// <summary>
    /// Searches in history with pagination
    /// </summary>
    List<HistoryItem> SearchHistory(string query, int page = 0, int pageSize = 100);
    
    /// <summary>
    /// Searches in history (first page for compatibility)
    /// </summary>
    List<HistoryItem> SearchHistory(string query);
    
    /// <summary>
    /// Gets the total number of records in the history
    /// </summary>
    int GetHistoryCount();
}

