using System;

namespace VetaleBrowser.VetaleBrowser.Database.Models;

/// <summary>
/// Модель для збереження вкладок в базі даних
/// </summary>
public class TabModel
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? FaviconUrl { get; set; }
    public byte[]? FaviconData { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    public int SessionId { get; set; }
    public int Order { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Модель для сесії браузера
/// </summary>
public class BrowserSession
{
    public int Id { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public bool IsCurrent { get; set; }
    public int TabCount { get; set; }
}

/// <summary>
/// Модель для закладок
/// </summary>
public class Bookmark
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Folder { get; set; } = "Закладки";
    public string? FaviconUrl { get; set; }
    public byte[]? FaviconData { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int Order { get; set; }
}

/// <summary>
/// Модель для історії переглядів
/// </summary>
public class HistoryItem
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? FaviconUrl { get; set; }
    public byte[]? FaviconData { get; set; }
    public DateTime VisitedAt { get; set; } = DateTime.UtcNow;
    public int VisitCount { get; set; } = 1;
}

/// <summary>
/// Модель для налаштувань браузера з AES шифруванням
/// </summary>
public class SettingItem
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string EncryptedValue { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Модель для логів консолі з AES шифруванням
/// </summary>
public class ConsoleLogItem
{
    public int Id { get; set; }
    public string Level { get; set; } = "Info"; // Info, Warning, Error, Debug
    public string Message { get; set; } = string.Empty;
    public string? Source { get; set; } // Джерело логу (WebView, System, User тощо)
    public string? StackTrace { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Модель для індексації сторінок для Vetale Search (локальний пошук)
/// </summary>
public class SearchIndex
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty; // Текстовий вміст сторінки
    public string Description { get; set; } = string.Empty; // Meta description
    public string Keywords { get; set; } = string.Empty; // Meta keywords
    public string? FaviconUrl { get; set; }
    public byte[]? FaviconData { get; set; }
    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastVisitedAt { get; set; } = DateTime.UtcNow;
    public int VisitCount { get; set; } = 1;
    public int RelevanceScore { get; set; } = 0; // Оцінка релевантності (базується на частоті відвідувань)
    public string Language { get; set; } = "uk"; // Мова контенту
}

/// <summary>
/// Модель для пошукових запитів користувача (історія пошуків)
/// </summary>
public class SearchQuery
{
    public int Id { get; set; }
    public string Query { get; set; } = string.Empty;
    public string SearchEngine { get; set; } = "Vetale Search"; // Назва використаної пошукової системи
    public DateTime SearchedAt { get; set; } = DateTime.UtcNow;
    public int ResultsCount { get; set; } = 0;
    public int? ClickedResultId { get; set; } // ID результату, на який клікнули (якщо є)
}

/// <summary>
/// Модель для ключових слів та їх ваги в пошуковому індексі
/// </summary>
public class SearchKeyword
{
    public int Id { get; set; }
    public int SearchIndexId { get; set; } // Зв'язок з SearchIndex
    public string Keyword { get; set; } = string.Empty;
    public int Frequency { get; set; } = 1; // Кількість появ слова на сторінці
    public double Weight { get; set; } = 1.0; // Вага слова (заголовки мають більшу вагу)
}

