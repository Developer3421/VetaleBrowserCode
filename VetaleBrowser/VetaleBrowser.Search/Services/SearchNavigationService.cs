using System;
using System.Collections.Generic;
using VetaleBrowser.VetaleBrowser.Core.Scripts.Models; // TabWorker

namespace VetaleBrowser.VetaleBrowser.Search.Services;

/// <summary>
/// Інформація про навігацію з результатів пошуку.
/// </summary>
public sealed class SearchNavigationInfo
{
    public Guid SearchSessionId { get; set; }
    public string Query { get; set; } = string.Empty;
    public string ParentTabId { get; set; } = string.Empty; // ID вкладки з VetaleSearchResultsPage
    public string WorkerId { get; set; } = string.Empty; // ID воркера, створеного для результату
    public string TargetUrl { get; set; } = string.Empty;
    public DateTime NavigatedAt { get; set; }
}

/// <summary>
/// Сервіс для відстеження навігації з результатів пошуку.
/// Зберігає зв'язок між пошуковою сесією та воркерами, що відкрили результати.
/// </summary>
public interface ISearchNavigationService
{
    void RegisterNavigation(Guid sessionId, string query, string parentTabId, string workerId, string targetUrl);
    SearchNavigationInfo? GetNavigationInfo(string workerId);
    void UnregisterWorker(string workerId);
    void ClearSession(Guid sessionId);

    // Новий метод: відкрити результат у поточному воркері (в тій самій вкладці)
    void OpenResultInCurrentWorker(
        Guid sessionId,
        string query,
        string parentTabId,
        TabWorker worker,
        string url,
        Action<TabWorker> showWebViewInUi,
        Action<TabWorker, string> navigateInWorker);
}

public sealed class SearchNavigationService : ISearchNavigationService
{
    private readonly Dictionary<string, SearchNavigationInfo> _workerToNavigation = new();
    private readonly object _lock = new();

    public void RegisterNavigation(Guid sessionId, string query, string parentTabId, string workerId, string targetUrl)
    {
        lock (_lock)
        {
            _workerToNavigation[workerId] = new SearchNavigationInfo
            {
                SearchSessionId = sessionId,
                Query = query,
                ParentTabId = parentTabId,
                WorkerId = workerId,
                TargetUrl = targetUrl,
                NavigatedAt = DateTime.UtcNow
            };

            System.Diagnostics.Debug.WriteLine($"[SearchNavService] Registered: worker={workerId}, parent={parentTabId}, session={sessionId}");
        }
    }

    public SearchNavigationInfo? GetNavigationInfo(string workerId)
    {
        lock (_lock)
        {
            return _workerToNavigation.TryGetValue(workerId, out var info) ? info : null;
        }
    }

    public void UnregisterWorker(string workerId)
    {
        lock (_lock)
        {
            if (_workerToNavigation.Remove(workerId))
            {
                System.Diagnostics.Debug.WriteLine($"[SearchNavService] Unregistered worker: {workerId}");
            }
        }
    }

    public void ClearSession(Guid sessionId)
    {
        lock (_lock)
        {
            var toRemove = new List<string>();
            foreach (var kvp in _workerToNavigation)
            {
                if (kvp.Value.SearchSessionId == sessionId)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var key in toRemove)
            {
                _workerToNavigation.Remove(key);
            }

            System.Diagnostics.Debug.WriteLine($"[SearchNavService] Cleared session {sessionId}, removed {toRemove.Count} workers");
        }
    }

    public void OpenResultInCurrentWorker(
        Guid sessionId,
        string query,
        string parentTabId,
        TabWorker worker,
        string url,
        Action<TabWorker> showWebViewInUi,
        Action<TabWorker, string> navigateInWorker)
    {
        if (worker == null) return;
        var workerId = worker.Address ?? Guid.NewGuid().ToString();

        // Реєструємо перехід (для можливого Back у майбутньому)
        RegisterNavigation(sessionId, query, parentTabId, workerId, url);

        // Ховаємо внутрішню сторінку (VetaleSearchResultsPage) і показуємо WebView цього воркера
        showWebViewInUi(worker);

        // Навігуємо на цільовий URL у межах того ж воркера
        navigateInWorker(worker, url);
    }
}
