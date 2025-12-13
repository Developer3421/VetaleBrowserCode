using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Search.Models;
using VetaleBrowser.VetaleBrowser.Database.Services;

namespace VetaleBrowser.VetaleBrowser.Search.Services
{
    /// <summary>
    /// Дефолтні API ключі для пошуку
    /// </summary>
    public static class DefaultApiKeys
    {
        public const string PexelsApiKey = "";
        public const string UnsplashAccessKey = "";
        public const string YouTubeApiKey = "";
    }

    /// <summary>
    /// Інтерфейс для провайдерів пошуку зображень
    /// </summary>
    public interface IImageSearchProvider
    {
        Task<ImageSearchPage> SearchAsync(ImageSearchQuery query, CancellationToken ct);
    }

    public static class ImageSearchServiceFactory
    {
        public const int MaxResultsPerPage = 50;
        
        private static string? _cachedPexelsKey;
        private static string? _cachedUnsplashKey;
        private static string? _cachedYouTubeKey;
        private static string? _serviceCreatedWithPexelsKey;
        private static string? _serviceCreatedWithUnsplashKey;
        private static IImageSearchService? _cachedService;
        
        public static void SetPexelsApiKey(string? apiKey)
        {
            var cleanKey = apiKey?.Trim();
            System.Diagnostics.Debug.WriteLine($"[ImageSearchServiceFactory] SetPexelsApiKey called");
            System.Diagnostics.Debug.WriteLine($"[ImageSearchServiceFactory] Key length: {cleanKey?.Length ?? 0}");
            System.Diagnostics.Debug.WriteLine($"[ImageSearchServiceFactory] Key preview: {(string.IsNullOrWhiteSpace(cleanKey) ? "EMPTY" : cleanKey.Substring(0, Math.Min(15, cleanKey.Length)) + "...")}");
            _cachedPexelsKey = cleanKey;
            _cachedService = null;
            _serviceCreatedWithPexelsKey = null;
        }
        
        public static void SetUnsplashApiKey(string? apiKey)
        {
            System.Diagnostics.Debug.WriteLine($"[ImageSearchServiceFactory] SetUnsplashApiKey: {(string.IsNullOrWhiteSpace(apiKey) ? "null/empty" : "***")}");
            _cachedUnsplashKey = apiKey;
            _cachedService = null;
            _serviceCreatedWithUnsplashKey = null;
        }
        
        public static void SetYouTubeApiKey(string? apiKey)
        {
            System.Diagnostics.Debug.WriteLine($"[ImageSearchServiceFactory] SetYouTubeApiKey: {(string.IsNullOrWhiteSpace(apiKey) ? "null/empty" : "***")}");
            _cachedYouTubeKey = apiKey;
        }
        
        public static void InvalidateCache()
        {
            _cachedService = null;
            _serviceCreatedWithPexelsKey = null;
            _serviceCreatedWithUnsplashKey = null;
            System.Diagnostics.Debug.WriteLine("[ImageSearchServiceFactory] Cache invalidated");
        }
        
        private static string? GetEffectivePexelsKey()
        {
            System.Diagnostics.Debug.WriteLine($"[ImageSearchServiceFactory] GetEffectivePexelsKey called");
            System.Diagnostics.Debug.WriteLine($"[ImageSearchServiceFactory] _cachedPexelsKey: {(_cachedPexelsKey == null ? "null" : _cachedPexelsKey.Length + " chars")}");
            
            // 1. Кешований ключ
            if (!string.IsNullOrWhiteSpace(_cachedPexelsKey))
            {
                System.Diagnostics.Debug.WriteLine($"[ImageSearchServiceFactory] Returning cached Pexels key: {_cachedPexelsKey.Substring(0, Math.Min(15, _cachedPexelsKey.Length))}...");
                return _cachedPexelsKey;
            }
            
            // 2. Дефолтний
            if (!string.IsNullOrWhiteSpace(DefaultApiKeys.PexelsApiKey))
            {
                System.Diagnostics.Debug.WriteLine($"[ImageSearchServiceFactory] Returning default Pexels key");
                return DefaultApiKeys.PexelsApiKey;
            }
            
            // 3. БД
            System.Diagnostics.Debug.WriteLine($"[ImageSearchServiceFactory] Trying to load Pexels key from DB...");
            var dbKey = LoadApiKeyFromDatabase(ApiServiceIds.Pexels);
            if (!string.IsNullOrWhiteSpace(dbKey))
            {
                System.Diagnostics.Debug.WriteLine($"[ImageSearchServiceFactory] Got Pexels key from DB: {dbKey.Substring(0, Math.Min(15, dbKey.Length))}...");
                _cachedPexelsKey = dbKey;
                return dbKey;
            }
            
            // 4. ENV
            var envKey = Environment.GetEnvironmentVariable("VETALE_PEXELS_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                System.Diagnostics.Debug.WriteLine($"[ImageSearchServiceFactory] Got Pexels key from ENV");
                return envKey;
            }
            
            System.Diagnostics.Debug.WriteLine($"[ImageSearchServiceFactory] No Pexels key found!");
            return null;
        }
        
        private static string? GetEffectiveUnsplashKey()
        {
            if (!string.IsNullOrWhiteSpace(_cachedUnsplashKey))
                return _cachedUnsplashKey;
            
            if (!string.IsNullOrWhiteSpace(DefaultApiKeys.UnsplashAccessKey))
                return DefaultApiKeys.UnsplashAccessKey;
            
            var dbKey = LoadApiKeyFromDatabase(ApiServiceIds.Unsplash);
            if (!string.IsNullOrWhiteSpace(dbKey))
            {
                _cachedUnsplashKey = dbKey;
                return dbKey;
            }
            
            return Environment.GetEnvironmentVariable("VETALE_UNSPLASH_ACCESS_KEY");
        }
        
        public static IImageSearchService Create()
        {
            var pexelsKey = GetEffectivePexelsKey();
            var unsplashKey = GetEffectiveUnsplashKey();
            
            if (_cachedService != null && 
                _serviceCreatedWithPexelsKey == pexelsKey && 
                _serviceCreatedWithUnsplashKey == unsplashKey)
            {
                return _cachedService;
            }
            
            System.Diagnostics.Debug.WriteLine($"[ImageSearchServiceFactory] Creating service: Pexels={!string.IsNullOrWhiteSpace(pexelsKey)}, Unsplash={!string.IsNullOrWhiteSpace(unsplashKey)}");

            if (!string.IsNullOrWhiteSpace(pexelsKey) || !string.IsNullOrWhiteSpace(unsplashKey))
            {
                _cachedService = new UnifiedImageSearchService(pexelsKey, unsplashKey);
                _serviceCreatedWithPexelsKey = pexelsKey;
                _serviceCreatedWithUnsplashKey = unsplashKey;
                return _cachedService;
            }

            System.Diagnostics.Debug.WriteLine("[ImageSearchServiceFactory] No API keys, returning MockImageSearchService");
            _cachedService = new MockImageSearchService();
            _serviceCreatedWithPexelsKey = null;
            _serviceCreatedWithUnsplashKey = null;
            return _cachedService;
        }
        
        private static string? LoadApiKeyFromDatabase(string serviceId)
        {
            try
            {
                var apiKeysService = DatabaseServicesFactory.TryGetApiKeysService();
                if (apiKeysService == null)
                    return null;
                
                var key = Task.Run(async () => 
                {
                    try
                    {
                        return await apiKeysService.GetApiKeyAsync(serviceId).ConfigureAwait(false);
                    }
                    catch
                    {
                        return null;
                    }
                }).GetAwaiter().GetResult();
                
                if (!string.IsNullOrWhiteSpace(key))
                {
                    System.Diagnostics.Debug.WriteLine($"[ImageSearchServiceFactory] Loaded {serviceId} from DB");
                    return key;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ImageSearchServiceFactory] Error: {ex.Message}");
            }
            return null;
        }
        
        public static bool HasApiKey(string serviceId)
        {
            var hasDefaultKey = serviceId switch
            {
                ApiServiceIds.Pexels => !string.IsNullOrWhiteSpace(DefaultApiKeys.PexelsApiKey),
                ApiServiceIds.Unsplash => !string.IsNullOrWhiteSpace(DefaultApiKeys.UnsplashAccessKey),
                ApiServiceIds.YouTube => !string.IsNullOrWhiteSpace(DefaultApiKeys.YouTubeApiKey),
                _ => false
            };
            if (hasDefaultKey) return true;
            
            var hasEnvKey = serviceId switch
            {
                ApiServiceIds.Pexels => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VETALE_PEXELS_API_KEY")),
                ApiServiceIds.Unsplash => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VETALE_UNSPLASH_ACCESS_KEY")),
                ApiServiceIds.YouTube => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("YOUTUBE_API_KEY")),
                _ => false
            };
            if (hasEnvKey) return true;
            
            var key = LoadApiKeyFromDatabase(serviceId);
            return !string.IsNullOrWhiteSpace(key);
        }
        
        public static bool HasAnyImageSearchApiKey()
        {
            if (!string.IsNullOrWhiteSpace(DefaultApiKeys.PexelsApiKey) ||
                !string.IsNullOrWhiteSpace(DefaultApiKeys.UnsplashAccessKey))
                return true;
            
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VETALE_PEXELS_API_KEY")) ||
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VETALE_UNSPLASH_ACCESS_KEY")))
                return true;
            
            return HasApiKey(ApiServiceIds.Pexels) || HasApiKey(ApiServiceIds.Unsplash);
        }
        
        public static string? GetYouTubeApiKey()
        {
            if (!string.IsNullOrWhiteSpace(_cachedYouTubeKey))
                return _cachedYouTubeKey;
            
            if (!string.IsNullOrWhiteSpace(DefaultApiKeys.YouTubeApiKey))
                return DefaultApiKeys.YouTubeApiKey;
            
            var dbKey = LoadApiKeyFromDatabase(ApiServiceIds.YouTube);
            if (!string.IsNullOrWhiteSpace(dbKey))
            {
                _cachedYouTubeKey = dbKey;
                return dbKey;
            }
            
            return Environment.GetEnvironmentVariable("YOUTUBE_API_KEY");
        }
    }

    /// <summary>
    /// Мок-реалізація для тестування без API ключів
    /// </summary>
    public class MockImageSearchService : IImageSearchService
    {
        public Task<ImageSearchPage> SearchAsync(
            string query,
            int page = 1,
            int perPage = 30,
            ImageSearchFilter? filter = null,
            CancellationToken cancellationToken = default)
        {
            var results = new List<ImageSearchResult>();
            
            for (int i = 0; i < perPage; i++)
            {
                var index = (page - 1) * perPage + i + 1;
                results.Add(new ImageSearchResult
                {
                    Id = index.ToString(),
                    Provider = "Mock",
                    Source = ImageSource.Mock,
                    Title = $"{query} - demo image {index}",
                    Description = $"Demo image for '{query}'",
                    ThumbnailUrl = "https://via.placeholder.com/150",
                    MediumUrl = "https://via.placeholder.com/600",
                    LargeUrl = "https://via.placeholder.com/1200",
                    OriginalUrl = "https://via.placeholder.com/1920",
                    ImageUrl = "https://via.placeholder.com/600",
                    PhotographerName = "Demo",
                    Photographer = "Demo",
                    PhotographerUrl = "https://example.com",
                    SourcePageUrl = "https://example.com",
                    Urls = new ImageUrlSet
                    {
                        ThumbUrl = "https://via.placeholder.com/150",
                        SmallUrl = "https://via.placeholder.com/300",
                        RegularUrl = "https://via.placeholder.com/600",
                        FullUrl = "https://via.placeholder.com/1200"
                    },
                    Width = 1200,
                    Height = 800,
                    Color = "#CCCCCC",
                    AverageColor = "#CCCCCC"
                });
            }

            return Task.FromResult(new ImageSearchPage
            {
                Query = query,
                Page = page,
                PerPage = perPage,
                PageNumber = page,
                PageSize = perPage,
                TotalResults = 1000,
                HasNextPage = page * perPage < 1000,
                SearchTime = 0.1,
                Provider = "Mock",
                Results = results
            });
        }

        public Task<ImageSearchPage> GetCuratedAsync(
            int page = 1,
            int perPage = 30,
            CancellationToken cancellationToken = default)
        {
            return SearchAsync("curated", page, perPage, null, cancellationToken);
        }
    }
}
