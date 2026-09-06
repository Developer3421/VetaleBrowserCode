using System;
using Avalonia.Controls;
using VetaleBrowser.VetaleBrowser.UI.Pages;
using VetaleBrowser.VetaleBrowser.Search.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Services;

/// <summary>
/// Service for handling internal browser URLs (vetale://)
/// MEMORY OPTIMIZATION: Uses lazy service providers for on-demand creation
/// </summary>
public static class InternalUrlHandler
{
    public const string InternalProtocol = "vetale://";
    
    // MEMORY OPTIMIZATION: Lazy service providers instead of direct instances
    // Services will be created only when actually needed
    public static Func<ISuggestionsService>? SuggestionsServiceProvider { get; set; }
    
    // Legacy direct properties (for backward compatibility, marked as obsolete)
    private static ISuggestionsService? _cachedSuggestionsService;
    
    public static ISuggestionsService? GlobalSuggestionsService 
    { 
        get => _cachedSuggestionsService ?? SuggestionsServiceProvider?.Invoke();
        set => _cachedSuggestionsService = value;
    }
    
    /// <summary>
    /// Check whether a URL is internal
    /// </summary>
    public static bool IsInternalUrl(string url)
    {
        return url.StartsWith(InternalProtocol, StringComparison.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// Get the internal page type from a URL
    /// </summary>
    public static InternalPageType GetPageType(string url)
    {
        if (!IsInternalUrl(url))
            return InternalPageType.None;
            
        var path = url.Substring(InternalProtocol.Length)
                      .TrimStart('/')
                      .TrimEnd('/');
        
        // Discard query string if present
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
    /// Create a UserControl for an internal page
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
        
        // Configure services if they are available
        System.Diagnostics.Debug.WriteLine($"[InternalUrlHandler] GlobalSuggestionsService null? {GlobalSuggestionsService == null}");
        if (GlobalSuggestionsService != null)
            page.SetSuggestionsService(GlobalSuggestionsService);
        
        var q = GetQueryParameter(url, "q");
        if (!string.IsNullOrWhiteSpace(q))
        {
            page.SetQuery(q);
        }
        
        // IMPORTANT: wire navigation events back to the browser/tab
        page.NavigateRequested += (_, targetUrl) =>
        {
            try
            {
                NavigationRequestCallback?.Invoke(targetUrl);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InternalUrlHandler] HomePage NavigateRequested callback error: {ex.Message}");
            }
        };

        System.Diagnostics.Debug.WriteLine($"[InternalUrlHandler] Returning configured VetaleSearchHomePage");
        return page;
    }
    
    /// <summary>
    /// Create a search results page with a query parameter
    /// </summary>
    private static VetaleSearchResultsPage CreateSearchResultsPage(string url)
    {
        System.Diagnostics.Debug.WriteLine($"[InternalUrlHandler] CreateSearchResultsPage: {url}");
        var page = new VetaleSearchResultsPage();

        // IMPORTANT: wire navigation events back to the browser/tab
        page.NavigateRequested += (_, targetUrl) =>
        {
            try
            {
                NavigationRequestCallback?.Invoke(targetUrl);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InternalUrlHandler] ResultsPage NavigateRequested callback error: {ex.Message}");
            }
        };

        // Extract the query parameter from the URL
        var query = GetQueryParameter(url, "q");
        System.Diagnostics.Debug.WriteLine($"[InternalUrlHandler] Extracted query: '{query}'");
        
        if (!string.IsNullOrEmpty(query))
        {
            page.SetSearchQuery(query);
        }
        
        return page;
    }
    
    /// <summary>
    /// Get the value of a query parameter from a URL
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
    /// Get the title for an internal page
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
    
    /// <summary>
    /// Callback for internal pages to request navigation (typically to update current tab URL).
    /// Set by MainWindow on startup.
    /// </summary>
    public static Action<string>? NavigationRequestCallback { get; set; }
}

/// <summary>
/// Internal browser page types
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
