using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Інтерфейс для роботи з локальним пошуковим індексом Vetale Search
/// </summary>
public interface ISearchIndexService
{
    /// <summary>
    /// Індексувати сторінку для пошуку
    /// </summary>
    Task<bool> IndexPageAsync(string url, string title, string content, string description, string keywords);

    /// <summary>
    /// Оновити існуючий індекс сторінки
    /// </summary>
    Task<bool> UpdateIndexAsync(string url, string title, string content, string description, string keywords);

    /// <summary>
    /// Видалити сторінку з індексу
    /// </summary>
    Task<bool> RemoveFromIndexAsync(string url);

    /// <summary>
    /// Пошук в локальному індексі
    /// </summary>
    Task<List<SearchIndex>> SearchAsync(string query, int maxResults = 50);

    /// <summary>
    /// Зберегти пошуковий запит в історію
    /// </summary>
    Task<bool> SaveSearchQueryAsync(string query, string searchEngine, int resultsCount);

    /// <summary>
    /// Отримати історію пошукових запитів
    /// </summary>
    Task<List<SearchQuery>> GetSearchHistoryAsync(int limit = 100);

    /// <summary>
    /// Очистити історію пошуків
    /// </summary>
    Task<bool> ClearSearchHistoryAsync();

    /// <summary>
    /// Отримати популярні пошукові запити
    /// </summary>
    Task<List<string>> GetPopularQueriesAsync(int limit = 10);

    /// <summary>
    /// Автодоповнення для пошукового запиту
    /// </summary>
    Task<List<string>> GetAutocompleteSuggestionsAsync(string partialQuery, int limit = 10);

    /// <summary>
    /// Очистити весь пошуковий індекс
    /// </summary>
    Task<bool> ClearIndexAsync();

    /// <summary>
    /// Отримати статистику індексу
    /// </summary>
    Task<(int TotalPages, int TotalKeywords, DateTime? LastIndexed)> GetIndexStatisticsAsync();
}

