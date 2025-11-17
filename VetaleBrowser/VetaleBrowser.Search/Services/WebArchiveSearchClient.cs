using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
/// Професійний клієнт WebArchive через CDX Server API (Wayback Machine).
/// Шукає знімки сайтів за запитом, фільтрує за сьогоднішньою датою та релевантністю.
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

        // Нормалізуємо запит: якщо це схоже на URL/домен — шукаємо знімки цього сайту
        var searchUrl = NormalizeQueryToUrl(query);
        
        // CDX Server API: пошук знімків за URL-паттерном
        var today = DateTime.UtcNow;
        var todayStr = today.ToString("yyyyMMdd"); // формат для Wayback: YYYYMMDD
        
        // Шукаємо знімки за сьогодні (from/to = сьогодні)
        var cdxUrl = $"https://web.archive.org/cdx/search/cdx?url={Uri.EscapeDataString(searchUrl)}&matchType=prefix&limit={maxResults * 3}&output=json&from={todayStr}&to={todayStr}&filter=statuscode:200&collapse=urlkey";

        List<UnifiedSearchResult> results = new();
        string content;
        
        try
        {
            using var resp = await _http.GetAsync(cdxUrl, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                // Якщо за сьогодні нічого немає, пробуємо останні 7 днів
                var weekAgo = today.AddDays(-7).ToString("yyyyMMdd");
                cdxUrl = $"https://web.archive.org/cdx/search/cdx?url={Uri.EscapeDataString(searchUrl)}&matchType=prefix&limit={maxResults * 2}&output=json&from={weekAgo}&to={todayStr}&filter=statuscode:200&collapse=urlkey";
                using var resp2 = await _http.GetAsync(cdxUrl, ct).ConfigureAwait(false);
                if (!resp2.IsSuccessStatusCode)
                    return Array.Empty<UnifiedSearchResult>();
                
                content = await resp2.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            }
            else
            {
                content = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            }

            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            // Перший рядок — заголовки, пропускаємо
            var entries = new List<CdxEntry>();
            for (int i = 1; i < lines.Length && entries.Count < maxResults * 2; i++)
            {
                var entry = ParseCdxLine(lines[i]);
                if (entry != null)
                    entries.Add(entry);
            }

            if (entries.Count == 0)
                return Array.Empty<UnifiedSearchResult>();

            // Ранжуємо за релевантністю (як у Wikipedia):
            // 1) Точний збіг URL з запитом
            var bestMatch = entries.FirstOrDefault(e => 
                !string.IsNullOrEmpty(e.Original) && 
                e.Original.Contains(query, StringComparison.OrdinalIgnoreCase));

            // 2) Без query-параметрів (чистіші URL)
            if (bestMatch == null)
                bestMatch = entries.FirstOrDefault(e => 
                    !string.IsNullOrEmpty(e.Original) && 
                    !e.Original.Contains('?'));

            // 3) Інакше беремо перші за датою
            var topEntries = bestMatch != null 
                ? new[] { bestMatch }.Concat(entries.Where(e => e != bestMatch).Take(maxResults - 1))
                : entries.Take(maxResults);

            foreach (var entry in topEntries)
            {
                var timestamp = entry.Timestamp;
                var originalUrl = entry.Original;
                
                // Формуємо URL знімка
                var snapshotUrl = $"https://web.archive.org/web/{timestamp}/{originalUrl}";
                
                // Витягуємо домен для відображення
                var displayUrl = GetDisplayUrl(originalUrl);
                var title = GetTitleFromUrl(originalUrl);
                
                // Парсимо дату знімка
                DateTime? snapshotDate = null;
                if (timestamp.Length >= 8)
                {
                    if (DateTime.TryParseExact(timestamp.Substring(0, 8), "yyyyMMdd", 
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    {
                        snapshotDate = parsed;
                    }
                }

                results.Add(new UnifiedSearchResult
                {
                    Title = title,
                    Url = snapshotUrl,
                    DisplayUrl = displayUrl,
                    Snippet = $"Archived snapshot from {snapshotDate?.ToString("d MMM yyyy") ?? "unknown date"}: {originalUrl}",
                    Source = SearchSourceType.WebArchive,
                    Timestamp = snapshotDate,
                    RankScore = entries.Count - entries.IndexOf(entry),
                    PageNumber = 1
                });

                if (results.Count >= maxResults)
                    break;
            }

            return results;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WebArchiveSearchClient] Error: {ex.Message}");
            return Array.Empty<UnifiedSearchResult>();
        }
    }

    private static string NormalizeQueryToUrl(string query)
    {
        // Якщо запит вже схожий на URL — використовуємо його
        if (query.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
            query.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return query;
        }

        // Якщо це схоже на домен (містить крапку, але не пробіли)
        if (query.Contains('.') && !query.Contains(' '))
        {
            return query.StartsWith("www.") ? query : $"*.{query}*";
        }

        // Інакше шукаємо як частину URL (wildcards)
        return $"*{query}*";
    }

    private static CdxEntry? ParseCdxLine(string line)
    {
        try
        {
            // CDX формат (JSON array): ["urlkey", "timestamp", "original", "mimetype", "statuscode", "digest", "length"]
            var parts = System.Text.Json.JsonSerializer.Deserialize<string[]>(line);
            if (parts == null || parts.Length < 3)
                return null;

            return new CdxEntry
            {
                UrlKey = parts[0],
                Timestamp = parts[1],
                Original = parts[2],
                MimeType = parts.Length > 3 ? parts[3] : null,
                StatusCode = parts.Length > 4 ? parts[4] : null
            };
        }
        catch
        {
            return null;
        }
    }

    private static string GetDisplayUrl(string url)
    {
        try
        {
            var uri = new Uri(url.StartsWith("http") ? url : "https://" + url);
            var path = uri.AbsolutePath.TrimEnd('/');
            if (string.IsNullOrEmpty(path) || path == "/")
                return $"web.archive.org › {uri.Host}";
            
            return $"web.archive.org › {uri.Host} › {path.TrimStart('/')}";
        }
        catch
        {
            return $"web.archive.org › {url}";
        }
    }

    private static string GetTitleFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url.StartsWith("http") ? url : "https://" + url);
            var path = uri.AbsolutePath.TrimEnd('/');
            
            // Якщо є шлях — використовуємо останній сегмент
            if (!string.IsNullOrEmpty(path) && path != "/")
            {
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length > 0)
                {
                    var last = segments[^1];
                    // Прибираємо розширення файлу
                    var title = last.Contains('.') ? last.Substring(0, last.LastIndexOf('.')) : last;
                    // Замінюємо дефіси/підкреслення на пробіли
                    title = title.Replace('-', ' ').Replace('_', ' ');
                    return $"{uri.Host}: {title}";
                }
            }

            // Інакше просто домен
            return uri.Host;
        }
        catch
        {
            return url;
        }
    }

    public void Dispose()
    {
        _http.Dispose();
    }

    private sealed class CdxEntry
    {
        public string UrlKey { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public string Original { get; set; } = string.Empty;
        public string? MimeType { get; set; }
        public string? StatusCode { get; set; }
    }
}

