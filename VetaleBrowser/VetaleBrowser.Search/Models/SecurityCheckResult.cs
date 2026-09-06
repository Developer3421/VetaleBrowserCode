using System;

namespace VetaleBrowser.VetaleBrowser.Search.Models;

/// <summary>
/// URL security check result
/// </summary>
public class SecurityCheckResult
{
    /// <summary>Checked URL</summary>
    public string Url { get; set; } = string.Empty;
    
    /// <summary>Security status</summary>
    public SecurityStatus Status { get; set; } = SecurityStatus.Unknown;
    
    /// <summary>Security information source</summary>
    public SecuritySource Source { get; set; } = SecuritySource.Unknown;
    
    /// <summary>Result description</summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>Whether the site is in the phishing database</summary>
    public bool IsPhishing { get; set; }
    
    /// <summary>Check time</summary>
    public DateTime CheckedAt { get; set; } = DateTime.Now;
    
    /// <summary>Details from PhishTank (if available)</summary>
    public PhishTankDetails? Details { get; set; }
}

/// <summary>
/// Details from PhishTank API
/// </summary>
public class PhishTankDetails
{
    /// <summary>PhishTank record ID</summary>
    public string? PhishId { get; set; }
    
    /// <summary>Time added to database</summary>
    public DateTime? SubmittedAt { get; set; }
    
    /// <summary>Confirmed phishing</summary>
    public bool Verified { get; set; }
    
    /// <summary>Additional information</summary>
    public string? AdditionalInfo { get; set; }
}
