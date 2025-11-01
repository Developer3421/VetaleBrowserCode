using System.Collections.Generic;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Інтерфейс для роботи з базою даних вкладок
/// </summary>
public interface ITabDatabaseService
{
    // Операції з сесіями
    int CreateSession();
    BrowserSession? GetCurrentSession();
    void DeleteSession(int sessionId);

    // Операції з вкладками
    int AddTab(int sessionId, string url, string title, bool isActive = false);
    void UpdateTab(int tabId, string? url = null, string? title = null, bool? isActive = null);
    List<TabModel> GetSessionTabs(int sessionId);
    void DeleteTab(int tabId);

    // Операції з закладками
    int AddBookmark(string url, string title, string folder = "Закладки");
    List<Bookmark> GetBookmarks(string? folder = null);
    void DeleteBookmark(int bookmarkId);

    // Статистика
    DatabaseStats GetStats();
}

