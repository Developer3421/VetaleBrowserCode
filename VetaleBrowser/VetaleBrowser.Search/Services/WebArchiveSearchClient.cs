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
/// Client for searching in Web Archive (Internet Archive).
/// Creates a search URL for user query, analogous to Google/YouTube.
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

        // Create URL for searching in Web Archive (analogous to Google/YouTube)
        var webArchiveSearchUrl = $"https://web.archive.org/web/*/{Uri.EscapeDataString(query)}";

        var titleTemplate = SearchLocalization.Get(
            "Search.Redirect.WebArchive.Title",
            "🌐 Search in Web Archive: \"{0}\"");
        var snippetTemplate = SearchLocalization.Get(
            "Search.Redirect.WebArchive.Snippet",
            "Open archived snapshots of sites for \"{0}\" in Internet Archive.");

        string SafeFormat(string template, string arg)
        {
            try { return string.Format(template, arg); }
            catch { return template.Replace("{0}", arg); }
        }

        // Return one result-link to the search in Web Archive
        var results = new List<UnifiedSearchResult>
        {
            new UnifiedSearchResult
            {
                Title = SafeFormat(titleTemplate, query),
                Url = webArchiveSearchUrl,
                DisplayUrl = "web.archive.org › search",
                Snippet = SafeFormat(snippetTemplate, query),
                Source = SearchSourceType.WebArchive,
                Timestamp = null,
                RankScore = 1,
                PageNumber = 1
            }
        };

        await Task.CompletedTask; // For compatibility with async
        return results;
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}
