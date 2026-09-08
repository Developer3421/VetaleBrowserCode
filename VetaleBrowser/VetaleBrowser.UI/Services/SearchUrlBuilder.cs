using System;

namespace VetaleBrowser.VetaleBrowser.UI.Services;

/// <summary>
/// Кастомні пошукові системи: нормалізація шаблону і безпечна підстановка запиту.
/// Підтримує плейсхолдери {0} і %s. Навмисно НЕ string.Format — він падає на шаблонах
/// з фігурними дужками (напр. JS у URL) і мовчки ламає навігацію.
/// </summary>
public static class SearchUrlBuilder
{
    public const string Placeholder = "{0}";

    /// <summary>Нормалізує введений шаблон: trim, %s -> {0}, перевірки. Повертає null якщо ок.</summary>
    public static string? Validate(string? rawTemplate)
    {
        if (string.IsNullOrWhiteSpace(rawTemplate))
            return "Введіть URL пошуку, наприклад https://example.com/search?q={0}";
        var t = rawTemplate.Trim().Replace("%s", Placeholder);
        if (!t.Contains(Placeholder, StringComparison.Ordinal))
            return "Шаблон має містити плейсхолдер запиту: {0} (або %s)";
        var qi = t.IndexOf('?');
        var basePart = qi >= 0 ? t.Substring(0, qi) : t;
        if (!Uri.TryCreate(basePart, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return "URL має починатись з http:// або https://";
        return null;
    }

    public static string Normalize(string rawTemplate)
        => rawTemplate.Trim().Replace("%s", Placeholder);

    /// <summary>Будує фінальний URL. Ніколи не кидає виняток — повертає null при битій конфігурації.</summary>
    public static string? Build(string? template, string query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(template)) return null;
            var t = template.Trim().Replace("%s", Placeholder);
            if (!t.Contains(Placeholder, StringComparison.Ordinal)) return null;
            return t.Replace(Placeholder, Uri.EscapeDataString(query ?? string.Empty));
        }
        catch
        {
            return null;
        }
    }
}
