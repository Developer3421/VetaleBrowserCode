using System;

namespace VetaleBrowser.VetaleBrowser.Search.Database;

/// <summary>
/// Records search result clicks.
/// </summary>
public interface ISearchHistoryDatabaseService
{
    void AddSearchClick(Guid sessionId, string query, string url, string sourceType);
}

/// <summary>
/// No-op implementation of <see cref="ISearchHistoryDatabaseService"/>.
/// Click logging to files is disabled — no log files are created.
/// The constructor signature is kept so callers (MainWindow) don't change.
/// </summary>
public sealed class SearchHistoryDatabaseService : ISearchHistoryDatabaseService
{
    public SearchHistoryDatabaseService(string dbPath, string? encryptionKey = null)
    {
    }

    public void AddSearchClick(Guid sessionId, string query, string url, string sourceType)
    {
        // No-op: click logging to files is disabled (no log files are created).
        // The signature is kept so callers (MainWindow) don't change.
    }
}
