using System;

namespace VetaleBrowser.VetaleBrowser.Database.Models;

/// <summary>
/// Model for storing HTML Editor state with AES encryption
/// </summary>
public class HtmlEditorState
{
    public int Id { get; set; }
    public string SessionKey { get; set; } = string.Empty; // Unique session key for the editor
    public string EncryptedContent { get; set; } = string.Empty; // Encrypted HTML content
    public string FileName { get; set; } = "Untitled"; // File name
    public string? FilePath { get; set; } // File path (if saved)
    public string TemplateType { get; set; } = "Blank HTML"; // Template type
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } // Whether this is the active session
}

/// <summary>
/// Model for storing DOM tree elements
/// </summary>
public class DomElement
{
    public int Id { get; set; }
    public string SessionId { get; set; } = string.Empty; // DevTools session ID
    public string ElementPath { get; set; } = string.Empty; // Path to element in DOM
    public string TagName { get; set; } = string.Empty;
    public string? InnerHtml { get; set; }
    public string? Attributes { get; set; } // JSON with attributes
    public string? ComputedStyles { get; set; } // JSON with computed styles
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Model for storing Performance data
/// </summary>
public class PerformanceSnapshot
{
    public int Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public long LoadTime { get; set; } // milliseconds
    public long DomContentLoadedTime { get; set; }
    public long FirstPaintTime { get; set; }
    public long MemoryUsed { get; set; } // bytes
    public string? ResourceTimings { get; set; } // JSON with resource timings
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Model for storing page resource information
/// </summary>
public class PageResource
{
    public int Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // script, stylesheet, image, etc.
    public long Size { get; set; } // bytes
    public string? ContentType { get; set; }
    public string? EncryptedContent { get; set; } // Encrypted content (for scripts/css)
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Model for storing Application Storage data
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

