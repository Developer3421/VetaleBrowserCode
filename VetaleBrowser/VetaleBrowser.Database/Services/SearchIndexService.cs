using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LiteDB;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Сервіс для роботи з локальним пошуковим індексом Vetale Search
/// </summary>
public class SearchIndexService : ISearchIndexService, IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<SearchIndex> _indexCollection;
    private readonly ILiteCollection<SearchQuery> _queryCollection;
    private readonly ILiteCollection<SearchKeyword> _keywordCollection;
    private readonly string _databasePath;

    private const int MaxIndexItems = 50000;
    private const long MaxDatabaseSizeBytes = 1024 * 1024 * 1024; // 1 GB

    public SearchIndexService(string databasePath)
    {
        _databasePath = databasePath;

        // Створюємо директорію для бази даних якщо не існує
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Ініціалізуємо базу даних
        var connectionString = new ConnectionString
        {
            Filename = databasePath,
            Connection = ConnectionType.Shared
        };

        _database = new LiteDatabase(connectionString);

        // Отримуємо колекції
        _indexCollection = _database.GetCollection<SearchIndex>("search_index");
        _queryCollection = _database.GetCollection<SearchQuery>("search_queries");
        _keywordCollection = _database.GetCollection<SearchKeyword>("search_keywords");

        // Створюємо індекси для оптимізації
        _indexCollection.EnsureIndex(x => x.Url);
        _indexCollection.EnsureIndex(x => x.Title);
        _indexCollection.EnsureIndex(x => x.IndexedAt);
        _indexCollection.EnsureIndex(x => x.RelevanceScore);
        
        _queryCollection.EnsureIndex(x => x.Query);
        _queryCollection.EnsureIndex(x => x.SearchedAt);
        
        _keywordCollection.EnsureIndex(x => x.SearchIndexId);
        _keywordCollection.EnsureIndex(x => x.Keyword);
    }

    public async Task<bool> IndexPageAsync(string url, string title, string content, string description, string keywords)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Перевіряємо чи сторінка вже існує
                var existing = _indexCollection.FindOne(x => x.Url == url);
                if (existing != null)
                {
                    return UpdateIndexAsync(url, title, content, description, keywords).Result;
                }

                // Перевіряємо ліміт
                if (_indexCollection.Count() >= MaxIndexItems)
                {
                    // Видаляємо найстаріші записи
                    var oldestItems = _indexCollection
                        .Query()
                        .OrderBy(x => x.LastVisitedAt)
                        .Limit(1000)
                        .ToList();

                    foreach (var item in oldestItems)
                    {
                        _indexCollection.Delete(item.Id);
                        _keywordCollection.DeleteMany(x => x.SearchIndexId == item.Id);
                    }
                }

                // Створюємо новий запис
                var searchIndex = new SearchIndex
                {
                    Url = url,
                    Title = title ?? string.Empty,
                    Content = CleanContent(content),
                    Description = description ?? string.Empty,
                    Keywords = keywords ?? string.Empty,
                    IndexedAt = DateTime.UtcNow,
                    LastVisitedAt = DateTime.UtcNow,
                    VisitCount = 1,
                    RelevanceScore = CalculateRelevanceScore(title, content, 1)
                };

                var id = _indexCollection.Insert(searchIndex);

                // Індексуємо ключові слова
                IndexKeywords(id, title, content);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SearchIndexService: Error indexing page: {ex}");
                return false;
            }
        });
    }

    public async Task<bool> UpdateIndexAsync(string url, string title, string content, string description, string keywords)
    {
        return await Task.Run(() =>
        {
            try
            {
                var existing = _indexCollection.FindOne(x => x.Url == url);
                if (existing == null)
                {
                    return IndexPageAsync(url, title, content, description, keywords).Result;
                }

                // Оновлюємо дані
                existing.Title = title ?? existing.Title;
                existing.Content = CleanContent(content);
                existing.Description = description ?? existing.Description;
                existing.Keywords = keywords ?? existing.Keywords;
                existing.LastVisitedAt = DateTime.UtcNow;
                existing.VisitCount++;
                existing.RelevanceScore = CalculateRelevanceScore(title, content, existing.VisitCount);

                _indexCollection.Update(existing);

                // Видаляємо старі ключові слова і додаємо нові
                _keywordCollection.DeleteMany(x => x.SearchIndexId == existing.Id);
                IndexKeywords(existing.Id, title, content);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SearchIndexService: Error updating index: {ex}");
                return false;
            }
        });
    }

    public async Task<bool> RemoveFromIndexAsync(string url)
    {
        return await Task.Run(() =>
        {
            try
            {
                var item = _indexCollection.FindOne(x => x.Url == url);
                if (item != null)
                {
                    _keywordCollection.DeleteMany(x => x.SearchIndexId == item.Id);
                    _indexCollection.Delete(item.Id);
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SearchIndexService: Error removing from index: {ex}");
                return false;
            }
        });
    }

    public async Task<List<SearchIndex>> SearchAsync(string query, int maxResults = 50)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                    return new List<SearchIndex>();

                var searchTerms = query.ToLowerInvariant()
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                // Пошук за всіма полями
                var results = _indexCollection.FindAll()
                    .Select(item => new
                    {
                        Item = item,
                        Score = CalculateSearchScore(item, searchTerms)
                    })
                    .Where(x => x.Score > 0)
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.Item.RelevanceScore)
                    .ThenByDescending(x => x.Item.LastVisitedAt)
                    .Take(maxResults)
                    .Select(x => x.Item)
                    .ToList();

                return results;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SearchIndexService: Error searching: {ex}");
                return new List<SearchIndex>();
            }
        });
    }

    public async Task<bool> SaveSearchQueryAsync(string query, string searchEngine, int resultsCount)
    {
        return await Task.Run(() =>
        {
            try
            {
                var searchQuery = new SearchQuery
                {
                    Query = query,
                    SearchEngine = searchEngine,
                    SearchedAt = DateTime.UtcNow,
                    ResultsCount = resultsCount
                };

                _queryCollection.Insert(searchQuery);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SearchIndexService: Error saving query: {ex}");
                return false;
            }
        });
    }

    public async Task<List<SearchQuery>> GetSearchHistoryAsync(int limit = 100)
    {
        return await Task.Run(() =>
        {
            try
            {
                return _queryCollection
                    .Query()
                    .OrderByDescending(x => x.SearchedAt)
                    .Limit(limit)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SearchIndexService: Error getting history: {ex}");
                return new List<SearchQuery>();
            }
        });
    }

    public async Task<bool> ClearSearchHistoryAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                _queryCollection.DeleteAll();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SearchIndexService: Error clearing history: {ex}");
                return false;
            }
        });
    }

    public async Task<List<string>> GetPopularQueriesAsync(int limit = 10)
    {
        return await Task.Run(() =>
        {
            try
            {
                return _queryCollection.FindAll()
                    .GroupBy(x => x.Query.ToLowerInvariant())
                    .OrderByDescending(g => g.Count())
                    .Take(limit)
                    .Select(g => g.First().Query)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SearchIndexService: Error getting popular queries: {ex}");
                return new List<string>();
            }
        });
    }

    public async Task<List<string>> GetAutocompleteSuggestionsAsync(string partialQuery, int limit = 10)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(partialQuery))
                    return new List<string>();

                var lowerQuery = partialQuery.ToLowerInvariant();

                // Пошук у попередніх запитах
                var suggestions = _queryCollection.FindAll()
                    .Where(x => x.Query.ToLowerInvariant().StartsWith(lowerQuery))
                    .GroupBy(x => x.Query.ToLowerInvariant())
                    .OrderByDescending(g => g.Count())
                    .Take(limit)
                    .Select(g => g.First().Query)
                    .ToList();

                return suggestions;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SearchIndexService: Error getting autocomplete: {ex}");
                return new List<string>();
            }
        });
    }

    public async Task<bool> ClearIndexAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                _indexCollection.DeleteAll();
                _keywordCollection.DeleteAll();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SearchIndexService: Error clearing index: {ex}");
                return false;
            }
        });
    }

    public async Task<(int TotalPages, int TotalKeywords, DateTime? LastIndexed)> GetIndexStatisticsAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var totalPages = _indexCollection.Count();
                var totalKeywords = _keywordCollection.Count();
                var lastIndexed = _indexCollection
                    .Query()
                    .OrderByDescending(x => x.IndexedAt)
                    .Select(x => x.IndexedAt)
                    .FirstOrDefault();

                return (totalPages, totalKeywords, lastIndexed == default ? null : (DateTime?)lastIndexed);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SearchIndexService: Error getting statistics: {ex}");
                return (0, 0, null);
            }
        });
    }

    private void IndexKeywords(int searchIndexId, string title, string content)
    {
        var allText = $"{title} {content}";
        var words = Regex.Split(allText.ToLowerInvariant(), @"\W+")
            .Where(w => w.Length > 2) // Ігноруємо короткі слова
            .GroupBy(w => w)
            .Select(g => new { Word = g.Key, Count = g.Count() });

        foreach (var wordGroup in words.Take(100)) // Обмежуємо кількість ключових слів
        {
            var weight = title.ToLowerInvariant().Contains(wordGroup.Word) ? 2.0 : 1.0;

            var keyword = new SearchKeyword
            {
                SearchIndexId = searchIndexId,
                Keyword = wordGroup.Word,
                Frequency = wordGroup.Count,
                Weight = weight
            };

            _keywordCollection.Insert(keyword);
        }
    }

    private int CalculateSearchScore(SearchIndex item, string[] searchTerms)
    {
        int score = 0;
        var titleLower = item.Title.ToLowerInvariant();
        var contentLower = item.Content.ToLowerInvariant();
        var descriptionLower = item.Description.ToLowerInvariant();

        foreach (var term in searchTerms)
        {
            // Точне співпадіння в заголовку - найбільша вага
            if (titleLower.Contains(term))
                score += 10;

            // Співпадіння в описі
            if (descriptionLower.Contains(term))
                score += 5;

            // Співпадіння в контенті
            if (contentLower.Contains(term))
                score += 2;

            // Ключові слова
            if (item.Keywords.ToLowerInvariant().Contains(term))
                score += 3;
        }

        return score;
    }

    private int CalculateRelevanceScore(string title, string content, int visitCount)
    {
        int score = visitCount * 10; // База - кількість відвідувань

        // Додаємо бали за довжину заголовка (більш інформативні заголовки)
        if (!string.IsNullOrEmpty(title) && title.Length > 10)
            score += 5;

        // Додаємо бали за наявність контенту
        if (!string.IsNullOrEmpty(content) && content.Length > 100)
            score += 10;

        return score;
    }

    private string CleanContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        // Видаляємо HTML теги
        var cleaned = Regex.Replace(content, @"<[^>]+>", " ");
        
        // Видаляємо зайві пробіли
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        
        // Обмежуємо довжину (зберігаємо перші 5000 символів)
        if (cleaned.Length > 5000)
            cleaned = cleaned.Substring(0, 5000);

        return cleaned.Trim();
    }

    public void Dispose()
    {
        _database?.Dispose();
    }
}

