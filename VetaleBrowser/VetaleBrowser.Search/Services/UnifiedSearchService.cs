using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Search.Database;
using VetaleBrowser.VetaleBrowser.Search.Models;

namespace VetaleBrowser.VetaleBrowser.Search.Services;

/// <summary>
/// Сервіс уніфікованого пошуку по Wikipedia, WebArchive, (надалі CommonCrawl),
/// який агрегує результати в одну сторінку й зберігає їх у тимчасове сховище.
/// </summary>
public interface IUnifiedSearchService
{
    Task<UnifiedSearchPage> SearchAsync(string query, int pageNumber, int pageSize, CancellationToken ct = default);
}

public sealed class UnifiedSearchService : IUnifiedSearchService
{
    private readonly IWikipediaSearchClient _wikipedia;
    private readonly IWebArchiveSearchClient _webArchive;
    private readonly Func<ISearchSessionStore> _storeFactory;

    public UnifiedSearchService()
        : this(new WikipediaSearchClient(), new WebArchiveSearchClient(), () => new JsonFileSearchSessionStore())
    {
    }

    public UnifiedSearchService(
        IWikipediaSearchClient wikipedia,
        IWebArchiveSearchClient webArchive,
        Func<ISearchSessionStore> storeFactory)
    {
        _wikipedia = wikipedia ?? throw new ArgumentNullException(nameof(wikipedia));
        _webArchive = webArchive ?? throw new ArgumentNullException(nameof(webArchive));
        _storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
    }

    public async Task<UnifiedSearchPage> SearchAsync(string query, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new UnifiedSearchPage
            {
                SearchSessionId = Guid.NewGuid(),
                PageNumber = 1,
                PageSize = pageSize,
                TotalResults = 0,
                Results = Array.Empty<UnifiedSearchResult>()
            };
        }

        var store = _storeFactory();

        // Паралельно тягнемо Wikipedia (1 результат) і WebArchive (посилання на пошук)
        var wikiTask = _wikipedia.SearchTopAsync(query, maxResults: 1, ct);
        var webArchiveTask = _webArchive.SearchTodayAsync(query, maxResults: 1, ct);

        await Task.WhenAll(wikiTask, webArchiveTask);

        var results = new List<UnifiedSearchResult>();

        if (wikiTask.Result is { Count: > 0 })
        {
            foreach (var r in wikiTask.Result)
            {
                r.Source = SearchSourceType.Wikipedia;
                results.Add(r);
            }
        }

        if (webArchiveTask.Result is { Count: > 0 })
        {
            foreach (var r in webArchiveTask.Result)
            {
                r.Source = SearchSourceType.WebArchive;
                results.Add(r);
            }
        }

        // TODO: сюди буде додано CommonCrawl (ще +5 результатів).

        // Додаємо штучний результат-посилання на пошук по YouTube для цього запиту
        var youtubeUrl = $"https://www.youtube.com/results?search_query={Uri.EscapeDataString(query)}";
        results.Add(new UnifiedSearchResult
        {
            Title = $"Пошук на YouTube: \"{query}\"",
            Url = youtubeUrl,
            DisplayUrl = "youtube.com › results",
            Snippet = $"Відкрити результати пошуку на YouTube для \"{query}\".",
            Source = SearchSourceType.YouTube,
            Timestamp = null,
            RankScore = 0,
            PageNumber = 1
        });

        // Проставляємо PageNumber і RankScore (спрощено: позиція в списку)
        for (int i = 0; i < results.Count; i++)
        {
            results[i].PageNumber = 1;
            results[i].RankScore = results.Count - i;
        }

        var page = new UnifiedSearchPage
        {
            SearchSessionId = store.SessionId,
            PageNumber = 1,
            PageSize = pageSize,
            TotalResults = results.Count,
            Results = results.ToArray()
        };

        await store.SavePageAsync(page, ct);

        return page;
    }
}
