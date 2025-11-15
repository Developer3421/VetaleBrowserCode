using System;
using Avalonia.Controls;
using VetaleBrowser.VetaleBrowser.UI.Pages;

namespace VetaleBrowser.VetaleBrowser.UI.Services;

/// <summary>
/// Сервіс для обробки внутрішніх URL браузера (vetale://)
/// </summary>
public static class InternalUrlHandler
{
    public const string InternalProtocol = "vetale://";
    
    /// <summary>
    /// Перевірити чи є URL внутрішнім
    /// </summary>
    public static bool IsInternalUrl(string url)
    {
        return url.StartsWith(InternalProtocol, StringComparison.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// Отримати тип внутрішньої сторінки з URL
    /// </summary>
    public static InternalPageType GetPageType(string url)
    {
        if (!IsInternalUrl(url))
            return InternalPageType.None;
            
        var path = url.Substring(InternalProtocol.Length)
                      .TrimStart('/')
                      .TrimEnd('/');
        
        // Відкидаємо query string якщо є
        var queryIndex = path.IndexOf('?');
        if (queryIndex >= 0)
        {
            path = path.Substring(0, queryIndex);
        }
        
        path = path.ToLowerInvariant();
        
        return path switch
        {
            "search" => InternalPageType.VetaleSearch,
            "search/home" => InternalPageType.VetaleSearch,
            "search/results" => InternalPageType.VetaleSearchResults,
            "bookmarks" => InternalPageType.Bookmarks,
            "history" => InternalPageType.History,
            "settings" => InternalPageType.Settings,
            "tools" => InternalPageType.Tools,
            _ => InternalPageType.Unknown
        };
    }
    
    /// <summary>
    /// Створити UserControl для внутрішньої сторінки
    /// </summary>
    public static UserControl? CreatePageContent(string url)
    {
        System.Diagnostics.Debug.WriteLine($"[InternalUrlHandler] CreatePageContent: {url}");
        var pageType = GetPageType(url);
        System.Diagnostics.Debug.WriteLine($"[InternalUrlHandler] Page type: {pageType}");
        
        return pageType switch
        {
            InternalPageType.VetaleSearch => new VetaleSearchHomePage(),
            InternalPageType.VetaleSearchResults => CreateSearchResultsPage(url),
            InternalPageType.Bookmarks => new BookmarksPage(),
            InternalPageType.History => new HistoryPage(),
            InternalPageType.Settings => new SettingsMainPage(),
            InternalPageType.Tools => new ToolsMainPage(),
            _ => null
        };
    }
    
    /// <summary>
    /// Створити сторінку результатів пошуку з query параметром
    /// </summary>
    private static VetaleSearchResultsPage CreateSearchResultsPage(string url)
    {
        System.Diagnostics.Debug.WriteLine($"[InternalUrlHandler] CreateSearchResultsPage: {url}");
        var page = new VetaleSearchResultsPage();
        
        // Витягуємо query параметр з URL
        var query = GetQueryParameter(url, "q");
        System.Diagnostics.Debug.WriteLine($"[InternalUrlHandler] Extracted query: '{query}'");
        
        if (!string.IsNullOrEmpty(query))
        {
            page.SetSearchQuery(query);
        }
        
        return page;
    }
    
    /// <summary>
    /// Отримати значення query параметра з URL
    /// </summary>
    public static string? GetQueryParameter(string url, string parameterName)
    {
        try
        {
            var queryStart = url.IndexOf('?');
            if (queryStart < 0)
                return null;
                
            var query = url.Substring(queryStart + 1);
            var pairs = query.Split('&');
            
            foreach (var pair in pairs)
            {
                var parts = pair.Split('=');
                if (parts.Length == 2 && parts[0] == parameterName)
                {
                    return Uri.UnescapeDataString(parts[1]);
                }
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Отримати заголовок для внутрішньої сторінки
    /// </summary>
    public static string GetPageTitle(string url)
    {
        var pageType = GetPageType(url);
        
        return pageType switch
        {
            InternalPageType.VetaleSearch => "Vetale Search",
            InternalPageType.VetaleSearchResults => "Результати пошуку - Vetale Search",
            InternalPageType.Bookmarks => "Закладки",
            InternalPageType.History => "Історія",
            InternalPageType.Settings => "Налаштування",
            InternalPageType.Tools => "Інструменти",
            _ => "VetaleBrowser"
        };
    }
}

/// <summary>
/// Типи внутрішніх сторінок браузера
/// </summary>
public enum InternalPageType
{
    None,
    Unknown,
    VetaleSearch,
    VetaleSearchResults,
    Bookmarks,
    History,
    Settings,
    Tools
}
