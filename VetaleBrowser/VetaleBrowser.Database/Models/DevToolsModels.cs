using System;

namespace VetaleBrowser.VetaleBrowser.Database.Models;

/// <summary>
/// Модель для збереження стану HTML Editor з AES шифруванням
/// </summary>
public class HtmlEditorState
{
    public int Id { get; set; }
    public string SessionKey { get; set; } = string.Empty; // Унікальний ключ для сесії редактора
    public string EncryptedContent { get; set; } = string.Empty; // Зашифрований HTML контент
    public string FileName { get; set; } = "Untitled"; // Назва файлу
    public string? FilePath { get; set; } // Шлях до файлу (якщо збережений)
    public string TemplateType { get; set; } = "Blank HTML"; // Тип шаблону
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } // Чи є це активна сесія
}

/// <summary>
/// Модель для збереження елементів DOM дерева
/// </summary>
public class DomElement
{
    public int Id { get; set; }
    public string SessionId { get; set; } = string.Empty; // ID сесії DevTools
    public string ElementPath { get; set; } = string.Empty; // Шлях до елемента в DOM
    public string TagName { get; set; } = string.Empty;
    public string? InnerHtml { get; set; }
    public string? Attributes { get; set; } // JSON з атрибутами
    public string? ComputedStyles { get; set; } // JSON з computed styles
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Модель для зберігання даних Performance
/// </summary>
public class PerformanceSnapshot
{
    public int Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public long LoadTime { get; set; } // мілісекунди
    public long DomContentLoadedTime { get; set; }
    public long FirstPaintTime { get; set; }
    public long MemoryUsed { get; set; } // байти
    public string? ResourceTimings { get; set; } // JSON з тайменгами ресурсів
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Модель для зберігання інформації про ресурси сторінки
/// </summary>
public class PageResource
{
    public int Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // script, stylesheet, image, etc.
    public long Size { get; set; } // байти
    public string? ContentType { get; set; }
    public string? EncryptedContent { get; set; } // Зашифрований контент (для scripts/css)
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Модель для зберігання Application Storage даних
/// </summary>
public class StorageItem
{
    public int Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string StorageType { get; set; } = string.Empty; // LocalStorage, SessionStorage, Cookie, Cache
    public string Key { get; set; } = string.Empty;
    public string EncryptedValue { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}

