using System;

namespace VetaleBrowser.VetaleBrowser.UI.Services;

/// <summary>
/// Кастомні пошукові системи: нормалізація шаблону і безпечна підстановка запиту.
/// Підтримувані варіанти плейсхолдера: {0}, %s, {searchTerms}, {query} (регістр ігнорується).
/// Шаблон БЕЗ плейсхолдера теж валідний: запит дописується в кінець
/// (напр. https://www.ecosia.org/search?q= → https://www.ecosia.org/search?q=запит).
/// Навмисно НЕ string.Format — він падає на шаблонах з фігурними дужками.
/// </summary>
public static class SearchUrlBuilder
{
    public const string Placeholder = "{0}";

    private static readonly string[] AltPlaceholders = new[]
    {
        "%s", "{searchTerms}", "{searchterms}", "{SearchTerms}", "{SEARCHTERMS}",
        "{query}", "{Query}", "{QUERY}", "%q", "{text}", "{Text}",
    };

    /// <summary>Фатальні помилки шаблону (блокують збереження). Повертає null якщо ок.</summary>
    public static string? Validate(string? rawTemplate)
    {
        if (string.IsNullOrWhiteSpace(rawTemplate))
            return "Введіть URL пошуку, наприклад https://www.ecosia.org/search?q=";
        var t = rawTemplate.Trim();
        var qi = t.IndexOf('?');
        var basePart = qi >= 0 ? t.Substring(0, qi) : t;
        // Базою може бути і шаблон з плейсхолдером у хості — перевіряємо м'яко.
        var probe = Normalize(t);
        var probeBase = probe.IndexOf('?') >= 0 ? probe.Substring(0, probe.IndexOf('?')) : probe;
        probeBase = probeBase.Replace(Placeholder, "x");
        if (!Uri.TryCreate(string.IsNullOrEmpty(probeBase) ? basePart : probeBase, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return "URL має починатись з http:// або https://";
        return null;
    }

    /// <summary>Чи є в шаблоні явний плейсхолдер. Якщо нема — запит допишеться в кінець.</summary>
    public static bool HasPlaceholder(string? rawTemplate)
    {
        if (string.IsNullOrWhiteSpace(rawTemplate)) return false;
        var t = rawTemplate.Trim();
        if (t.Contains(Placeholder, StringComparison.Ordinal)) return true;
        foreach (var alt in AltPlaceholders)
            if (t.Contains(alt, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static string Normalize(string rawTemplate)
    {
        var t = rawTemplate.Trim();
        foreach (var alt in AltPlaceholders)
            t = ReplaceInsensitive(t, alt, Placeholder);
        return t;
    }

    /// <summary>Будує фінальний URL. Ніколи не кидає виняток — повертає null при битій конфігурації.</summary>
    public static string? Build(string? template, string query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(template)) return null;
            var t = Normalize(template);
            var encoded = Uri.EscapeDataString(query ?? string.Empty);
            if (t.Contains(Placeholder, StringComparison.Ordinal))
                return t.Replace(Placeholder, encoded);
            // Без плейсхолдера: https://www.ecosia.org/search?q= + запит
            return t + encoded;
        }
        catch
        {
            return null;
        }
    }

    private static string ReplaceInsensitive(string input, string oldValue, string newValue)
    {
        int idx;
        while ((idx = input.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase)) >= 0)
            input = input.Substring(0, idx) + newValue + input.Substring(idx + oldValue.Length);
        return input;
    }
}
