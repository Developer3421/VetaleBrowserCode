using System.Collections.Generic;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Search.Models;

namespace VetaleBrowser.VetaleBrowser.Search.Services;

/// <summary>
/// Interface of search suggestions service
/// </summary>
public interface ISuggestionsService
{
    /// <summary>
    /// Get suggestions for query
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="maxResults">Maximum number of results</param>
    /// <returns>List of suggestions</returns>
    Task<List<SearchSuggestion>> GetSuggestionsAsync(string query, int maxResults = 8);
}

