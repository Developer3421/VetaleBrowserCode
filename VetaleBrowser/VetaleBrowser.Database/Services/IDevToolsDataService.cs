using System.Collections.Generic;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

public interface IDevToolsDataService
{
    // HTML Editor
    Task<HtmlEditorState?> GetActiveHtmlEditorStateAsync();
    Task<List<HtmlEditorState>> GetAllHtmlEditorStatesAsync();
    Task SaveHtmlEditorStateAsync(HtmlEditorState state);
    Task DeleteHtmlEditorStateAsync(int id);
    Task<HtmlEditorState?> GetHtmlEditorStateByIdAsync(int id);
    
    // DOM Elements
    Task<List<DomElement>> GetDomElementsAsync(string sessionId);
    Task SaveDomElementAsync(DomElement element);
    Task ClearDomElementsAsync(string sessionId);
    
    // Performance
    Task<List<PerformanceSnapshot>> GetPerformanceSnapshotsAsync(string sessionId);
    Task SavePerformanceSnapshotAsync(PerformanceSnapshot snapshot);
    Task<PerformanceSnapshot?> GetLatestPerformanceSnapshotAsync(string sessionId);
    
    // Resources
    Task<List<PageResource>> GetPageResourcesAsync(string sessionId);
    Task SavePageResourceAsync(PageResource resource);
    Task ClearPageResourcesAsync(string sessionId);
    
    // Storage
    Task<List<StorageItem>> GetStorageItemsAsync(string sessionId, string? storageType = null);
    Task SaveStorageItemAsync(StorageItem item);
    Task DeleteStorageItemAsync(int id);
    Task ClearStorageAsync(string sessionId, string? storageType = null);
}

