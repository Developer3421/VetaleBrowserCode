using System;

namespace VetaleBrowser.VetaleBrowser.Search.Models;

/// <summary>
/// Джерело результату пошуку.
/// </summary>
public enum SearchSourceType
{
    Unknown = 0,
    Wikipedia = 1,
    WebArchive = 2,
    CommonCrawl = 3,
    YouTube = 4 // новий тип для пошуку на YouTube
}

/// <summary>
/// Уніфікований результат пошуку для відображення у Vetale Search.
/// </summary>
public sealed class UnifiedSearchResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    /// <summary>
    /// Людинозрозумілий варіант URL (без "http(s)://"), що використовується в breadcrumb.
    /// </summary>
    public string DisplayUrl { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public SearchSourceType Source { get; set; } = SearchSourceType.Unknown;
    public DateTime? Timestamp { get; set; }
    /// <summary>
    /// Додатковий ранг/бал релевантності для сортування.
    /// </summary>
    public double RankScore { get; set; }
    /// <summary>
    /// Номер сторінки в межах сесії, на якій розташований результат.
    /// </summary>
    public int PageNumber { get; set; }
}

/// <summary>
/// Сторінка результатів уніфікованого пошуку.
/// </summary>
public sealed class UnifiedSearchPage
{
    public Guid SearchSessionId { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalResults { get; set; }
    public UnifiedSearchResult[] Results { get; set; } = Array.Empty<UnifiedSearchResult>();
}
