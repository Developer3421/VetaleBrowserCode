using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Search.Models;
using VetaleBrowser.VetaleBrowser.Database.Services;

namespace VetaleBrowser.VetaleBrowser.Search.Services
{
    // ================== КОНФІГУРАЦІЯ API КЛЮЧІВ ДЛЯ ПОШУКУ ==================
    // 
    // ⚠️ ДЕФОЛТНІ API КЛЮЧІ - ВСТАВТЕ СВОЇ КЛЮЧІ НИЖЧЕ:
    // 
    // Пріоритет завантаження ключів:
    //   1. Дефолтні константи (нижче)
    //   2. База даних (api_keys.db) - якщо користувач ввів свій ключ
    //   3. Змінні середовища
    //   4. MockImageSearchService (якщо ключів немає)
    // ===========================================================================
    
    /// <summary>
    /// Дефолтні API ключі для пошуку
    /// </summary>
    public static class DefaultApiKeys
    {
        // =====================================================================
        // 🔑 PEXELS API KEY
        // Отримати: https://www.pexels.com/api/
        // Безкоштовно: 200 запитів/годину, 20,000 запитів/місяць
        // =====================================================================
        public const string PexelsApiKey = "zGnL0MfSCjjLbq7KrOgtVXKoDs0TX1wHMCZoDKJtgt4KWGY37CNVZIbj"; // ← ВСТАВТЕ ВАШ PEXELS API KEY ТУТ
        
        // =====================================================================
        // 🔑 UNSPLASH ACCESS KEY
        // Отримати: https://unsplash.com/developers
        // Безкоштовно: 50 запитів/годину для демо, необмежено для production
        // =====================================================================
        public const string UnsplashAccessKey = "-_hfnNlMh9CQ-dojmvWqkathOaIqZFpmAk-Tdv6uuzc"; // ← ВСТАВТЕ ВАШ UNSPLASH ACCESS KEY ТУТ
        
        // =====================================================================
        // 🔑 YOUTUBE DATA API KEY
        // Отримати: https://console.cloud.google.com/apis/library/youtube.googleapis.com
        // Безкоштовно: 10,000 одиниць/день
        // =====================================================================
        public const string YouTubeApiKey = "AIzaSyD3WKiUcX1vdclWhLBFm3s8l9Z2bDGZiWs"; // ← ВСТАВТЕ ВАШ YOUTUBE API KEY ТУТ
    }

    /// <summary>
    /// Інтерфейс для провайдерів пошуку зображень (legacy, для сумісності з Provider класами)
    /// </summary>
    public interface IImageSearchProvider
    {
        Task<ImageSearchPage> SearchAsync(ImageSearchQuery query, CancellationToken ct);
    }

    public static class ImageSearchServiceFactory
    {
        /// <summary>
        /// Максимальна кількість результатів на сторінку
        /// </summary>
        public const int MaxResultsPerPage = 50;
        
        /// <summary>
        /// Створює сервіс пошуку зображень, підхоплюючи ключі з різних джерел.
        /// </summary>
        public static IImageSearchService Create()
        {
            // 1. Дефолтні ключі з констант
            var pexelsKey = DefaultApiKeys.PexelsApiKey;
            var unsplashKey = DefaultApiKeys.UnsplashAccessKey;
            
            // 2. Якщо дефолтні порожні - пробуємо БД
            if (string.IsNullOrWhiteSpace(pexelsKey))
            {
                pexelsKey = LoadApiKeyFromDatabase(ApiServiceIds.Pexels);
            }
            if (string.IsNullOrWhiteSpace(unsplashKey))
            {
                unsplashKey = LoadApiKeyFromDatabase(ApiServiceIds.Unsplash);
            }
            
            // 3. Fallback на змінні середовища
            if (string.IsNullOrWhiteSpace(pexelsKey))
            {
                pexelsKey = Environment.GetEnvironmentVariable("VETALE_PEXELS_API_KEY");
            }
            if (string.IsNullOrWhiteSpace(unsplashKey))
            {
                unsplashKey = Environment.GetEnvironmentVariable("VETALE_UNSPLASH_ACCESS_KEY");
            }

            // Якщо є ключі - повертаємо UnifiedImageSearchService
            if (!string.IsNullOrWhiteSpace(pexelsKey) || !string.IsNullOrWhiteSpace(unsplashKey))
            {
                System.Diagnostics.Debug.WriteLine($"[ImageSearchServiceFactory] Creating UnifiedImageSearchService with Pexels={!string.IsNullOrWhiteSpace(pexelsKey)}, Unsplash={!string.IsNullOrWhiteSpace(unsplashKey)}");
                return new UnifiedImageSearchService(pexelsKey, unsplashKey);
            }

            // Інакше - повертаємо mock
            System.Diagnostics.Debug.WriteLine("[ImageSearchServiceFactory] No API keys found, returning MockImageSearchService");
            return new MockImageSearchService();
        }
        
        /// <summary>
        /// Завантажує API ключ з бази даних
        /// </summary>
        private static string? LoadApiKeyFromDatabase(string serviceId)
        {
            try
            {
                var apiKeysService = DatabaseServicesFactory.TryGetApiKeysService();
                if (apiKeysService != null)
                {
                    // Використовуємо Task.Run щоб уникнути deadlock в UI потоці
                    var key = Task.Run(async () => await apiKeysService.GetApiKeyAsync(serviceId).ConfigureAwait(false)).GetAwaiter().GetResult();
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        System.Diagnostics.Debug.WriteLine($"[ImageSearchServiceFactory] Loaded {serviceId} API key from database");
                        return key;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ImageSearchServiceFactory] Error loading {serviceId} API key: {ex.Message}");
            }
            return null;
        }
        
        /// <summary>
        /// Перевіряє чи є API ключ для сервісу (швидка перевірка)
        /// </summary>
        public static bool HasApiKey(string serviceId)
        {
            // 1. Перевіряємо дефолтні ключі (найшвидше)
            var hasDefaultKey = serviceId switch
            {
                ApiServiceIds.Pexels => !string.IsNullOrWhiteSpace(DefaultApiKeys.PexelsApiKey),
                ApiServiceIds.Unsplash => !string.IsNullOrWhiteSpace(DefaultApiKeys.UnsplashAccessKey),
                ApiServiceIds.YouTube => !string.IsNullOrWhiteSpace(DefaultApiKeys.YouTubeApiKey),
                _ => false
            };
            if (hasDefaultKey) return true;
            
            // 2. Перевіряємо ENV (швидко)
            var hasEnvKey = serviceId switch
            {
                ApiServiceIds.Pexels => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VETALE_PEXELS_API_KEY")),
                ApiServiceIds.Unsplash => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VETALE_UNSPLASH_ACCESS_KEY")),
                ApiServiceIds.YouTube => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("YOUTUBE_API_KEY")),
                _ => false
            };
            if (hasEnvKey) return true;
            
            // 3. Перевіряємо БД (може бути повільно)
            var key = LoadApiKeyFromDatabase(serviceId);
            return !string.IsNullOrWhiteSpace(key);
        }
        
        /// <summary>
        /// Перевіряє чи є хоча б один API ключ для пошуку зображень
        /// </summary>
        public static bool HasAnyImageSearchApiKey()
        {
            // 1. Дефолтні ключі
            if (!string.IsNullOrWhiteSpace(DefaultApiKeys.PexelsApiKey) ||
                !string.IsNullOrWhiteSpace(DefaultApiKeys.UnsplashAccessKey))
            {
                return true;
            }
            
            // 2. Швидка перевірка ENV
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VETALE_PEXELS_API_KEY")) ||
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VETALE_UNSPLASH_ACCESS_KEY")))
            {
                return true;
            }
            
            // 3. Повільна перевірка БД
            return HasApiKey(ApiServiceIds.Pexels) || HasApiKey(ApiServiceIds.Unsplash);
        }
        
        /// <summary>
        /// Отримує YouTube API ключ
        /// </summary>
        public static string? GetYouTubeApiKey()
        {
            // 1. Дефолтний
            if (!string.IsNullOrWhiteSpace(DefaultApiKeys.YouTubeApiKey))
                return DefaultApiKeys.YouTubeApiKey;
            
            // 2. БД
            var dbKey = LoadApiKeyFromDatabase(ApiServiceIds.YouTube);
            if (!string.IsNullOrWhiteSpace(dbKey))
                return dbKey;
            
            // 3. ENV
            return Environment.GetEnvironmentVariable("YOUTUBE_API_KEY");
        }
    }

    /// <summary>
    /// Мок-реалізація для тестування без API ключів.
    /// ПРИМІТКА: Використовується як fallback, якщо фабрика не знайде ключі.
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
                    Title = $"{query} - демо зображення {index}",
                    Description = $"Демонстраційне зображення для запиту '{query}'",
                    ThumbnailUrl = "https://via.placeholder.com/150",
                    MediumUrl = "https://via.placeholder.com/600",
                    LargeUrl = "https://via.placeholder.com/1200",
                    OriginalUrl = "https://via.placeholder.com/1920",
                    ImageUrl = "https://via.placeholder.com/600",
                    PhotographerName = "Demo Photographer",
                    Photographer = "Demo Photographer",
                    PhotographerUrl = "https://example.com/photographer",
                    SourcePageUrl = "https://example.com/image",
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

            var pageResult = new ImageSearchPage
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
            };

            return Task.FromResult(pageResult);
        }

        public Task<ImageSearchPage> GetCuratedAsync(
            int page = 1,
            int perPage = 30,
            CancellationToken cancellationToken = default)
        {
            // Повертаємо куровані зображення (просто використовуємо SearchAsync з запитом "curated")
            return SearchAsync("curated", page, perPage, null, cancellationToken);
        }
    }
}
