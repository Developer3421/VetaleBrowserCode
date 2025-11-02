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

