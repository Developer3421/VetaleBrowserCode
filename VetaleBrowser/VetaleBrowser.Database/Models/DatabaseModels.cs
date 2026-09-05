using System;

namespace VetaleBrowser.VetaleBrowser.Database.Models;

/// <summary>
/// Model for storing tabs in the database
/// </summary>
public class TabModel
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? FaviconUrl { get; set; }
    public byte[]? FaviconData { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    public int SessionId { get; set; }
    public int Order { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Model for browser session
/// </summary>
public class BrowserSession
{
    public int Id { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public bool IsCurrent { get; set; }
    public int TabCount { get; set; }
}

/// <summary>
/// Model for bookmarks
/// </summary>
public class Bookmark
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Folder { get; set; } = "Bookmarks";
    public string? FaviconUrl { get; set; }
    public byte[]? FaviconData { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int Order { get; set; }
}

/// <summary>
/// Model for browsing history
/// </summary>
public class HistoryItem
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? FaviconUrl { get; set; }
    public byte[]? FaviconData { get; set; }
    public DateTime VisitedAt { get; set; } = DateTime.UtcNow;
    public int VisitCount { get; set; } = 1;
}

/// <summary>
/// Model for browser settings with AES encryption
/// </summary>
public class SettingItem
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string EncryptedValue { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Model for page indexing for Vetale Search (local search)
/// </summary>
public class SearchIndex
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty; // Page text content
    public string Description { get; set; } = string.Empty; // Meta description
    public string Keywords { get; set; } = string.Empty; // Meta keywords
    public string? FaviconUrl { get; set; }
    public byte[]? FaviconData { get; set; }
    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastVisitedAt { get; set; } = DateTime.UtcNow;
    public int VisitCount { get; set; } = 1;
    public int RelevanceScore { get; set; } = 0; // Relevance score (based on visit frequency)
    public string Language { get; set; } = "uk"; // Content language
}

/// <summary>
/// Model for user search queries (search history)
/// </summary>
public class SearchQuery
{
    public int Id { get; set; }
    public string Query { get; set; } = string.Empty;
    public string SearchEngine { get; set; } = "Vetale Search"; // Name of the search engine used
    public DateTime SearchedAt { get; set; } = DateTime.UtcNow;
    public int ResultsCount { get; set; } = 0;
    public int? ClickedResultId { get; set; } // ID of the clicked result (if any)
}

/// <summary>
/// Model for keywords and their weight in the search index
/// </summary>
public class SearchKeyword
{
    public int Id { get; set; }
    public int SearchIndexId { get; set; } // Relation to SearchIndex
    public string Keyword { get; set; } = string.Empty;
    public int Frequency { get; set; } = 1; // Number of word occurrences on the page
    public double Weight { get; set; } = 1.0; // Word weight (headings have higher weight)
}

/// <summary>
/// Model for file downloads (download manager)
/// </summary>
public class DownloadItem
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty; // encrypted
    public string FileName { get; set; } = string.Empty; // encrypted
    public string TargetPath { get; set; } = string.Empty; // encrypted full path
    public string Status { get; set; } = "Pending"; // Pending, Downloading, Completed, Error, Cancelled
    public long BytesReceived { get; set; }
    public long TotalBytes { get; set; } = -1; // -1 if unknown
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public string? ErrorMessage { get; set; } // encrypted
    public string? ContentType { get; set; } // encrypted
    public double LastMeasuredSpeedBytesPerSec { get; set; } // last speed measurement
    public double AverageSpeedBytesPerSec { get; set; } // moving average
    public double EstimatedRemainingSeconds { get; set; } // estimated time to completion
    public bool IsArchived { get; set; } // for cleaning old records
    public DateTime? ImportedAt { get; set; } // date of first import from downloads folder scan
}

/// <summary>
/// Model for storing API keys with AES encryption
/// </summary>
public class ApiKeyItem
{
    public int Id { get; set; }
    
    /// <summary>
    /// Unique service identifier (e.g.: "gemini", "pexels", "unsplash", "youtube")
    /// </summary>
    public string ServiceId { get; set; } = string.Empty;
    
    /// <summary>
    /// Service display name (e.g.: "Google Gemini", "Pexels", "Unsplash", "YouTube")
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;
    
    /// <summary>
    /// Encrypted API key
    /// </summary>
    public string EncryptedApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this key is active (user can disable)
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Creation date
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Last update date
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Date of last successful use
    /// </summary>
    public DateTime? LastUsedAt { get; set; }
    
    /// <summary>
    /// Number of successful requests
    /// </summary>
    public int SuccessfulRequestsCount { get; set; } = 0;
    
    /// <summary>
    /// Number of failed requests
    /// </summary>
    public int FailedRequestsCount { get; set; } = 0;
}

