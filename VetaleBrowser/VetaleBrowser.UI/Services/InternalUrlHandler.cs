using System;
using Avalonia.Controls;
using VetaleBrowser.VetaleBrowser.UI.Pages;
using VetaleBrowser.VetaleBrowser.Search.Services;
using VetaleBrowser.VetaleBrowser.VoiceRecognition.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Services;

/// <summary>
/// Сервіс для обробки внутрішніх URL браузера (vetale://)
/// </summary>
public static class InternalUrlHandler
{
    public const string InternalProtocol = "vetale://";
    
    // Статичні сервіси для налаштування створюваних сторінок
    public static ISuggestionsService? GlobalSuggestionsService { get; set; }
    public static IVoiceRecognitionService? GlobalVoiceRecognitionService { get; set; }
    
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
            InternalPageType.VetaleSearch => CreateSearchHomePage(url),
            InternalPageType.VetaleSearchResults => CreateSearchResultsPage(url),
            InternalPageType.Bookmarks => new BookmarksPage(),
            InternalPageType.History => new HistoryPage(),
            InternalPageType.Settings => new SettingsMainPage(),
            InternalPageType.Tools => new ToolsMainPage(),
            _ => null
        };
    }

    private static VetaleSearchHomePage CreateSearchHomePage(string url)
    {
        System.Diagnostics.Debug.WriteLine($"[InternalUrlHandler] CreateSearchHomePage called for: {url}");
        var page = new VetaleSearchHomePage();
        System.Diagnostics.Debug.WriteLine($"[InternalUrlHandler] VetaleSearchHomePage instance created");
        
        // Налаштовуємо сервіси якщо вони доступні
        System.Diagnostics.Debug.WriteLine($"[InternalUrlHandler] GlobalSuggestionsService null? {GlobalSuggestionsService == null}");
        if (GlobalSuggestionsService != null)
            page.SetSuggestionsService(GlobalSuggestionsService);
        
        System.Diagnostics.Debug.WriteLine($"[InternalUrlHandler] GlobalVoiceRecognitionService null? {GlobalVoiceRecognitionService == null}");
        if (GlobalVoiceRecognitionService != null)
        {
            System.Diagnostics.Debug.WriteLine($"[InternalUrlHandler] Calling SetVoiceRecognitionService...");
            page.SetVoiceRecognitionService(GlobalVoiceRecognitionService);
            System.Diagnostics.Debug.WriteLine($"[InternalUrlHandler] SetVoiceRecognitionService called");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[InternalUrlHandler] WARNING: GlobalVoiceRecognitionService is NULL!");
        }
        
        var q = GetQueryParameter(url, "q");
        if (!string.IsNullOrWhiteSpace(q))
        {
            page.SetQuery(q);
        }
        
        System.Diagnostics.Debug.WriteLine($"[InternalUrlHandler] Returning configured VetaleSearchHomePage");
        return page;
    }
    
    /// <summary>
    /// Створити сторінку результатів пошуку з query параметром
    /// </summary>
    private static VetaleSearchResultsPage CreateSearchResultsPage(string url)
    {
        System.Diagnostics.Debug.WriteLine($"[InternalUrlHandler] CreateSearchResultsPage: {url}");
        var page = new VetaleSearchResultsPage();
        
        // Налаштовуємо сервіси якщо вони доступні
        if (GlobalVoiceRecognitionService != null)
            page.SetVoiceRecognitionService(GlobalVoiceRecognitionService);
        
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
