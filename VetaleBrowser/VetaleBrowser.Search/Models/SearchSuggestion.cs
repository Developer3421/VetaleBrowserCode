using System;
using System.Net.Mime;

namespace VetaleBrowser.VetaleBrowser.Search.Models;

/// <summary>
/// Модель підказки пошуку.
/// </summary>
public class SearchSuggestion
{
    /// <summary>Текст підказки.</summary>
    public string Text { get; set; } = string.Empty;
    /// <summary>Тип підказки (search, history, bookmark).</summary>
    public string Type { get; set; } = "search";
    /// <summary>Іконка (емодзі чи символ).</summary>
    public string Icon { get; set; } = "🔍";
    /// <summary>URL для переходу (опціонально).</summary>
    public string? Url { get; set; }
}
