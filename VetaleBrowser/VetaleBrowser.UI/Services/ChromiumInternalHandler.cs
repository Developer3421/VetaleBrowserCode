using System;
using Avalonia.Controls;
using VetaleBrowser.VetaleBrowser.UI.Pages;

namespace VetaleBrowser.VetaleBrowser.UI.Services;

/// <summary>
/// Chromium internal pages (chrome://) that the bundled CEF build cannot
/// render itself (unregistered scheme -&gt; ERR_FILE_NOT_FOUND).
/// Served as built-in pages with real system data; the address bar
/// keeps showing the original chrome:// URL.
/// </summary>
public static class ChromiumInternalHandler
{
    public static bool IsChromiumInternalUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var u = url.Trim().ToLowerInvariant();
        return u.Equals("chrome://gpu")
            || u.StartsWith("chrome://gpu/", StringComparison.Ordinal)
            || u.Equals("chrome://version")
            || u.StartsWith("chrome://version/", StringComparison.Ordinal);
    }

    public static UserControl? CreatePageContent(string url)
    {
        var u = url.Trim().ToLowerInvariant();
        if (u.StartsWith("chrome://gpu", StringComparison.Ordinal))
            return new GpuInfoPage();
        if (u.StartsWith("chrome://version", StringComparison.Ordinal))
            return new VersionInfoPage();
        return null;
    }

    public static string GetPageTitle(string url)
    {
        var u = url.Trim().ToLowerInvariant();
        if (u.StartsWith("chrome://gpu", StringComparison.Ordinal))
            return "Graphics Feature Status - chrome://gpu";
        if (u.StartsWith("chrome://version", StringComparison.Ordinal))
            return "About Version - chrome://version";
        return url;
    }
}
