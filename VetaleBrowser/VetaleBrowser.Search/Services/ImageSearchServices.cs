using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Search.Models;

namespace VetaleBrowser.VetaleBrowser.Search.Services
{
    // ================== КОНФІГУРАЦІЯ IMAGE SEARCH API КЛЮЧІВ ==================
    // ВАРІАНТ 1 (ШВИДКИЙ ЛОКАЛЬНИЙ ТЕСТ): вставити ключі прямо в константи нижче.
    //    НЕ ЗАЛИШАЙ ЇХ У РЕПО ДЛЯ PROD! Видаляй перед комітом або використовуй env.
    // private const string PEXELS_API_KEY = "ВСТАВ_СЮДИ_СВІЙ_PEXELS_KEY"; // ← заміни рядок на реальний ключ
    // private const string UNSPLASH_ACCESS_KEY = "ВСТАВ_СЮДИ_СВІЙ_UNSPLASH_KEY"; // ← заміни рядок на реальний ключ
    // 
    // ВАРІАНТ 2 (РЕКОМЕНДОВАНО): задати ключі через змінні середовища:
    //   VETALE_PEXELS_API_KEY      = your_pexels_key
    //   VETALE_UNSPLASH_ACCESS_KEY = your_unsplash_key
    // 
    // Потім фабрика нижче сама їх прочитає. Якщо ключів немає — автоматично використає Mock.
    // ===========================================================================

    public static class ImageSearchServiceFactory
    {
        /// <summary>
        /// Створює сервіс пошуку зображень, автоматично підхоплюючи ключі з env.
        /// Якщо ключі відсутні — повертає UnifiedImageSearchService без провайдерів і він впаде назад на Mock.
        /// </summary>
        public static IImageSearchService Create()
        {
            // Спроба прочитати ключі з середовища
            var pexelsKey = Environment.GetEnvironmentVariable("zGnL0MfSCjjLbq7KrOgtVXKoDs0TX1wHMCZoDKJtgt4KWGY37CNVZIbj");
            var unsplashKey = Environment.GetEnvironmentVariable("-_hfnNlMh9CQ-dojmvWqkathOaIqZFpmAk-Tdv6uuzc");

            // Явні коментарі: якщо хочеш жорстко вписати ключі тимчасово — розкоментуй наступні рядки:
            // pexelsKey = pexelsKey ?? PEXELS_API_KEY;          // ← прибери // якщо додав константу вище
            // unsplashKey = unsplashKey ?? UNSPLASH_ACCESS_KEY; // ← прибери // якщо додав константу вище

            return new UnifiedImageSearchService(pexelsKey, unsplashKey);
        }
    }

    public interface IImageSearchProvider
    {
        Task<ImageSearchPage> SearchAsync(ImageSearchQuery query, CancellationToken ct);
    }

    public interface IImageSearchService
    {
        Task<ImageSearchPage> SearchAsync(string query, int page, int pageSize, CancellationToken ct);
    }

    /// <summary>
    /// Об'єднаний сервіс пошуку зображень, який використовує Pexels і Unsplash.
    /// </summary>
    public class UnifiedImageSearchService : IImageSearchService
    {
        private readonly List<IImageSearchProvider> _providers = new();

        public UnifiedImageSearchService(string? pexelsApiKey = null, string? unsplashAccessKey = null)
        {
            // Додаємо провайдери якщо є ключі
            if (!string.IsNullOrWhiteSpace(pexelsApiKey))
            {
                _providers.Add(new PexelsImageSearchProvider(pexelsApiKey));
            }
            
            if (!string.IsNullOrWhiteSpace(unsplashAccessKey))
            {
                _providers.Add(new UnsplashImageSearchProvider(unsplashAccessKey));
            }

            // Якщо немає ключів - використовуємо mock
            if (_providers.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[UnifiedImageSearchService] No API keys provided, using mock provider");
            }
        }

        public async Task<ImageSearchPage> SearchAsync(string query, int page, int pageSize, CancellationToken ct)
        {
            if (_providers.Count == 0)
            {
                // Fallback на mock якщо немає провайдерів
                return await new MockImageSearchService().SearchAsync(query, page, pageSize, ct);
            }

            // Використовуємо перший доступний провайдер (можна розширити логіку)
            var provider = _providers[0];
            var searchQuery = new ImageSearchQuery
            {
                Query = query,
                PageNumber = page,
                PageSize = pageSize
            };

            return await provider.SearchAsync(searchQuery, ct);
        }
    }

    /// <summary>
    /// Мок-реалізація для тестування без API ключів.
    /// ПРИМІТКА: Використовується як fallback, якщо фабрика не знайде ключі.
    /// </summary>
    public class MockImageSearchService : IImageSearchService
    {
        public Task<ImageSearchPage> SearchAsync(string query, int page, int pageSize, CancellationToken ct)
        {
            var results = new ImageSearchResult[pageSize];
            for (int i = 0; i < pageSize; i++)
            {
                var index = (page - 1) * pageSize + i + 1;
                results[i] = new ImageSearchResult
                {
                    Id = index.ToString(),
                    Provider = "Mock",
                    Title = $"{query} - демо зображення {index}",
                    PhotographerName = "Demo Photographer",
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
                    Color = "#CCCCCC"
                };
            }

            var pageResult = new ImageSearchPage
            {
                Query = query,
                PageNumber = page,
                PageSize = pageSize,
                TotalResults = 1000,
                HasNextPage = page * pageSize < 1000,
                Provider = "Mock",
                Results = results
            };

            return Task.FromResult(pageResult);
        }
    }
}
