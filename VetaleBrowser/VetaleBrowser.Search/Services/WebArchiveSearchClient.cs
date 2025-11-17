using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Search.Models;

namespace VetaleBrowser.VetaleBrowser.Search.Services;

public interface IWebArchiveSearchClient
{
    Task<IReadOnlyList<UnifiedSearchResult>> SearchTodayAsync(string query, int maxResults, CancellationToken ct = default);
}

/// <summary>
/// Клієнт для пошуку в Web Archive (Internet Archive).
/// Створює пошуковий URL для запиту користувача, аналогічно до Google/YouTube.
/// </summary>
public sealed class WebArchiveSearchClient : IWebArchiveSearchClient, IDisposable
{
    private readonly HttpClient _http;

    public WebArchiveSearchClient(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        if (!_http.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "VetaleBrowser/1.0 (WebArchiveSearchClient)");
        }
    }

    public async Task<IReadOnlyList<UnifiedSearchResult>> SearchTodayAsync(string query, int maxResults, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<UnifiedSearchResult>();

        // Створюємо URL для пошуку в Web Archive (аналогічно до Google/YouTube)
        var webArchiveSearchUrl = $"https://web.archive.org/web/*/{Uri.EscapeDataString(query)}";
        
        // Повертаємо один результат-посилання на пошук у Web Archive
        var results = new List<UnifiedSearchResult>
        {
            new UnifiedSearchResult
            {
                Title = $"Пошук в Web Archive: \"{query}\"",
                Url = webArchiveSearchUrl,
                DisplayUrl = "web.archive.org › search",
                Snippet = $"Відкрити архівні знімки сайтів для \"{query}\" в Internet Archive.",
                Source = SearchSourceType.WebArchive,
                Timestamp = null,
                RankScore = 1,
                PageNumber = 1
            }
        };

        await Task.CompletedTask; // Для сумісності з async
        return results;
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}

