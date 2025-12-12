using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Search.Models;

namespace VetaleBrowser.VetaleBrowser.Search.Services;

/// <summary>
/// Інтерфейс для пошуку через MetaSearx (відкрита метапошукова система)
/// </summary>
public interface IMetaSearxSearchClient
{
    /// <summary>
    /// Шукає результати через MetaSearx API з пагінацією
    /// </summary>
    /// <param name="query">Пошуковий запит</param>
    /// <param name="page">Номер сторінки (1-based)</param>
    /// <param name="resultsPerPage">Кількість результатів на сторінку (за замовчуванням 50)</param>
    /// <param name="ct">Токен скасування</param>
    Task<MetaSearxSearchResult> SearchAsync(string query, int page = 1, int resultsPerPage = 50, CancellationToken ct = default);
    
    /// <summary>
    /// Простий пошук без пагінації (для сумісності)
    /// </summary>
    Task<List<UnifiedSearchResult>> SearchAsync(string query, int maxResults = 5, CancellationToken ct = default);
}

/// <summary>
/// Результат пошуку MetaSearx з інформацією про пагінацію
/// </summary>
public sealed class MetaSearxSearchResult
{
    /// <summary>
    /// Результати пошуку на поточній сторінці
    /// </summary>
    public List<UnifiedSearchResult> Results { get; set; } = new();
    
    /// <summary>
    /// Поточна сторінка (1-based)
    /// </summary>
    public int CurrentPage { get; set; } = 1;
    
    /// <summary>
    /// Кількість результатів на сторінку
    /// </summary>
    public int ResultsPerPage { get; set; } = 50;
    
    /// <summary>
    /// Чи є наступна сторінка
    /// </summary>
    public bool HasNextPage { get; set; }
    
    /// <summary>
    /// Чи є попередня сторінка
    /// </summary>
    public bool HasPreviousPage { get; set; }
    
    /// <summary>
    /// Загальна кількість результатів (якщо відома)
    /// </summary>
    public int? TotalResults { get; set; }
    
    /// <summary>
    /// Загальна кількість сторінок (якщо відома)
    /// </summary>
    public int? TotalPages { get; set; }
    
    /// <summary>
    /// Пошуковий запит
    /// </summary>
    public string Query { get; set; } = string.Empty;
}

/// <summary>
/// Клієнт для пошуку через MetaSearx
/// MetaSearx - це метапошукова система, що агрегує результати з багатьох пошукових систем
/// </summary>
public sealed class MetaSearxSearchClient : IMetaSearxSearchClient
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    // Публічний інстанс MetaSearx
    private const string BaseUrl = "https://metasearx.com";
    private const string SearchApiUrl = "https://metasearx.com/search";
    
    // Кількість результатів на одну сторінку MetaSearx API (зазвичай ~10-20)
    private const int MetaSearxResultsPerPage = 10;

    /// <summary>
    /// Пошук з повною пагінацією (професійний режим)
    /// </summary>
    public async Task<MetaSearxSearchResult> SearchAsync(string query, int page = 1, int resultsPerPage = 50, CancellationToken ct = default)
    {
        var result = new MetaSearxSearchResult
        {
            Query = query,
            CurrentPage = page,
            ResultsPerPage = resultsPerPage,
            HasPreviousPage = page > 1
        };

        if (string.IsNullOrWhiteSpace(query))
            return result;

        try
        {
            // Розраховуємо які сторінки MetaSearx потрібно завантажити
            // Наприклад: page=1, resultsPerPage=50 -> MetaSearx pages 1-5
            // page=2, resultsPerPage=50 -> MetaSearx pages 6-10
            var metaSearxPagesPerOurPage = (int)Math.Ceiling(resultsPerPage / (double)MetaSearxResultsPerPage);
            var startMetaSearxPage = (page - 1) * metaSearxPagesPerOurPage + 1;
            var endMetaSearxPage = startMetaSearxPage + metaSearxPagesPerOurPage - 1;
            
            System.Diagnostics.Debug.WriteLine($"[MetaSearx] Loading pages {startMetaSearxPage}-{endMetaSearxPage} for user page {page}");
            
            var allResults = new List<UnifiedSearchResult>();
            var hasMoreResults = false;
            
            for (int metaPage = startMetaSearxPage; metaPage <= endMetaSearxPage; metaPage++)
            {
                if (ct.IsCancellationRequested)
                    break;
                
                var (pageResults, hasMore) = await FetchPageWithInfoAsync(query, metaPage, ct);
                
                foreach (var r in pageResults)
                {
                    // Уникаємо дублікатів
                    if (!allResults.Exists(x => x.Url.Equals(r.Url, StringComparison.OrdinalIgnoreCase)))
                    {
                        r.PageNumber = page;
                        allResults.Add(r);
                    }
                }
                
                hasMoreResults = hasMore;
                
                // Затримка між запитами
                if (metaPage < endMetaSearxPage)
                {
                    await Task.Delay(50, ct);
                }
            }
            
            // Обмежуємо до resultsPerPage
            result.Results = allResults.Count > resultsPerPage 
                ? allResults.GetRange(0, resultsPerPage) 
                : allResults;
            
            result.HasNextPage = hasMoreResults || allResults.Count >= resultsPerPage;
            
            // Оновлюємо RankScore для правильного сортування
            for (int i = 0; i < result.Results.Count; i++)
            {
                result.Results[i].RankScore = resultsPerPage - i;
            }
            
            System.Diagnostics.Debug.WriteLine($"[MetaSearx] Page {page}: {result.Results.Count} results, hasNext={result.HasNextPage}");
        }
        catch (OperationCanceledException)
        {
            // Нормальне скасування
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MetaSearx] Error: {ex.Message}");
            
            if (result.Results.Count == 0)
            {
                result.Results.Add(CreateMetaSearxSearchLink(query));
            }
        }

        if (result.Results.Count == 0)
        {
            result.Results.Add(CreateMetaSearxSearchLink(query));
            result.HasNextPage = false;
        }

        return result;
    }

    /// <summary>
    /// Простий пошук без пагінації (для сумісності з існуючим кодом)
    /// </summary>
    public async Task<List<UnifiedSearchResult>> SearchAsync(string query, int maxResults = 5, CancellationToken ct = default)
    {
        var searchResult = await SearchAsync(query, page: 1, resultsPerPage: maxResults, ct);
        return searchResult.Results;
    }
    
    /// <summary>
    /// Завантажує одну сторінку результатів з інформацією про наявність наступних
    /// </summary>
    private async Task<(List<UnifiedSearchResult> Results, bool HasMore)> FetchPageWithInfoAsync(string query, int pageNumber, CancellationToken ct)
    {
        var results = new List<UnifiedSearchResult>();
        var hasMore = false;
        
        try
        {
            var searchUrl = $"{SearchApiUrl}?q={Uri.EscapeDataString(query)}&format=json&language=uk-UA&pageno={pageNumber}";
            
            var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            request.Headers.Add("User-Agent", "VetaleBrowser/1.0 (Educational Browser)");
            request.Headers.Add("Accept", "application/json, text/html");
            
            System.Diagnostics.Debug.WriteLine($"[MetaSearx] Fetching MetaSearx page {pageNumber}");
            
            var response = await _httpClient.SendAsync(request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[MetaSearx] HTTP error: {response.StatusCode}");
                return (results, false);
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            var content = await response.Content.ReadAsStringAsync(ct);
            
            if (contentType.Contains("json"))
            {
                (results, hasMore) = ParseJsonResponseWithInfo(content);
            }
            else
            {
                results = ParseHtmlResponse(content, query, 100);
                hasMore = results.Count >= 10;
            }
            
            System.Diagnostics.Debug.WriteLine($"[MetaSearx] MetaSearx page {pageNumber}: {results.Count} results");
        }
        catch (OperationCanceledException)
        {
            // Нормальне скасування
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MetaSearx] FetchPage error: {ex.Message}");
        }

        return (results, hasMore);
    }

    /// <summary>
    /// Парсить JSON відповідь з інформацією про пагінацію
    /// </summary>
    private (List<UnifiedSearchResult> Results, bool HasMore) ParseJsonResponseWithInfo(string json)
    {
        var results = new List<UnifiedSearchResult>();
        var hasMore = false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Перевіряємо чи є інформація про пагінацію
            if (root.TryGetProperty("number_of_results", out var totalProp))
            {
                hasMore = totalProp.TryGetInt64(out var total) && total > 0;
            }

            if (root.TryGetProperty("results", out var resultsArray) && 
                resultsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in resultsArray.EnumerateArray())
                {
                    try
                    {
                        var url = item.TryGetProperty("url", out var urlProp) 
                            ? urlProp.GetString() ?? "" 
                            : "";
                        var title = item.TryGetProperty("title", out var titleProp) 
                            ? titleProp.GetString() ?? "" 
                            : "";
                        var content = item.TryGetProperty("content", out var contentProp) 
                            ? contentProp.GetString() ?? "" 
                            : "";
                        var engine = item.TryGetProperty("engine", out var engineProp) 
                            ? engineProp.GetString() ?? "MetaSearx" 
                            : "MetaSearx";

                        if (string.IsNullOrWhiteSpace(url))
                            continue;

                        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                            continue;

                        results.Add(new UnifiedSearchResult
                        {
                            Title = string.IsNullOrWhiteSpace(title) ? uri.Host : title,
                            Url = url,
                            DisplayUrl = $"{uri.Host} › {engine}",
                            Snippet = string.IsNullOrWhiteSpace(content) 
                                ? $"Результат з {engine}"
                                : TruncateText(content, 200),
                            Source = SearchSourceType.MetaSearx,
                            Timestamp = null,
                            RankScore = 0,
                            PageNumber = 1
                        });
                    }
                    catch
                    {
                        // Пропускаємо некоректний елемент
                    }
                }
                
                // Якщо отримали результати, ймовірно є ще
                hasMore = hasMore || results.Count >= 10;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MetaSearx] JSON parse error: {ex.Message}");
        }

        return (results, hasMore);
    }

    /// <summary>
    /// Парсить HTML відповідь від MetaSearx (fallback)
    /// </summary>
    private List<UnifiedSearchResult> ParseHtmlResponse(string html, string query, int maxResults)
    {
        var results = new List<UnifiedSearchResult>();

        try
        {
            var searchStart = 0;
            var count = 0;

            // Шукаємо результати в HTML (клас result або article)
            while (count < maxResults)
            {
                // Шукаємо посилання на результати
                var resultStart = html.IndexOf("class=\"result\"", searchStart, StringComparison.OrdinalIgnoreCase);
                if (resultStart < 0)
                {
                    resultStart = html.IndexOf("class=\"result_header\"", searchStart, StringComparison.OrdinalIgnoreCase);
                    if (resultStart < 0)
                        break;
                }

                // Знаходимо href
                var linkStart = html.IndexOf("href=\"", resultStart, StringComparison.OrdinalIgnoreCase);
                if (linkStart < 0 || linkStart > resultStart + 1000)
                {
                    searchStart = resultStart + 20;
                    continue;
                }

                linkStart += 6;
                var linkEnd = html.IndexOf('"', linkStart);
                if (linkEnd < 0)
                    break;

                var url = html.Substring(linkStart, linkEnd - linkStart);
                
                // Пропускаємо внутрішні та некоректні посилання
                if (url.StartsWith("/") || url.StartsWith("#") || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    searchStart = linkEnd + 1;
                    continue;
                }

                // Витягуємо заголовок
                var titleStart = html.IndexOf(">", linkEnd);
                var titleEnd = html.IndexOf("<", titleStart + 1);
                var title = titleStart >= 0 && titleEnd > titleStart
                    ? System.Net.WebUtility.HtmlDecode(html.Substring(titleStart + 1, titleEnd - titleStart - 1).Trim())
                    : uri.Host;

                // Витягуємо опис
                var descStart = html.IndexOf("class=\"content\"", linkEnd, StringComparison.OrdinalIgnoreCase);
                var description = string.Empty;
                
                if (descStart >= 0 && descStart < linkEnd + 2000)
                {
                    var descTextStart = html.IndexOf(">", descStart);
                    var descTextEnd = html.IndexOf("<", descTextStart + 1);
                    if (descTextStart >= 0 && descTextEnd > descTextStart)
                    {
                        description = System.Net.WebUtility.HtmlDecode(
                            html.Substring(descTextStart + 1, Math.Min(descTextEnd - descTextStart - 1, 300)).Trim()
                        );
                    }
                }

                if (!string.IsNullOrWhiteSpace(url))
                {
                    results.Add(new UnifiedSearchResult
                    {
                        Title = string.IsNullOrWhiteSpace(title) ? uri.Host : title,
                        Url = url,
                        DisplayUrl = $"{uri.Host} › MetaSearx",
                        Snippet = string.IsNullOrWhiteSpace(description) 
                            ? $"Результат з MetaSearx для \"{query}\""
                            : description,
                        Source = SearchSourceType.MetaSearx,
                        Timestamp = null,
                        RankScore = 0,
                        PageNumber = 1
                    });
                    count++;
                }

                searchStart = linkEnd + 1;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MetaSearx] HTML parse error: {ex.Message}");
        }

        return results;
    }

    /// <summary>
    /// Обрізає текст до заданої довжини
    /// </summary>
    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        
        return text.Substring(0, maxLength - 3) + "...";
    }

    /// <summary>
    /// Створює посилання на пошук в MetaSearx
    /// </summary>
    private UnifiedSearchResult CreateMetaSearxSearchLink(string query)
    {
        return new UnifiedSearchResult
        {
            Title = $"Пошук в MetaSearx: \"{query}\"",
            Url = $"{SearchApiUrl}?q={Uri.EscapeDataString(query)}",
            DisplayUrl = "metasearx.com › search",
            Snippet = $"Відкрити результати метапошуку в MetaSearx - агрегатор результатів з Google, Bing, DuckDuckGo та інших пошукових систем для \"{query}\".",
            Source = SearchSourceType.MetaSearx,
            Timestamp = null,
            RankScore = 0,
            PageNumber = 1
        };
    }
}

