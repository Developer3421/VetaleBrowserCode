using System;

namespace VetaleBrowser.VetaleBrowser.Search.Models;

/// <summary>
/// Source of search result.
/// </summary>
public enum SearchSourceType
{
    Unknown = 0,
    Wikipedia = 1,
    WebArchive = 2,
    CommonCrawl = 3,
    YouTube = 4,
    Curlie = 5,       // Curlie.org - open web directory (successor to DMOZ)
    MetaSearx = 6     // MetaSearx - meta search engine (aggregator for Google, Bing, DuckDuckGo, etc.)
}

/// <summary>
/// Unified search result for display in Vetale Search.
/// </summary>
public sealed class UnifiedSearchResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    /// <summary>
    /// Human-readable URL variant (without "http(s)://"), used in breadcrumb.
    /// </summary>
    public string DisplayUrl { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public SearchSourceType Source { get; set; } = SearchSourceType.Unknown;
    public DateTime? Timestamp { get; set; }
    /// <summary>
    /// Additional rank/relevance score for sorting.
    /// </summary>
    public double RankScore { get; set; }
    /// <summary>
    /// Page number within the session where the result is located.
    /// </summary>
    public int PageNumber { get; set; }
}

/// <summary>
/// Unified search results page.
/// </summary>
public sealed class UnifiedSearchPage
{
    public Guid SearchSessionId { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalResults { get; set; }
    public UnifiedSearchResult[] Results { get; set; } = Array.Empty<UnifiedSearchResult>();
    
    /// <summary>
    /// Whether there is a next page of results
    /// </summary>
    public bool HasNextPage { get; set; }
    
    /// <summary>
    /// Whether there is a previous page of results
    /// </summary>
    public bool HasPreviousPage { get; set; }
    
    /// <summary>
    /// Search query
    /// </summary>
    public string Query { get; set; } = string.Empty;
}
