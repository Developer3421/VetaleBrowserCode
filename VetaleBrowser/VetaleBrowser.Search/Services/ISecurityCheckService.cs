using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Search.Models;

namespace VetaleBrowser.VetaleBrowser.Search.Services;

/// <summary>
/// Interface of URL security check service
/// </summary>
public interface ISecurityCheckService
{
    /// <summary>
    /// Check URL security
    /// </summary>
    /// <param name="url">URL to check</param>
    /// <returns>Security check result</returns>
    Task<SecurityCheckResult> CheckUrlAsync(string url);
    
    /// <summary>
    /// Clear check cache
    /// </summary>
    void ClearCache();
}

