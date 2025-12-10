using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Search.Models;

namespace VetaleBrowser.VetaleBrowser.Search.Services;

/// <summary>
/// Сервіс отримання підказок від Google Suggest API
/// </summary>
public class GoogleSuggestionsService : ISuggestionsService
{
    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(3)
    };

    /// <summary>
    /// Отримати підказки від Google
    /// </summary>
    public async Task<List<SearchSuggestion>> GetSuggestionsAsync(string query, int maxResults = 8)
    {
        var suggestions = new List<SearchSuggestion>();

        if (string.IsNullOrWhiteSpace(query))
            return suggestions;

        try
        {
            // Google Suggest API endpoint
            var url = $"http://suggestqueries.google.com/complete/search?client=firefox&q={Uri.EscapeDataString(query)}";

            var response = await _httpClient.GetStringAsync(url);

            // Парсимо JSON відповідь
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            // Формат відповіді: [query, [suggestions...]]
            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() >= 2)
            {
                var suggestionsArray = root[1];
                if (suggestionsArray.ValueKind == JsonValueKind.Array)
                {
                    var count = 0;
                    foreach (var item in suggestionsArray.EnumerateArray())
                    {
                        if (count >= maxResults)
                            break;

                        var text = item.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            suggestions.Add(new SearchSuggestion
                            {
                                Text = text,
                                Type = "search",
                                Icon = "🔍"
                            });
                            count++;
                        }
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[GoogleSuggestionsService] Got {suggestions.Count} suggestions for '{query}'");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GoogleSuggestionsService] Error: {ex.Message}");
        }

        return suggestions;
    }
}

