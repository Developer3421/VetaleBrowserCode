using System.Collections.Generic;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Interface for working with the tab database
/// </summary>
public interface ITabDatabaseService
{
    // Session operations
    int CreateSession();
    BrowserSession? GetCurrentSession();
    void DeleteSession(int sessionId);

    // Tab operations
    int AddTab(int sessionId, string url, string title, bool isActive = false);
    void UpdateTab(int tabId, string? url = null, string? title = null, bool? isActive = null);
    List<TabModel> GetSessionTabs(int sessionId);
    void DeleteTab(int tabId);

    // Bookmark operations
    int AddBookmark(string url, string title, string folder = "Bookmarks");
    List<Bookmark> GetBookmarks(string? folder = null);
    void DeleteBookmark(int bookmarkId);

    // Statistics
    DatabaseStats GetStats();
}

