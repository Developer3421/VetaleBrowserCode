using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Search.Models;

namespace VetaleBrowser.VetaleBrowser.Search.Database;

/// <summary>
/// Stores unified search session pages (file-backed).
/// </summary>
public interface ISearchSessionStore
{
    Guid SessionId { get; }
    Task SavePageAsync(UnifiedSearchPage page, CancellationToken ct = default);
}

/// <summary>
/// JSON-file implementation of <see cref="ISearchSessionStore"/>.
/// </summary>
public sealed class JsonFileSearchSessionStore : ISearchSessionStore
{
    public Guid SessionId { get; } = Guid.NewGuid();

    public Task SavePageAsync(UnifiedSearchPage page, CancellationToken ct = default)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VetaleBrowser", "Data", "SearchSessions", SessionId.ToString());
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"page_{page.PageNumber}.json");
            var json = JsonSerializer.Serialize(page);
            return File.WriteAllTextAsync(path, json, ct);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SearchSessionStore] Save failed: {ex.Message}");
            return Task.CompletedTask;
        }
    }
}
