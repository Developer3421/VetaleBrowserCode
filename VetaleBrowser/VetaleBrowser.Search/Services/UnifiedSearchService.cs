using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Search.Database;
using VetaleBrowser.VetaleBrowser.Search.Models;

namespace VetaleBrowser.VetaleBrowser.Search.Services;

/// <summary>
/// Сервіс уніфікованого пошуку по Wikipedia, WebArchive, MetaSearx та інших джерелах,
/// який агрегує результати в сторінки по 50 записів (як Google/Bing).
/// </summary>
public interface IUnifiedSearchService
{
    /// <summary>
    /// Пошук з пагінацією
    /// </summary>
    /// <param name="query">Пошуковий запит</param>
    /// <param name="pageNumber">Номер сторінки (1-based)</param>
    /// <param name="pageSize">Кількість результатів на сторінку (за замовчуванням 50)</param>
    /// <param name="ct">Токен скасування</param>
    Task<UnifiedSearchPage> SearchAsync(string query, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
}

public sealed class UnifiedSearchService : IUnifiedSearchService
{
    private readonly IWikipediaSearchClient _wikipedia;
    private readonly IWebArchiveSearchClient _webArchive;
    private readonly IMetaSearxSearchClient _metaSearx;
    private readonly Func<ISearchSessionStore> _storeFactory;
    
    // Кількість результатів на сторінку (як у Google)
    private const int DefaultPageSize = 50;

    public UnifiedSearchService()
        : this(new WikipediaSearchClient(), new WebArchiveSearchClient(), new MetaSearxSearchClient(), () => new JsonFileSearchSessionStore())
    {
    }

    public UnifiedSearchService(
        IWikipediaSearchClient wikipedia,
        IWebArchiveSearchClient webArchive,
        IMetaSearxSearchClient metaSearx,
        Func<ISearchSessionStore> storeFactory)
    {
        _wikipedia = wikipedia ?? throw new ArgumentNullException(nameof(wikipedia));
        _webArchive = webArchive ?? throw new ArgumentNullException(nameof(webArchive));
        _metaSearx = metaSearx ?? throw new ArgumentNullException(nameof(metaSearx));
        _storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
    }

    public async Task<UnifiedSearchPage> SearchAsync(string query, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new UnifiedSearchPage
            {
                SearchSessionId = Guid.NewGuid(),
                PageNumber = 1,
                PageSize = pageSize,
                TotalResults = 0,
                Results = Array.Empty<UnifiedSearchResult>(),
                HasNextPage = false,
                HasPreviousPage = false
            };
        }

        var store = _storeFactory();
        var results = new List<UnifiedSearchResult>();
        var hasNextPage = false;

        // На першій сторінці показуємо Wikipedia, WebArchive, YouTube + MetaSearx
        // На наступних сторінках - тільки MetaSearx
        if (pageNumber == 1)
        {
            // Паралельно тягнемо Wikipedia, WebArchive та MetaSearx (перша сторінка)
            var wikiTask = _wikipedia.SearchTopAsync(query, maxResults: 1, ct);
            var webArchiveTask = _webArchive.SearchTodayAsync(query, maxResults: 1, ct);
            var metaSearxTask = _metaSearx.SearchAsync(query, page: 1, resultsPerPage: pageSize - 3, ct);

            await Task.WhenAll(wikiTask, webArchiveTask, metaSearxTask);

            // 1. Wikipedia (енциклопедія)
            if (wikiTask.Result is { Count: > 0 })
            {
                foreach (var r in wikiTask.Result)
                {
                    r.Source = SearchSourceType.Wikipedia;
                    r.PageNumber = pageNumber;
                    results.Add(r);
                }
            }

            // 2. WebArchive (архів інтернету)
            if (webArchiveTask.Result is { Count: > 0 })
            {
                foreach (var r in webArchiveTask.Result)
                {
                    r.Source = SearchSourceType.WebArchive;
                    r.PageNumber = pageNumber;
                    results.Add(r);
                }
            }

            // 3. YouTube (посилання на відеопошук)
            var youtubeTitleTemplate = SearchLocalization.Get(
                "Search.Redirect.YouTube.Title",
                "🎬 Videos on YouTube: \"{0}\"");
            var youtubeSnippetTemplate = SearchLocalization.Get(
                "Search.Redirect.YouTube.Snippet",
                "View YouTube video search results for \"{0}\".");

            string SafeFormat(string template, string arg)
            {
                try { return string.Format(template, arg); }
                catch { return template.Replace("{0}", arg); }
            }

            results.Add(new UnifiedSearchResult
            {
                Title = SafeFormat(youtubeTitleTemplate, query),
                Url = $"https://www.youtube.com/results?search_query={Uri.EscapeDataString(query)}",
                DisplayUrl = "youtube.com › results",
                Snippet = SafeFormat(youtubeSnippetTemplate, query),
                Source = SearchSourceType.YouTube,
                Timestamp = null,
                RankScore = 0,
                PageNumber = pageNumber
            });

            // 4. MetaSearx результати
            if (metaSearxTask.Result.Results.Count > 0)
            {
                foreach (var r in metaSearxTask.Result.Results)
                {
                    r.Source = SearchSourceType.MetaSearx;
                    r.PageNumber = pageNumber;
                    results.Add(r);
                }
            }
            
            hasNextPage = metaSearxTask.Result.HasNextPage;
        }
        else
        {
            // Для сторінок 2+ - тільки результати з MetaSearx
            var metaSearxResult = await _metaSearx.SearchAsync(query, page: pageNumber, resultsPerPage: pageSize, ct);
            
            foreach (var r in metaSearxResult.Results)
            {
                r.Source = SearchSourceType.MetaSearx;
                r.PageNumber = pageNumber;
                results.Add(r);
            }
            
            hasNextPage = metaSearxResult.HasNextPage;
        }

        // Проставляємо RankScore для правильного сортування
        for (int i = 0; i < results.Count; i++)
        {
            results[i].RankScore = results.Count - i;
        }

        var page = new UnifiedSearchPage
        {
            SearchSessionId = store.SessionId,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalResults = results.Count,
            Results = results.ToArray(),
            HasNextPage = hasNextPage,
            HasPreviousPage = pageNumber > 1
        };

        await store.SavePageAsync(page, ct);

        return page;
    }
}
