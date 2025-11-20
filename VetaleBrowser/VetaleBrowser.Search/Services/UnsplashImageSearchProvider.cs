using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Search.Models;

namespace VetaleBrowser.VetaleBrowser.Search.Services
{
    /// <summary>
    /// Провайдер пошуку зображень через Unsplash API.
    /// API документація: https://unsplash.com/documentation
    /// </summary>
    public class UnsplashImageSearchProvider : IImageSearchProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _accessKey;

        public UnsplashImageSearchProvider(string accessKey)
        {
            _accessKey = accessKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Accept-Version", "v1");
        }

        public async Task<ImageSearchPage> SearchAsync(ImageSearchQuery query, CancellationToken ct)
        {
            try
            {
                var url = $"https://api.unsplash.com/search/photos?client_id={_accessKey}&query={Uri.EscapeDataString(query.Query)}&per_page={query.PageSize}&page={query.PageNumber}";
                
                var response = await _httpClient.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                var unsplashResponse = JsonSerializer.Deserialize<UnsplashResponse>(json, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                if (unsplashResponse == null || unsplashResponse.Results == null)
                {
                    return new ImageSearchPage
                    {
                        Query = query.Query,
                        PageNumber = query.PageNumber,
                        PageSize = query.PageSize,
                        TotalResults = 0,
                        HasNextPage = false,
                        Provider = "Unsplash",
                        Results = Array.Empty<ImageSearchResult>()
                    };
                }

                var results = new ImageSearchResult[unsplashResponse.Results.Length];
                for (int i = 0; i < unsplashResponse.Results.Length; i++)
                {
                    var photo = unsplashResponse.Results[i];
                    results[i] = new ImageSearchResult
                    {
                        Id = photo.Id ?? string.Empty,
                        Provider = "Unsplash",
                        Title = photo.AltDescription ?? photo.Description ?? $"Photo by {photo.User?.Name}",
                        PhotographerName = photo.User?.Name ?? "Unknown",
                        PhotographerUrl = photo.User?.Links?.Html ?? string.Empty,
                        SourcePageUrl = photo.Links?.Html ?? string.Empty,
                        Urls = new ImageUrlSet
                        {
                            ThumbUrl = photo.Urls?.Thumb ?? string.Empty,
                            SmallUrl = photo.Urls?.Small ?? string.Empty,
                            RegularUrl = photo.Urls?.Regular ?? string.Empty,
                            FullUrl = photo.Urls?.Full ?? string.Empty
                        },
                        Width = photo.Width,
                        Height = photo.Height,
                        Color = photo.Color ?? "#CCCCCC"
                    };
                }

                return new ImageSearchPage
                {
                    Query = query.Query,
                    PageNumber = query.PageNumber,
                    PageSize = query.PageSize,
                    TotalResults = unsplashResponse.Total,
                    HasNextPage = unsplashResponse.TotalPages > query.PageNumber,
                    Provider = "Unsplash",
                    Results = results
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UnsplashImageSearchProvider] Error: {ex.Message}");
                return new ImageSearchPage
                {
                    Query = query.Query,
                    PageNumber = query.PageNumber,
                    PageSize = query.PageSize,
                    TotalResults = 0,
                    HasNextPage = false,
                    Provider = "Unsplash",
                    Results = Array.Empty<ImageSearchResult>()
                };
            }
        }

        private class UnsplashResponse
        {
            public int Total { get; set; }
            public int TotalPages { get; set; }
            public UnsplashPhoto[]? Results { get; set; }
        }

        private class UnsplashPhoto
        {
            public string? Id { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public string? Color { get; set; }
            public string? Description { get; set; }
            public string? AltDescription { get; set; }
            public UnsplashUrls? Urls { get; set; }
            public UnsplashLinks? Links { get; set; }
            public UnsplashUser? User { get; set; }
        }

        private class UnsplashUrls
        {
            public string? Raw { get; set; }
            public string? Full { get; set; }
            public string? Regular { get; set; }
            public string? Small { get; set; }
            public string? Thumb { get; set; }
        }

        private class UnsplashLinks
        {
            public string? Self { get; set; }
            public string? Html { get; set; }
            public string? Download { get; set; }
        }

        private class UnsplashUser
        {
            public string? Id { get; set; }
            public string? Username { get; set; }
            public string? Name { get; set; }
            public UnsplashUserLinks? Links { get; set; }
        }

        private class UnsplashUserLinks
        {
            public string? Self { get; set; }
            public string? Html { get; set; }
        }
    }
}

