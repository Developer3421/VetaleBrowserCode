using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Service for working with DevTools data with AES encryption
/// </summary>
public class DevToolsDataService : IDevToolsDataService, IDisposable
{
    private static readonly ConcurrentDictionary<string, LiteDatabase> _databaseInstances = new();
    private readonly LiteDatabase _database;
    private readonly DatabaseEncryptionService _encryption;
    private readonly string _dbPath;
    
    private readonly ILiteCollection<HtmlEditorState> _htmlEditorStates;
    private readonly ILiteCollection<DomElement> _domElements;
    private readonly ILiteCollection<PerformanceSnapshot> _performanceSnapshots;
    private readonly ILiteCollection<PageResource> _pageResources;
    private readonly ILiteCollection<StorageItem> _storageItems;

    public DevToolsDataService(string? databasePath = null, string? encryptionKey = null)
    {
        _dbPath = databasePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VetaleBrowser",
            "devtools.db"
        );

        var directory = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Use shared connection to avoid file locking issues
        var connectionString = $"Filename={_dbPath};Connection=shared";
        _database = _databaseInstances.GetOrAdd(_dbPath, _ => new LiteDatabase(connectionString));
        _encryption = new DatabaseEncryptionService(encryptionKey ?? "VetaleBrowser_DevTools_2024");

        _htmlEditorStates = _database.GetCollection<HtmlEditorState>("html_editor_states");
        _domElements = _database.GetCollection<DomElement>("dom_elements");
        _performanceSnapshots = _database.GetCollection<PerformanceSnapshot>("performance_snapshots");
        _pageResources = _database.GetCollection<PageResource>("page_resources");
        _storageItems = _database.GetCollection<StorageItem>("storage_items");

        // Create indexes
        _htmlEditorStates.EnsureIndex(x => x.SessionKey);
        _htmlEditorStates.EnsureIndex(x => x.IsActive);
        _domElements.EnsureIndex(x => x.SessionId);
        _performanceSnapshots.EnsureIndex(x => x.SessionId);
        _pageResources.EnsureIndex(x => x.SessionId);
        _storageItems.EnsureIndex(x => x.SessionId);
        _storageItems.EnsureIndex(x => x.StorageType);

        Debug.WriteLine($"[DevToolsDataService] Initialized with database at: {_dbPath}");
    }

    #region HTML Editor

    public async Task<HtmlEditorState?> GetActiveHtmlEditorStateAsync()
    {
        return await Task.Run(() =>
        {
            var state = _htmlEditorStates.FindOne(x => x.IsActive);
            if (state != null && !string.IsNullOrEmpty(state.EncryptedContent))
            {
                try
                {
                    var decryptedContent = _encryption.DecryptString(state.EncryptedContent);
                    state.EncryptedContent = decryptedContent;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DevToolsDataService] Error decrypting HTML content: {ex.Message}");
                }
            }
            return state;
        });
    }

    public async Task<List<HtmlEditorState>> GetAllHtmlEditorStatesAsync()
    {
        return await Task.Run(() =>
        {
            var states = _htmlEditorStates.FindAll().OrderByDescending(x => x.UpdatedAt).ToList();
            foreach (var state in states)
            {
                if (!string.IsNullOrEmpty(state.EncryptedContent))
                {
                    try
                    {
                        state.EncryptedContent = _encryption.DecryptString(state.EncryptedContent);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[DevToolsDataService] Error decrypting HTML content: {ex.Message}");
                    }
                }
            }
            return states;
        });
    }

    public async Task SaveHtmlEditorStateAsync(HtmlEditorState state)
    {
        await Task.Run(() =>
        {
            try
            {
                // Encrypt content before saving
                if (!string.IsNullOrEmpty(state.EncryptedContent))
                {
                    state.EncryptedContent = _encryption.EncryptString(state.EncryptedContent);
                }

                state.UpdatedAt = DateTime.UtcNow;

                // If this is active, deactivate all others
                if (state.IsActive)
                {
                    var allStates = _htmlEditorStates.FindAll();
                    foreach (var s in allStates)
                    {
                        if (s.Id != state.Id)
                        {
                            s.IsActive = false;
                            _htmlEditorStates.Update(s);
                        }
                    }
                }

                if (state.Id > 0)
                {
                    _htmlEditorStates.Update(state);
                }
                else
                {
                    _htmlEditorStates.Insert(state);
                }

                Debug.WriteLine($"[DevToolsDataService] Saved HTML Editor State: {state.SessionKey}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DevToolsDataService] Error saving HTML Editor State: {ex.Message}");
                throw;
            }
        });
    }

    public async Task DeleteHtmlEditorStateAsync(int id)
    {
        await Task.Run(() =>
        {
            _htmlEditorStates.Delete(id);
            Debug.WriteLine($"[DevToolsDataService] Deleted HTML Editor State: {id}");
        });
    }

    public async Task<HtmlEditorState?> GetHtmlEditorStateByIdAsync(int id)
    {
        return await Task.Run(() =>
        {
            var state = _htmlEditorStates.FindById(id);
            if (state != null && !string.IsNullOrEmpty(state.EncryptedContent))
            {
                try
                {
                    state.EncryptedContent = _encryption.DecryptString(state.EncryptedContent);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DevToolsDataService] Error decrypting HTML content: {ex.Message}");
                }
            }
            return state;
        });
    }

    #endregion

    #region DOM Elements

    public async Task<List<DomElement>> GetDomElementsAsync(string sessionId)
    {
        return await Task.Run(() =>
        {
            return _domElements.Find(x => x.SessionId == sessionId)
                .OrderBy(x => x.ElementPath)
                .ToList();
        });
    }

    public async Task SaveDomElementAsync(DomElement element)
    {
        await Task.Run(() =>
        {
            element.CapturedAt = DateTime.UtcNow;
            _domElements.Insert(element);
            Debug.WriteLine($"[DevToolsDataService] Saved DOM Element: {element.TagName}");
        });
    }

    public async Task ClearDomElementsAsync(string sessionId)
    {
        await Task.Run(() =>
        {
            _domElements.DeleteMany(x => x.SessionId == sessionId);
            Debug.WriteLine($"[DevToolsDataService] Cleared DOM elements for session: {sessionId}");
        });
    }

    #endregion

    #region Performance

    public async Task<List<PerformanceSnapshot>> GetPerformanceSnapshotsAsync(string sessionId)
    {
        return await Task.Run(() =>
        {
            return _performanceSnapshots.Find(x => x.SessionId == sessionId)
                .OrderByDescending(x => x.CapturedAt)
                .ToList();
        });
    }

    public async Task SavePerformanceSnapshotAsync(PerformanceSnapshot snapshot)
    {
        await Task.Run(() =>
        {
            snapshot.CapturedAt = DateTime.UtcNow;
            _performanceSnapshots.Insert(snapshot);
            Debug.WriteLine($"[DevToolsDataService] Saved Performance Snapshot for URL: {snapshot.Url}");
        });
    }

    public async Task<PerformanceSnapshot?> GetLatestPerformanceSnapshotAsync(string sessionId)
    {
        return await Task.Run(() =>
        {
            return _performanceSnapshots.Find(x => x.SessionId == sessionId)
                .OrderByDescending(x => x.CapturedAt)
                .FirstOrDefault();
        });
    }

    #endregion

    #region Resources

    public async Task<List<PageResource>> GetPageResourcesAsync(string sessionId)
    {
        return await Task.Run(() =>
        {
            var resources = _pageResources.Find(x => x.SessionId == sessionId).ToList();
            foreach (var resource in resources)
            {
                if (!string.IsNullOrEmpty(resource.EncryptedContent))
                {
                    try
                    {
                        resource.EncryptedContent = _encryption.DecryptString(resource.EncryptedContent);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[DevToolsDataService] Error decrypting resource content: {ex.Message}");
                    }
                }
            }
            return resources;
        });
    }

    public async Task SavePageResourceAsync(PageResource resource)
    {
        await Task.Run(() =>
        {
            // Encrypt content if present
            if (!string.IsNullOrEmpty(resource.EncryptedContent))
            {
                resource.EncryptedContent = _encryption.EncryptString(resource.EncryptedContent);
            }

            resource.CapturedAt = DateTime.UtcNow;
            _pageResources.Insert(resource);
            Debug.WriteLine($"[DevToolsDataService] Saved Page Resource: {resource.Url}");
        });
    }

    public async Task ClearPageResourcesAsync(string sessionId)
    {
        await Task.Run(() =>
        {
            _pageResources.DeleteMany(x => x.SessionId == sessionId);
            Debug.WriteLine($"[DevToolsDataService] Cleared page resources for session: {sessionId}");
        });
    }

    #endregion

    #region Storage

    public async Task<List<StorageItem>> GetStorageItemsAsync(string sessionId, string? storageType = null)
    {
        return await Task.Run(() =>
        {
            var query = storageType == null
                ? _storageItems.Find(x => x.SessionId == sessionId)
                : _storageItems.Find(x => x.SessionId == sessionId && x.StorageType == storageType);

            var items = query.ToList();
            foreach (var item in items)
            {
                if (!string.IsNullOrEmpty(item.EncryptedValue))
                {
                    try
                    {
                        item.EncryptedValue = _encryption.DecryptString(item.EncryptedValue);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[DevToolsDataService] Error decrypting storage item: {ex.Message}");
                    }
                }
            }
            return items;
        });
    }

    public async Task SaveStorageItemAsync(StorageItem item)
    {
        await Task.Run(() =>
        {
            // Encrypt value before saving
            if (!string.IsNullOrEmpty(item.EncryptedValue))
            {
                item.EncryptedValue = _encryption.EncryptString(item.EncryptedValue);
            }

            item.CapturedAt = DateTime.UtcNow;
            _storageItems.Insert(item);
            Debug.WriteLine($"[DevToolsDataService] Saved Storage Item: {item.Key}");
        });
    }

    public async Task DeleteStorageItemAsync(int id)
    {
        await Task.Run(() =>
        {
            _storageItems.Delete(id);
            Debug.WriteLine($"[DevToolsDataService] Deleted Storage Item: {id}");
        });
    }

    public async Task ClearStorageAsync(string sessionId, string? storageType = null)
    {
        await Task.Run(() =>
        {
            if (storageType == null)
            {
                _storageItems.DeleteMany(x => x.SessionId == sessionId);
                Debug.WriteLine($"[DevToolsDataService] Cleared all storage for session: {sessionId}");
            }
            else
            {
                _storageItems.DeleteMany(x => x.SessionId == sessionId && x.StorageType == storageType);
                Debug.WriteLine($"[DevToolsDataService] Cleared {storageType} for session: {sessionId}");
            }
        });
    }

    #endregion

    public void Dispose()
    {
        // Don't dispose shared database instance here
        // Use DisposeAll() on application shutdown
        Debug.WriteLine("[DevToolsDataService] Service disposed (database instance remains shared)");
    }

    /// <summary>
    /// Dispose all shared database instances. Call this on application shutdown.
    /// </summary>
    public static void DisposeAll()
    {
        foreach (var kvp in _databaseInstances)
        {
            try
            {
                kvp.Value?.Dispose();
                Debug.WriteLine($"[DevToolsDataService] Disposed database: {kvp.Key}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DevToolsDataService] Error disposing database {kvp.Key}: {ex.Message}");
            }
        }
        _databaseInstances.Clear();
    }
}

