using System;

namespace VetaleBrowser.VetaleBrowser.Search.Models;

/// <summary>
/// Результат перевірки безпеки URL
/// </summary>
public class SecurityCheckResult
{
    /// <summary>Перевірений URL</summary>
    public string Url { get; set; } = string.Empty;
    
    /// <summary>Статус безпеки</summary>
    public SecurityStatus Status { get; set; } = SecurityStatus.Unknown;
    
    /// <summary>Джерело інформації про безпеку</summary>
    public SecuritySource Source { get; set; } = SecuritySource.Unknown;
    
    /// <summary>Опис результату</summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>Чи є сайт в базі фішингу</summary>
    public bool IsPhishing { get; set; }
    
    /// <summary>Час перевірки</summary>
    public DateTime CheckedAt { get; set; } = DateTime.Now;
    
    /// <summary>Деталі з PhishTank (якщо є)</summary>
    public PhishTankDetails? Details { get; set; }
    
    /// <summary>Деталі з VirusTotal (якщо є)</summary>
    public VirusTotalDetails? VirusTotalDetails { get; set; }
}

/// <summary>
/// Деталі з PhishTank API
/// </summary>
public class PhishTankDetails
{
    /// <summary>ID запису в PhishTank</summary>
    public string? PhishId { get; set; }
    
    /// <summary>Час додавання до бази</summary>
    public DateTime? SubmittedAt { get; set; }
    
    /// <summary>Підтверджений фішинг</summary>
    public bool Verified { get; set; }
    
    /// <summary>Додаткова інформація</summary>
    public string? AdditionalInfo { get; set; }
}

/// <summary>
/// Деталі з VirusTotal API
/// </summary>
public class VirusTotalDetails
{
    public string? Id { get; set; }
    public int Harmless { get; set; }
    public int Malicious { get; set; }
    public int Suspicious { get; set; }
    public int Undetected { get; set; }
    public int Timeout { get; set; }
    public DateTime? LastAnalysisDate { get; set; }
    public string? RawLabel { get; set; }
}
