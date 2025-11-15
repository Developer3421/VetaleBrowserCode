using System.Collections.Generic;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Search.Models;

namespace VetaleBrowser.VetaleBrowser.Search.Services;

/// <summary>
/// Інтерфейс сервісу пошукових підказок
/// </summary>
public interface ISuggestionsService
{
    /// <summary>
    /// Отримати підказки для запиту
    /// </summary>
    /// <param name="query">Пошуковий запит</param>
    /// <param name="maxResults">Максимальна кількість результатів</param>
    /// <returns>Список підказок</returns>
    Task<List<SearchSuggestion>> GetSuggestionsAsync(string query, int maxResults = 8);
}

