using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Interface for working with the local Vetale Search index
/// </summary>
public interface ISearchIndexService
{
    /// <summary>
    /// Index a page for search
    /// </summary>
    Task<bool> IndexPageAsync(string url, string title, string content, string description, string keywords);

    /// <summary>
    /// Update an existing page index
    /// </summary>
    Task<bool> UpdateIndexAsync(string url, string title, string content, string description, string keywords);

    /// <summary>
    /// Remove a page from the index
    /// </summary>
    Task<bool> RemoveFromIndexAsync(string url);

    /// <summary>
    /// Search in the local index
    /// </summary>
    Task<List<SearchIndex>> SearchAsync(string query, int maxResults = 50);

    /// <summary>
    /// Save a search query to history
    /// </summary>
    Task<bool> SaveSearchQueryAsync(string query, string searchEngine, int resultsCount);

    /// <summary>
    /// Get the search query history
    /// </summary>
    Task<List<SearchQuery>> GetSearchHistoryAsync(int limit = 100);

    /// <summary>
    /// Clear the search history
    /// </summary>
    Task<bool> ClearSearchHistoryAsync();

    /// <summary>
    /// Get popular search queries
    /// </summary>
    Task<List<string>> GetPopularQueriesAsync(int limit = 10);

    /// <summary>
    /// Autocomplete suggestions for a search query
    /// </summary>
    Task<List<string>> GetAutocompleteSuggestionsAsync(string partialQuery, int limit = 10);

    /// <summary>
    /// Clear the entire search index
    /// </summary>
    Task<bool> ClearIndexAsync();

    /// <summary>
    /// Get index statistics
    /// </summary>
    Task<(int TotalPages, int TotalKeywords, DateTime? LastIndexed)> GetIndexStatisticsAsync();
}

