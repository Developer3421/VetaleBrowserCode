using System;
using System.Collections.Generic;
using System.Globalization; // for culture detection
using System.Linq; // for FirstOrDefault
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Search.Models;

namespace VetaleBrowser.VetaleBrowser.Search.Services;

public interface IWikipediaSearchClient
{
    Task<IReadOnlyList<UnifiedSearchResult>> SearchTopAsync(string query, int maxResults, CancellationToken ct = default);
}

/// <summary>
/// Клієнт пошуку Wikipedia через офіційне API (en.wikipedia.org, action=query&amp;list=search).
/// </summary>
public sealed class WikipediaSearchClient : IWikipediaSearchClient, IDisposable
{
    private readonly HttpClient _http;

    public WikipediaSearchClient(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        if (!_http.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "VetaleBrowser/1.0 (WikipediaSearchClient)");
        }
    }

    public async Task<IReadOnlyList<UnifiedSearchResult>> SearchTopAsync(string query, int maxResults, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<UnifiedSearchResult>();

        var lang = DetectWikipediaLang(query);
        var encoded = Uri.EscapeDataString(query);
        var url = $"https://{lang}.wikipedia.org/w/api.php?action=query&list=search&srsearch={encoded}&format=json&utf8=1&srlimit={maxResults}";

        WikipediaSearchResponse? payload = null;
        try
        {
            using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return Array.Empty<UnifiedSearchResult>();

            payload = await resp.Content.ReadFromJsonAsync<WikipediaSearchResponse>(cancellationToken: ct).ConfigureAwait(false);
        }
        catch
        {
            return Array.Empty<UnifiedSearchResult>();
        }

        if (payload?.Query?.Search is not { Length: > 0 } allResults)
            return Array.Empty<UnifiedSearchResult>();

        // 1) точний збіг заголовка
        var bestMatch = allResults.FirstOrDefault(r =>
            !string.IsNullOrEmpty(r.Title) && string.Equals(r.Title.Trim(), query.Trim(), StringComparison.OrdinalIgnoreCase));

        // 2) серед решти — віддати перевагу заголовкам без дужок
        if (bestMatch == null)
            bestMatch = allResults.FirstOrDefault(r => !string.IsNullOrEmpty(r.Title) && !r.Title!.Contains('('));

        // 3) інакше — перший із списку API
        if (bestMatch == null)
            bestMatch = allResults[0];

        var title = bestMatch.Title ?? string.Empty;
        var pageUrl = $"https://{lang}.wikipedia.org/wiki/{Uri.EscapeDataString(title.Replace(' ', '_'))}";

        return new List<UnifiedSearchResult>
        {
            new UnifiedSearchResult
            {
                Title = title,
                Url = pageUrl,
                DisplayUrl = $"wikipedia.org › {lang} › {title}",
                Snippet = StripHtml(bestMatch.Snippet ?? string.Empty),
                Source = SearchSourceType.Wikipedia,
                Timestamp = bestMatch.Timestamp,
                RankScore = bestMatch.Score,
                PageNumber = 1
            }
        };
    }

    private static string DetectWikipediaLang(string query)
    {
        // Просте визначення мови запиту + запасний варіант за культурою інтерфейсу.
        // Пріоритет: uk (якщо є українські літери), ru (російські), інакше — мова UI або en.
        if (string.IsNullOrEmpty(query))
            return FallbackLang();

        foreach (var ch in query)
        {
            if ("іїєґІЇЄҐ".IndexOf(ch) >= 0) return "uk";
        }
        foreach (var ch in query)
        {
            if ("ёЁъЪыЫэЭ".IndexOf(ch) >= 0) return "ru";
        }

        // Якщо кирилиця, але без специфічних букв — спробуємо uk, потім ru
        if (query.Any(c => char.GetUnicodeCategory(c) == UnicodeCategory.UppercaseLetter || char.GetUnicodeCategory(c) == UnicodeCategory.LowercaseLetter))
        {
            // Якщо є будь-які кириличні букви
            bool hasCyr = query.Any(c => c >= '\u0400' && c <= '\u04FF');
            if (hasCyr)
            {
                // Нехай спочатку uk за гео-очікуванням цього проєкту
                return "uk";
            }
        }

        // fallback: мова UI
        return FallbackLang();

        static string FallbackLang()
        {
            var ui = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            if (ui.Equals("uk", StringComparison.OrdinalIgnoreCase)) return "uk";
            if (ui.Equals("ru", StringComparison.OrdinalIgnoreCase)) return "ru";
            if (ui.Equals("en", StringComparison.OrdinalIgnoreCase)) return "en";
            return "en";
        }
    }

    private static string StripHtml(string input)
    {
        var span = input ?? string.Empty;
        var result = new System.Text.StringBuilder(span.Length);
        var insideTag = false;

        foreach (var ch in span)
        {
            if (ch == '<')
            {
                insideTag = true;
                continue;
            }

            if (ch == '>')
            {
                insideTag = false;
                continue;
            }

            if (!insideTag)
            {
                result.Append(ch);
            }
        }

        return result.ToString();
    }

    public void Dispose()
    {
        _http.Dispose();
    }

    private sealed class WikipediaSearchResponse
    {
        [JsonPropertyName("query")]
        public WikipediaQuery? Query { get; set; }
    }

    private sealed class WikipediaQuery
    {
        [JsonPropertyName("search")]
        public WikipediaSearchItem[]? Search { get; set; }
    }

    private sealed class WikipediaSearchItem
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("snippet")]
        public string? Snippet { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime? Timestamp { get; set; }

        [JsonPropertyName("score")]
        public double Score { get; set; }
    }
}
