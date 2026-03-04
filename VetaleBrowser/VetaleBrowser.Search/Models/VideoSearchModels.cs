namespace VetaleBrowser.VetaleBrowser.Search.Models
{
    /// <summary>
    /// Video search query
    /// </summary>
    public class VideoSearchQuery
    {
        public string Query { get; set; } = string.Empty;
        public int PageNumber { get; set; } = 1;
        /// <summary>
        /// Maximum number of results per page (maximum 50)
        /// </summary>
        public int PageSize { get; set; } = 50;
        /// <summary>
        /// Provider: "YouTube", "All"
        /// </summary>
        public string Provider { get; set; } = "YouTube";
    }

    /// <summary>
    /// Video search result
    /// </summary>
    public class VideoSearchResult
    {
        public string Id { get; set; } = string.Empty;
        public string VideoId { get; set; } = string.Empty;
        public string Provider { get; set; } = "YouTube";
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string VideoUrl { get; set; } = string.Empty;
        public string ChannelTitle { get; set; } = string.Empty;
        public string ChannelId { get; set; } = string.Empty;
        public string ChannelUrl { get; set; } = string.Empty;
        public System.DateTime? PublishedAt { get; set; }
        public string Duration { get; set; } = string.Empty;
        public long ViewCount { get; set; }
        public long LikeCount { get; set; }
        public System.Collections.Generic.List<string>? Tags { get; set; }
    }

    /// <summary>
    /// Video search results page
    /// </summary>
    public class VideoSearchPage
    {
        public string Query { get; set; } = string.Empty;
        public int PageNumber { get; set; }
        /// <summary>
        /// Maximum number of results per page (maximum 50)
        /// </summary>
        public int PageSize { get; set; } = 50;
        public int TotalResults { get; set; }
        public bool HasNextPage { get; set; }
        public string? NextPageToken { get; set; }
        public string? PrevPageToken { get; set; }
        public double SearchTime { get; set; }
        public string Provider { get; set; } = "YouTube";
        public System.Collections.Generic.List<VideoSearchResult> Results { get; set; } = new();
    }
}

