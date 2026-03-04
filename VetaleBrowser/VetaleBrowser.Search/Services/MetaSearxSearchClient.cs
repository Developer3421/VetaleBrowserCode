using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Search.Models;

namespace VetaleBrowser.VetaleBrowser.Search.Services;

/// <summary>
/// Interface for searching via MetaSearx (open meta search engine)
/// </summary>
public interface IMetaSearxSearchClient
{
    /// <summary>
    /// Searches results via MetaSearx API with pagination
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="resultsPerPage">Number of results per page (default 50)</param>
    /// <param name="ct">Cancellation token</param>
    Task<MetaSearxSearchResult> SearchAsync(string query, int page = 1, int resultsPerPage = 50, CancellationToken ct = default);
    
    /// <summary>
    /// Simple search without pagination (for compatibility)
    /// </summary>
    Task<List<UnifiedSearchResult>> SearchAsync(string query, int maxResults = 5, CancellationToken ct = default);
}

/// <summary>
/// MetaSearx search result with pagination information
/// </summary>
public sealed class MetaSearxSearchResult
{
    /// <summary>
    /// Search results on current page
    /// </summary>
    public List<UnifiedSearchResult> Results { get; set; } = new();
    
    /// <summary>
    /// Current page (1-based)
    /// </summary>
    public int CurrentPage { get; set; } = 1;
    
    /// <summary>
    /// Number of results per page
    /// </summary>
    public int ResultsPerPage { get; set; } = 50;
    
    /// <summary>
    /// Whether there is a next page
    /// </summary>
    public bool HasNextPage { get; set; }
    
    /// <summary>
    /// Whether there is a previous page
    /// </summary>
    public bool HasPreviousPage { get; set; }
    
    /// <summary>
    /// Total number of results (if known)
    /// </summary>
    public int? TotalResults { get; set; }
    
    /// <summary>
    /// Total number of pages (if known)
    /// </summary>
    public int? TotalPages { get; set; }
    
    /// <summary>
    /// Search query
    /// </summary>
    public string Query { get; set; } = string.Empty;
}

/// <summary>
/// Client for searching via MetaSearx.
/// MetaSearx is a meta search engine that aggregates results from many search engines.
/// </summary>
public sealed class MetaSearxSearchClient : IMetaSearxSearchClient
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    // Public MetaSearx instance
    private const string BaseUrl = "https://metasearx.com";
    private const string SearchApiUrl = "https://metasearx.com/search";
    
    // Number of results per MetaSearx API page (usually ~10-20)
    private const int MetaSearxResultsPerPage = 10;

    /// <summary>
    /// Search with full pagination (professional mode)
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
            // Calculate which MetaSearx pages need to be loaded
            // Example: page=1, resultsPerPage=50 -> MetaSearx pages 1-5
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
                    // Avoid duplicates
                    if (!allResults.Exists(x => x.Url.Equals(r.Url, StringComparison.OrdinalIgnoreCase)))
                    {
                        r.PageNumber = page;
                        allResults.Add(r);
                    }
                }
                
                hasMoreResults = hasMore;
                
                // Delay between requests
                if (metaPage < endMetaSearxPage)
                {
                    await Task.Delay(50, ct);
                }
            }
            
            // Limit to resultsPerPage
            result.Results = allResults.Count > resultsPerPage 
                ? allResults.GetRange(0, resultsPerPage) 
                : allResults;
            
            result.HasNextPage = hasMoreResults || allResults.Count >= resultsPerPage;
            
            // Update RankScore for correct sorting
            for (int i = 0; i < result.Results.Count; i++)
            {
                result.Results[i].RankScore = resultsPerPage - i;
            }
            
            System.Diagnostics.Debug.WriteLine($"[MetaSearx] Page {page}: {result.Results.Count} results, hasNext={result.HasNextPage}");
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
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
    /// Simple search without pagination (for compatibility with existing code)
    /// </summary>
    public async Task<List<UnifiedSearchResult>> SearchAsync(string query, int maxResults = 5, CancellationToken ct = default)
    {
        var searchResult = await SearchAsync(query, page: 1, resultsPerPage: maxResults, ct);
        return searchResult.Results;
    }
    
    /// <summary>
    /// Loads one page of results with information about whether there are more
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
            // Normal cancellation
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MetaSearx] FetchPage error: {ex.Message}");
        }

        return (results, hasMore);
    }

    /// <summary>
    /// Parses JSON response with pagination information
    /// </summary>
    private (List<UnifiedSearchResult> Results, bool HasMore) ParseJsonResponseWithInfo(string json)
    {
        var results = new List<UnifiedSearchResult>();
        var hasMore = false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Check if there is pagination information
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
                                ? $"Result from {engine}"
                                : TruncateText(content, 200),
                            Source = SearchSourceType.MetaSearx,
                            Timestamp = null,
                            RankScore = 0,
                            PageNumber = 1
                        });
                    }
                    catch
                    {
                        // Skip invalid element
                    }
                }
                
                // If we got results, there are likely more
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
    /// Parses HTML response from MetaSearx (fallback)
    /// </summary>
    private List<UnifiedSearchResult> ParseHtmlResponse(string html, string query, int maxResults)
    {
        var results = new List<UnifiedSearchResult>();

        try
        {
            var searchStart = 0;
            var count = 0;

            // Search for results in HTML (class result or article)
            while (count < maxResults)
            {
                // Search for links to results
                var resultStart = html.IndexOf("class=\"result\"", searchStart, StringComparison.OrdinalIgnoreCase);
                if (resultStart < 0)
                {
                    resultStart = html.IndexOf("class=\"result_header\"", searchStart, StringComparison.OrdinalIgnoreCase);
                    if (resultStart < 0)
                        break;
                }

                // Find href
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
                
                // Skip internal and invalid links
                if (url.StartsWith("/") || url.StartsWith("#") || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    searchStart = linkEnd + 1;
                    continue;
                }

                // Extract title
                var titleStart = html.IndexOf(">", linkEnd);
                var titleEnd = html.IndexOf("<", titleStart + 1);
                var title = titleStart >= 0 && titleEnd > titleStart
                    ? System.Net.WebUtility.HtmlDecode(html.Substring(titleStart + 1, titleEnd - titleStart - 1).Trim())
                    : uri.Host;

                // Extract description
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
                            ? $"Result from MetaSearx for \"{query}\""
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
    /// Truncates text to given length
    /// </summary>
    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        
        return text.Substring(0, maxLength - 3) + "...";
    }

    /// <summary>
    /// Creates a search link in MetaSearx
    /// </summary>
    private UnifiedSearchResult CreateMetaSearxSearchLink(string query)
    {
        return new UnifiedSearchResult
        {
            Title = $"Search in MetaSearx: \"{query}\"",
            Url = $"{SearchApiUrl}?q={Uri.EscapeDataString(query)}",
            DisplayUrl = "metasearx.com › search",
            Snippet = $"Open meta search results in MetaSearx - aggregator of results from Google, Bing, DuckDuckGo and other search engines for \"{query}\".",
            Source = SearchSourceType.MetaSearx,
            Timestamp = null,
            RankScore = 0,
            PageNumber = 1
        };
    }
}

