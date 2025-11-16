using System.Threading;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Search.Models;

namespace VetaleBrowser.VetaleBrowser.Search.Services;

/// <summary>
/// Interface for AI search summary service
/// </summary>
public interface IAiSummaryService
{
    /// <summary>
    /// Generate AI summary for search query
    /// </summary>
    /// <param name="query">Search query to summarize</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AI-generated summary</returns>
    Task<AiSearchSummary> GenerateSummaryAsync(string query, CancellationToken cancellationToken = default);
}

