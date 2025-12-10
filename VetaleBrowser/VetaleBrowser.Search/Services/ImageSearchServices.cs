using System;
using System.Collections.Generic;
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
        /// Створює сервіс пошуку зображень, автоматично підхоплюючи ключі з env.
        /// Якщо ключі відсутні — повертає MockImageSearchService.
        /// </summary>
        public static IImageSearchService Create()
        {
            // Спроба прочитати ключі з середовища
            var pexelsKey = Environment.GetEnvironmentVariable("VETALE_PEXELS_API_KEY");
            var unsplashKey = Environment.GetEnvironmentVariable("VETALE_UNSPLASH_ACCESS_KEY");

            // Явні коментарі: якщо хочеш жорстко вписати ключі тимчасово — розкоментуй наступні рядки:
            // pexelsKey = pexelsKey ?? PEXELS_API_KEY;          // ← прибери // якщо додав константу вище
            // unsplashKey = unsplashKey ?? UNSPLASH_ACCESS_KEY; // ← прибери // якщо додав константу вище

            // Якщо є ключі - повертаємо UnifiedImageSearchService
            if (!string.IsNullOrWhiteSpace(pexelsKey) || !string.IsNullOrWhiteSpace(unsplashKey))
            {
                return new UnifiedImageSearchService(pexelsKey, unsplashKey);
            }

            // Інакше - повертаємо mock
            return new MockImageSearchService();
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
