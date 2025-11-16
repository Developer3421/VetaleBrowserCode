namespace VetaleBrowser.VetaleBrowser.Search.Models;

/// <summary>
/// Джерело інформації про безпеку URL
/// </summary>
public enum SecuritySource
{
    Unknown,
    LocalList,
    Heuristic,
    VirusTotal,
    LocalAndVirusTotal
}

