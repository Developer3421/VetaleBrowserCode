namespace VetaleBrowser.VetaleBrowser.Search.Models;

/// <summary>
/// Website security status
/// </summary>
public enum SecurityStatus
{
    /// <summary>Unknown status (check not performed)</summary>
    Unknown,
    
    /// <summary>Safe site</summary>
    Safe,
    
    /// <summary>Check in progress</summary>
    Checking,
    
    /// <summary>Dangerous site (phishing)</summary>
    Dangerous,
    
    /// <summary>Check error</summary>
    Error
}

