namespace VetaleBrowser.VetaleBrowser.Search.Models
{
    public class ImageSearchQuery
    {
        public string Query { get; set; } = string.Empty;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        /// <summary>
        /// Источник: "Pexels", "Unsplash" или "All".
        /// </summary>
        public string Provider { get; set; } = "All";
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
        public string Title { get; set; } = string.Empty;
        public string PhotographerName { get; set; } = string.Empty;
        public string PhotographerUrl { get; set; } = string.Empty;
        public string SourcePageUrl { get; set; } = string.Empty;
        public ImageUrlSet Urls { get; set; } = new ImageUrlSet();
        public int Width { get; set; }
        public int Height { get; set; }
        public string Color { get; set; } = string.Empty;
    }

    public class ImageSearchPage
    {
        public string Query { get; set; } = string.Empty;
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalResults { get; set; }
        public bool HasNextPage { get; set; }
        public string Provider { get; set; } = string.Empty;
        public ImageSearchResult[] Results { get; set; } = System.Array.Empty<ImageSearchResult>();
    }
}
