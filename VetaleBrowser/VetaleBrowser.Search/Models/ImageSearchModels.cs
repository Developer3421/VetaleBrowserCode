namespace VetaleBrowser.VetaleBrowser.Search.Models
{
    public class ImageSearchQuery
    {
        public string Query { get; set; } = string.Empty;
        public int PageNumber { get; set; } = 1;
        /// <summary>
        /// Maximum number of results per page (maximum 50)
        /// </summary>
        public int PageSize { get; set; } = 50;
        /// <summary>
        /// Source: "Pexels", "Unsplash" or "All".
        /// </summary>
        public string Provider { get; set; } = "All";
    }

    /// <summary>
    /// Filter for image search
    /// </summary>
    public class ImageSearchFilter
    {
        /// <summary>
        /// Orientation: "landscape", "portrait", "square"
        /// </summary>
        public string? Orientation { get; set; }
        
        /// <summary>
        /// Size: "large", "medium", "small" (Pexels)
        /// </summary>
        public string? Size { get; set; }
        
        /// <summary>
        /// Color: "red", "orange", "yellow", "green", "turquoise", "blue", "violet", "pink", "brown", "black", "gray", "white"
        /// </summary>
        public string? Color { get; set; }
        
        /// <summary>
        /// Locale: "en-US", "pt-BR" etc. (Pexels)
        /// </summary>
        public string? Locale { get; set; }
    }

    public enum ImageSource
    {
        Pexels,
        Unsplash,
        Mock
    }

    public class ImageUrlSet
    {
        public string ThumbUrl { get; set; } = string.Empty;
        public string SmallUrl { get; set; } = string.Empty;
        public string RegularUrl { get; set; } = string.Empty;
        public string FullUrl { get; set; } = string.Empty;
    }

    public class ImageSearchResult
    {
        public string Id { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public ImageSource Source { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        
        // URL properties (old format for compatibility)
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string MediumUrl { get; set; } = string.Empty;
        public string LargeUrl { get; set; } = string.Empty;
        public string OriginalUrl { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        
        // URL properties (new format via ImageUrlSet)
        public string PhotographerName { get; set; } = string.Empty;
        public string Photographer { get; set; } = string.Empty;
        public string PhotographerUrl { get; set; } = string.Empty;
        public string SourcePageUrl { get; set; } = string.Empty;
        public ImageUrlSet Urls { get; set; } = new ImageUrlSet();
        
        public int Width { get; set; }
        public int Height { get; set; }
        public string Color { get; set; } = string.Empty;
        public string AverageColor { get; set; } = string.Empty;
        public System.Collections.Generic.List<string>? Tags { get; set; }
    }

    public class ImageSearchPage
    {
        public string Query { get; set; } = string.Empty;
        public int Page { get; set; }
        public int PerPage { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalResults { get; set; }
        public bool HasNextPage { get; set; }
        public double SearchTime { get; set; }
        public string Provider { get; set; } = string.Empty;
        public System.Collections.Generic.List<ImageSearchResult> Results { get; set; } = new();
    }
}
