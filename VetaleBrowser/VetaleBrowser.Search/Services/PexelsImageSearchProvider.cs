using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Search.Models;

namespace VetaleBrowser.VetaleBrowser.Search.Services
{
    /// <summary>
    /// Провайдер пошуку зображень через Pexels API.
    /// API документація: https://www.pexels.com/api/documentation/
    /// </summary>
    public class PexelsImageSearchProvider : IImageSearchProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public PexelsImageSearchProvider(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", apiKey);
        }

        public async Task<ImageSearchPage> SearchAsync(ImageSearchQuery query, CancellationToken ct)
        {
            try
            {
                var url = $"https://api.pexels.com/v1/search?query={Uri.EscapeDataString(query.Query)}&per_page={query.PageSize}&page={query.PageNumber}";
                
                var response = await _httpClient.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                var pexelsResponse = JsonSerializer.Deserialize<PexelsResponse>(json, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                if (pexelsResponse == null || pexelsResponse.Photos == null)
                {
                    return CreateEmptyPage(query);
                }

                var results = new List<ImageSearchResult>();
                for (int i = 0; i < pexelsResponse.Photos.Length; i++)
                {
                    var photo = pexelsResponse.Photos[i];
                    results.Add(new ImageSearchResult
                    {
                        Id = photo.Id.ToString(),
                        Provider = "Pexels",
                        Source = ImageSource.Pexels,
                        Title = photo.Alt ?? $"Photo by {photo.Photographer}",
                        PhotographerName = photo.Photographer ?? "Unknown",
                        Photographer = photo.Photographer ?? "Unknown",
                        PhotographerUrl = photo.PhotographerUrl ?? string.Empty,
                        SourcePageUrl = photo.Url ?? string.Empty,
                        Urls = new ImageUrlSet
                        {
                            ThumbUrl = photo.Src?.Tiny ?? string.Empty,
                            SmallUrl = photo.Src?.Small ?? string.Empty,
                            RegularUrl = photo.Src?.Medium ?? string.Empty,
                            FullUrl = photo.Src?.Original ?? string.Empty
                        },
                        Width = photo.Width,
                        Height = photo.Height,
                        Color = photo.AvgColor ?? "#CCCCCC",
                        AverageColor = photo.AvgColor ?? "#CCCCCC"
                    });
                }

                return new ImageSearchPage
                {
                    Query = query.Query,
                    Page = query.PageNumber,
                    PerPage = query.PageSize,
                    PageNumber = query.PageNumber,
                    PageSize = query.PageSize,
                    TotalResults = pexelsResponse.TotalResults,
                    HasNextPage = pexelsResponse.Page * pexelsResponse.PerPage < pexelsResponse.TotalResults,
                    Provider = "Pexels",
                    Results = results
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PexelsImageSearchProvider] Error: {ex.Message}");
                return CreateEmptyPage(query);
            }
        }

        private ImageSearchPage CreateEmptyPage(ImageSearchQuery query)
        {
            return new ImageSearchPage
            {
                Query = query.Query,
                Page = query.PageNumber,
                PerPage = query.PageSize,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                TotalResults = 0,
                HasNextPage = false,
                Provider = "Pexels",
                Results = new List<ImageSearchResult>()
            };
        }

        private class PexelsResponse
        {
            public int Page { get; set; }
            public int PerPage { get; set; }
            public PexelsPhoto[]? Photos { get; set; }
            public int TotalResults { get; set; }
        }

        private class PexelsPhoto
        {
            public int Id { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public string? Url { get; set; }
            public string? Photographer { get; set; }
            public string? PhotographerUrl { get; set; }
            public string? AvgColor { get; set; }
            public PexelsSrc? Src { get; set; }
            public string? Alt { get; set; }
        }

        private class PexelsSrc
        {
            public string? Original { get; set; }
            public string? Medium { get; set; }
            public string? Small { get; set; }
            public string? Tiny { get; set; }
        }
    }
}
