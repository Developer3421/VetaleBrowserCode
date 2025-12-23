using Avalonia;

namespace VetaleBrowser.VetaleBrowser.Search.Services;

/// <summary>
/// Small helper to read localized strings from Avalonia Application resources.
/// This allows backend search services to generate localized "redirect" entries.
/// </summary>
internal static class SearchLocalization
{
    public static string Get(string key, string fallback)
    {
        try
        {
            if (Application.Current?.Resources.TryGetResource(key, null, out var resource) == true && resource is string str)
                return str;
        }
        catch
        {
            // ignore (can happen when called before UI is initialized)
        }

        return fallback;
    }
}
