namespace VetaleBrowser.VetaleBrowser.Search.Models;

/// <summary>
/// Search suggestion model.
/// </summary>
public class SearchSuggestion
{
    /// <summary>Suggestion text.</summary>
    public string Text { get; set; } = string.Empty;
    /// <summary>Suggestion type (search, history, bookmark).</summary>
    public string Type { get; set; } = "search";
    /// <summary>Icon (emoji or symbol).</summary>
    public string Icon { get; set; } = "🔍";
    /// <summary>URL to navigate to (optional).</summary>
    public string? Url { get; set; }
}
