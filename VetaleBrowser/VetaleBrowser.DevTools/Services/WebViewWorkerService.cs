using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Avalonia.Threading;
using VetaleBrowser.VetaleBrowser.Core.Scripts.Models;
using VetaleBrowser.VetaleBrowser.Database.Models;
using VetaleBrowser.VetaleBrowser.Database.Services;
using VetaleBrowser.VetaleBrowser.Core.Scripts.Browser;

namespace VetaleBrowser.VetaleBrowser.DevTools.Services
{
    /// <summary>
    /// Service for integrating WebViewWorker with DevTools pages.
    /// Provides access to DOM, Performance, Resources and Storage via the active tab or local WebView.
    /// </summary>
    public class WebViewWorkerService
    {
        private static WebViewWorkerService? _instance;
        private static readonly object _lock = new object();
        
        private readonly DevToolsDataService _dataService;
        private TabWorker? _activeTab;
        private string _sessionId;

        // New: local DevTools WebView (not tied to MainWindow tabs)
        private IBrowserView? _localWebView;
        
        // Playwright integration - works in parallel with WebView
        private PlaywrightDevToolsService? _playwrightService;

        public event EventHandler<DomElement>? DomElementCaptured;
        public event EventHandler<PerformanceSnapshot>? PerformanceSnapshotCaptured;
        public event EventHandler<PageResource>? ResourceCaptured;
        public event EventHandler<StorageItem>? StorageItemCaptured;

        /// <summary>
        /// Get singleton instance of WebViewWorkerService
        /// </summary>
        public static WebViewWorkerService GetInstance(DevToolsDataService dataService)
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new WebViewWorkerService(dataService);
                    }
                }
            }
            return _instance;
        }

        private WebViewWorkerService(DevToolsDataService dataService)
        {
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _sessionId = Guid.NewGuid().ToString();
            
            // Initialize Playwright service (headless mode for DevTools analysis)
            _playwrightService = PlaywrightDevToolsService.GetInstance(_dataService);
            InitializePlaywrightAsync().ConfigureAwait(false);
        }
        
        private async Task InitializePlaywrightAsync()
        {
            try
            {
                await _playwrightService!.InitializeAsync();
                Debug.WriteLine("[WebViewWorkerService] Playwright initialized in headless mode");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerService] Failed to initialize Playwright: {ex.Message}");
            }
        }
        
        public TabWorker? ActiveTab
        {
            get => _activeTab;
            set
            {
                if (_activeTab != value)
                {
                    UnsubscribeFromTab(_activeTab);
                    _activeTab = value;
                    SubscribeToTab(_activeTab);
                    
                    // Generate new session ID for new tab
                    _sessionId = Guid.NewGuid().ToString();
                }
            }
        }

        /// <summary>
        /// Attach a local WebView from the DevTools page (independent of MainWindow).
        /// </summary>
        public void AttachLocalWebView(IBrowserView webView)
        {
            if (_localWebView == webView) return;

            DetachLocalWebView();
            _localWebView = webView;

            // Rotate session on navigation within local WebView
            _localWebView.PropertyChanged += OnLocalWebViewPropertyChanged;

            // New session context for this local View
            _sessionId = Guid.NewGuid().ToString();
            Debug.WriteLine("[WebViewWorkerService] Attached local WebView for DevTools");
        }

        /// <summary>
        /// Detach the local WebView.
        /// </summary>
        public void DetachLocalWebView()
        {
            if (_localWebView != null)
            {
                _localWebView.PropertyChanged -= OnLocalWebViewPropertyChanged;
                _localWebView = null;
            }
        }

        private void OnLocalWebViewPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property != null && e.Property.Name == "Address")
            {
                _sessionId = Guid.NewGuid().ToString();
                Debug.WriteLine($"[WebViewWorkerService] Local WebView navigated, new session: {_sessionId}");
            }
        }

        public string CurrentUrl => _localWebView?.Address ?? _activeTab?.Address ?? "";
        public string CurrentTitle => _localWebView?.Title ?? _activeTab?.Title ?? "";
        public string SessionId => _sessionId;

        /// <summary>
        /// Automatically sync with the current active tab from MainWindow
        /// </summary>
        public void SyncWithMainWindow()
        {
            try
            {
                if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var mainWindow = desktop.Windows.FirstOrDefault(w => w.GetType().Name == "MainWindow");
                    if (mainWindow != null)
                    {
                        var tabsManagerProp = mainWindow.GetType().GetProperty("TabsManager");
                        if (tabsManagerProp != null)
                        {
                            var tabsManager = tabsManagerProp.GetValue(mainWindow);
                            if (tabsManager != null)
                            {
                                var activeProp = tabsManager.GetType().GetProperty("Active");
                                if (activeProp != null)
                                {
                                    var activeTab = activeProp.GetValue(tabsManager) as TabWorker;
                                    if (activeTab != null && activeTab != _activeTab)
                                    {
                                        ActiveTab = activeTab;
                                        Debug.WriteLine($"[WebViewWorkerService] Synced with active tab: {activeTab.Title}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerService] Error syncing with MainWindow: {ex.Message}");
            }
        }

        private void SubscribeToTab(TabWorker? tab)
        {
            if (tab == null) return;
            
            tab.AddressChanged += OnTabNavigated;
            Debug.WriteLine($"[WebViewWorkerService] Subscribed to tab: {tab.Title}");
        }

        private void UnsubscribeFromTab(TabWorker? tab)
        {
            if (tab == null) return;
            
            tab.AddressChanged -= OnTabNavigated;
            Debug.WriteLine($"[WebViewWorkerService] Unsubscribed from tab: {tab.Title}");
        }

        private void OnTabNavigated(object? sender, string? url)
        {
            // Generate new session ID on navigation
            _sessionId = Guid.NewGuid().ToString();
            Debug.WriteLine($"[WebViewWorkerService] Tab navigated to: {url}, new session: {_sessionId}");
        }

        #region DOM Elements

        /// <summary>
        /// Captures the DOM structure of the current page via JavaScript.
        /// </summary>
        public async Task<List<DomElement>> CaptureDomStructureAsync()
        {
            // Try to use Playwright for better DOM analysis (takes URL from WebView)
            var currentUrl = CurrentUrl;
            if (!string.IsNullOrEmpty(currentUrl) && _playwrightService != null)
            {
                try
                {
                    Debug.WriteLine($"[WebViewWorkerService] Using Playwright to analyze DOM for: {currentUrl}");
                    await _playwrightService.NavigateAsync(currentUrl);
                    return await _playwrightService.CaptureDomStructureAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebViewWorkerService] Playwright failed, falling back to WebView: {ex.Message}");
                }
            }
            
            // Fallback: use WebView JavaScript
            if (!HasAnyWebView())
            {
                Debug.WriteLine("[WebViewWorkerService] No WebView available (neither local nor active tab)");
                return new List<DomElement>();
            }

            try
            {
                // Clear old DOM elements for this session
                await _dataService.ClearDomElementsAsync(_sessionId);

                // JavaScript to retrieve DOM structure
                var script = @"
                    (function() {
                        function serializeElement(element, path = '0') {
                            if (!element || !element.tagName) return null;
                            
                            var attrs = {};
                            if (element.attributes) {
                                for (var i = 0; i < element.attributes.length; i++) {
                                    var attr = element.attributes[i];
                                    attrs[attr.name] = attr.value;
                                }
                            }
                            
                            var computedStyle = window.getComputedStyle(element);
                            var styles = {
                                display: computedStyle.display,
                                position: computedStyle.position,
                                width: computedStyle.width,
                                height: computedStyle.height,
                                margin: computedStyle.margin,
                                padding: computedStyle.padding,
                                color: computedStyle.color,
                                backgroundColor: computedStyle.backgroundColor,
                                fontSize: computedStyle.fontSize,
                                fontFamily: computedStyle.fontFamily
                            };
                            
                            return {
                                tagName: element.tagName.toLowerCase(),
                                id: element.id || '',
                                className: element.className || '',
                                attributes: attrs,
                                textContent: element.childNodes.length === 1 && element.childNodes[0].nodeType === 3 
                                    ? element.childNodes[0].textContent.trim().substring(0, 100) 
                                    : '',
                                path: path,
                                styles: styles,
                                childCount: element.children.length
                            };
                        }
                        
                        function traverseDOM(element, depth = 0, maxDepth = 10, path = '0') {
                            var results = [];
                            
                            if (depth > maxDepth) return results;
                            
                            var serialized = serializeElement(element, path);
                            if (serialized) {
                                results.push(serialized);
                                
                                for (var i = 0; i < element.children.length && i < 50; i++) {
                                    var childPath = path + '.' + i;
                                    var childResults = traverseDOM(element.children[i], depth + 1, maxDepth, childPath);
                                    results = results.concat(childResults);
                                }
                            }
                            
                            return results;
                        }
                        
                        return JSON.stringify(traverseDOM(document.documentElement));
                    })();
                ";

                var result = await ExecuteJavaScriptAsync(script);
                if (string.IsNullOrEmpty(result)) return new List<DomElement>();

                // Parse JSON result
                var jsonElements = System.Text.Json.JsonSerializer.Deserialize<List<DomElementJson>>(result);
                if (jsonElements == null) return new List<DomElement>();

                var domElements = new List<DomElement>();
                foreach (var jsonEl in jsonElements)
                {
                    var domElement = new DomElement
                    {
                        SessionId = _sessionId,
                        TagName = jsonEl.tagName ?? "",
                        // Map short text content to InnerHtml field for preview
                        InnerHtml = jsonEl.textContent,
                        Attributes = System.Text.Json.JsonSerializer.Serialize(jsonEl.attributes ?? new Dictionary<string, string>()),
                        ComputedStyles = System.Text.Json.JsonSerializer.Serialize(jsonEl.styles ?? new Dictionary<string, string>()),
                        ElementPath = jsonEl.path ?? "",
                        CapturedAt = DateTime.UtcNow
                    };

                    await _dataService.SaveDomElementAsync(domElement);
                    domElements.Add(domElement);
                    
                    DomElementCaptured?.Invoke(this, domElement);
                }

                Debug.WriteLine($"[WebViewWorkerService] Captured {domElements.Count} DOM elements");
                return domElements;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerService] Error capturing DOM: {ex.Message}");
                return new List<DomElement>();
            }
        }

        public async Task<List<DomElement>> GetDomElementsAsync()
        {
            return await _dataService.GetDomElementsAsync(_sessionId);
        }

        #endregion

        #region Performance

        /// <summary>
        /// Captures performance metrics of the current page.
        /// </summary>
        public async Task<PerformanceSnapshot?> CapturePerformanceSnapshotAsync()
        {
            // Try to use Playwright for better performance analysis (takes URL from WebView)
            var currentUrl = CurrentUrl;
            if (!string.IsNullOrEmpty(currentUrl) && _playwrightService != null)
            {
                try
                {
                    Debug.WriteLine($"[WebViewWorkerService] Using Playwright to analyze performance for: {currentUrl}");
                    await _playwrightService.NavigateAsync(currentUrl);
                    return await _playwrightService.CapturePerformanceSnapshotAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebViewWorkerService] Playwright failed, falling back to WebView: {ex.Message}");
                }
            }
            
            // Fallback: use WebView JavaScript
            if (!HasAnyWebView())
            {
                return null;
            }

            try
            {
                var script = @"
                    (function() {
                        var perf = window.performance;
                        var timing = perf.timing;
                        var memory = perf.memory || {};
                        
                        var resources = perf.getEntriesByType('resource').map(function(r) {
                            return {
                                name: r.name,
                                type: r.initiatorType,
                                duration: r.duration,
                                size: r.transferSize || 0,
                                startTime: r.startTime
                            };
                        });
                        
                        return JSON.stringify({
                            loadTime: timing.loadEventEnd - timing.navigationStart,
                            domContentLoaded: timing.domContentLoadedEventEnd - timing.navigationStart,
                            firstPaint: (perf.getEntriesByType('paint').find(function(p) { return p.name === 'first-paint'; }) || {}).startTime || 0,
                            firstContentfulPaint: (perf.getEntriesByType('paint').find(function(p) { return p.name === 'first-contentful-paint'; }) || {}).startTime || 0,
                            memoryUsed: memory.usedJSHeapSize || 0,
                            memoryLimit: memory.jsHeapSizeLimit || 0,
                            resourceCount: resources.length,
                            resources: resources.slice(0, 100)
                        });
                    })();
                ";

                var result = await ExecuteJavaScriptAsync(script);
                if (string.IsNullOrEmpty(result)) return null;

                var perfData = System.Text.Json.JsonSerializer.Deserialize<PerformanceDataJson>(result);
                if (perfData == null) return null;

                var snapshot = new PerformanceSnapshot
                {
                    SessionId = _sessionId,
                    Url = CurrentUrl,
                    LoadTime = perfData.loadTime,
                    DomContentLoadedTime = perfData.domContentLoaded,
                    FirstPaintTime = (long)perfData.firstPaint,
                    MemoryUsed = perfData.memoryUsed,
                    // Persist resource timings summary as JSON string for compatibility
                    ResourceTimings = System.Text.Json.JsonSerializer.Serialize(new { perfData.resourceCount })
                };

                await _dataService.SavePerformanceSnapshotAsync(snapshot);
                PerformanceSnapshotCaptured?.Invoke(this, snapshot);

                Debug.WriteLine($"[WebViewWorkerService] Captured performance snapshot: Load={snapshot.LoadTime}ms, DOM={snapshot.DomContentLoadedTime}ms");
                return snapshot;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerService] Error capturing performance: {ex.Message}");
                return null;
            }
        }

        public async Task<List<PerformanceSnapshot>> GetPerformanceSnapshotsAsync()
        {
            return await _dataService.GetPerformanceSnapshotsAsync(_sessionId);
        }

        #endregion

        #region Resources (Sources Page)

        /// <summary>
        /// Captures all page resources (scripts, styles, images).
        /// </summary>
        public async Task<List<PageResource>> CapturePageResourcesAsync()
        {
            // Try to use Playwright for better resource analysis (takes URL from WebView)
            var currentUrl = CurrentUrl;
            if (!string.IsNullOrEmpty(currentUrl) && _playwrightService != null)
            {
                try
                {
                    Debug.WriteLine($"[WebViewWorkerService] Using Playwright to analyze resources for: {currentUrl}");
                    await _playwrightService.NavigateAsync(currentUrl);
                    return await _playwrightService.CapturePageResourcesAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebViewWorkerService] Playwright failed, falling back to WebView: {ex.Message}");
                }
            }
            
            // Fallback: use WebView JavaScript
            if (!HasAnyWebView())
            {
                Debug.WriteLine("[WebViewWorkerService] No WebView available (neither local nor active tab)");
                return new List<PageResource>();
            }

            try
            {
                await _dataService.ClearPageResourcesAsync(_sessionId);

                var script = @"
                    (function() {
                        var resources = [];
                        
                        // Scripts
                        document.querySelectorAll('script').forEach(function(script, idx) {
                            if (script.src) {
                                resources.push({
                                    type: 'script',
                                    url: script.src,
                                    content: '',
                                    size: 0
                                });
                            } else if (script.textContent) {
                                resources.push({
                                    type: 'inline-script',
                                    url: 'inline-' + idx,
                                    content: script.textContent.substring(0, 10000),
                                    size: script.textContent.length
                                });
                            }
                        });
                        
                        // Stylesheets
                        document.querySelectorAll('link[rel=""stylesheet""]').forEach(function(link) {
                            resources.push({
                                type: 'stylesheet',
                                url: link.href,
                                content: '',
                                size: 0
                            });
                        });
                        
                        document.querySelectorAll('style').forEach(function(style, idx) {
                            resources.push({
                                type: 'inline-style',
                                url: 'inline-' + idx,
                                content: style.textContent.substring(0, 10000),
                                size: style.textContent.length
                            });
                        });
                        
                        // Images
                        document.querySelectorAll('img').forEach(function(img) {
                            if (img.src) {
                                resources.push({
                                    type: 'image',
                                    url: img.src,
                                    content: '',
                                    size: 0
                                });
                            }
                        });
                        
                        return JSON.stringify(resources);
                    })();
                ";

                var result = await ExecuteJavaScriptAsync(script);
                if (string.IsNullOrEmpty(result)) return new List<PageResource>();

                var jsonResources = System.Text.Json.JsonSerializer.Deserialize<List<PageResourceJson>>(result);
                if (jsonResources == null) return new List<PageResource>();

                var pageResources = new List<PageResource>();
                foreach (var jsonRes in jsonResources)
                {
                    var resource = new PageResource
                    {
                        SessionId = _sessionId,
                        Url = jsonRes.url ?? "",
                        Type = jsonRes.type ?? "",
                        EncryptedContent = jsonRes.content ?? "",
                        Size = jsonRes.size,
                        CapturedAt = DateTime.UtcNow
                    };

                    await _dataService.SavePageResourceAsync(resource);
                    pageResources.Add(resource);
                    
                    ResourceCaptured?.Invoke(this, resource);
                }

                Debug.WriteLine($"[WebViewWorkerService] Captured {pageResources.Count} page resources");
                return pageResources;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerService] Error capturing resources: {ex.Message}");
                return new List<PageResource>();
            }
        }

        public async Task<List<PageResource>> GetPageResourcesAsync()
        {
            return await _dataService.GetPageResourcesAsync(_sessionId);
        }

        #endregion

        #region Storage (Application Page)

        /// <summary>
        /// Captures Storage (localStorage, sessionStorage, cookies).
        /// </summary>
        public async Task<List<StorageItem>> CaptureStorageAsync(string storageType = "all")
        {
            // Try to use Playwright for better storage analysis (takes URL from WebView)
            var currentUrl = CurrentUrl;
            if (!string.IsNullOrEmpty(currentUrl) && _playwrightService != null)
            {
                try
                {
                    Debug.WriteLine($"[WebViewWorkerService] Using Playwright to analyze storage for: {currentUrl}");
                    await _playwrightService.NavigateAsync(currentUrl);
                    return await _playwrightService.CaptureStorageAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebViewWorkerService] Playwright failed, falling back to WebView: {ex.Message}");
                }
            }
            
            if (!HasAnyWebView())
            {
                Debug.WriteLine("[WebViewWorkerService] No WebView available (neither local nor active tab)");
                return new List<StorageItem>();
            }

            try
            {
                var storageItems = new List<StorageItem>();

                // LocalStorage
                if (storageType == "all" || storageType == "localStorage")
                {
                    var localStorageScript = @"
                        (function() {
                            var items = [];
                            for (var i = 0; i < localStorage.length; i++) {
                                var key = localStorage.key(i);
                                items.push({
                                    key: key,
                                    value: localStorage.getItem(key)
                                });
                            }
                            return JSON.stringify(items);
                        })();
                    ";

                    var result = await ExecuteJavaScriptAsync(localStorageScript);
                    if (!string.IsNullOrEmpty(result))
                    {
                        var items = System.Text.Json.JsonSerializer.Deserialize<List<StorageItemJson>>(result);
                        if (items != null)
                        {
                            foreach (var item in items)
                            {
                                var storageItem = new StorageItem
                                {
                                    SessionId = _sessionId,
                                    StorageType = "localStorage",
                                    Key = item.key ?? "",
                                    EncryptedValue = item.value ?? "",
                                    CapturedAt = DateTime.UtcNow
                                };
                                await _dataService.SaveStorageItemAsync(storageItem);
                                storageItems.Add(storageItem);
                                StorageItemCaptured?.Invoke(this, storageItem);
                            }
                        }
                    }
                }

                // SessionStorage
                if (storageType == "all" || storageType == "sessionStorage")
                {
                    var sessionStorageScript = @"
                        (function() {
                            var items = [];
                            for (var i = 0; i < sessionStorage.length; i++) {
                                var key = sessionStorage.key(i);
                                items.push({
                                    key: key,
                                    value: sessionStorage.getItem(key)
                                });
                            }
                            return JSON.stringify(items);
                        })();
                    ";

                    var result = await ExecuteJavaScriptAsync(sessionStorageScript);
                    if (!string.IsNullOrEmpty(result))
                    {
                        var items = System.Text.Json.JsonSerializer.Deserialize<List<StorageItemJson>>(result);
                        if (items != null)
                        {
                            foreach (var item in items)
                            {
                                var storageItem = new StorageItem
                                {
                                    SessionId = _sessionId,
                                    StorageType = "sessionStorage",
                                    Key = item.key ?? "",
                                    EncryptedValue = item.value ?? "",
                                    CapturedAt = DateTime.UtcNow
                                };
                                await _dataService.SaveStorageItemAsync(storageItem);
                                storageItems.Add(storageItem);
                                StorageItemCaptured?.Invoke(this, storageItem);
                            }
                        }
                    }
                }

                // Cookies
                if (storageType == "all" || storageType == "cookies")
                {
                    var cookiesScript = @"
                        (function() {
                            var cookies = document.cookie.split(';').map(function(c) {
                                var parts = c.trim().split('=');
                                return {
                                    key: parts[0],
                                    value: parts.slice(1).join('=')
                                };
                            }).filter(function(c) { return c.key; });
                            return JSON.stringify(cookies);
                        })();
                    ";

                    var result = await ExecuteJavaScriptAsync(cookiesScript);
                    if (!string.IsNullOrEmpty(result))
                    {
                        var items = System.Text.Json.JsonSerializer.Deserialize<List<StorageItemJson>>(result);
                        if (items != null)
                        {
                            foreach (var item in items)
                            {
                                var storageItem = new StorageItem
                                {
                                    SessionId = _sessionId,
                                    StorageType = "cookies",
                                    Key = item.key ?? "",
                                    EncryptedValue = item.value ?? "",
                                    CapturedAt = DateTime.UtcNow
                                };
                                await _dataService.SaveStorageItemAsync(storageItem);
                                storageItems.Add(storageItem);
                                StorageItemCaptured?.Invoke(this, storageItem);
                            }
                        }
                    }
                }

                Debug.WriteLine($"[WebViewWorkerService] Captured {storageItems.Count} storage items");
                return storageItems;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerService] Error capturing storage: {ex.Message}");
                return new List<StorageItem>();
            }
        }

        public async Task<List<StorageItem>> GetStorageItemsAsync(string? storageType = null)
        {
            return await _dataService.GetStorageItemsAsync(_sessionId, storageType);
        }

        public async Task DeleteStorageItemAsync(int id)
        {
            await _dataService.DeleteStorageItemAsync(id);
        }

        public async Task ClearStorageAsync(string? storageType = null)
        {
            await _dataService.ClearStorageAsync(_sessionId, storageType);
        }

        #endregion

        #region JavaScript Execution Helper

        private bool HasAnyWebView() => _localWebView != null || _activeTab?.WebView != null;

        public async Task<string> ExecuteJavaScriptAsync(string script)
        {
            var webView = _localWebView ?? _activeTab?.WebView;
            if (webView == null) return "";

            try
            {
                var tcs = new TaskCompletionSource<string>();
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    try
                    {
                        var res = await webView.EvaluateScriptAsync<object>(script);
                        tcs.TrySetResult(res?.ToString() ?? "");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WebViewWorkerService] Execute script error: {ex.Message}");
                        tcs.TrySetResult("");
                    }
                });

                return await tcs.Task;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerService] Error executing JavaScript: {ex.Message}");
                return "";
            }
        }

        #endregion

        #region JSON Models for Deserialization

        // Keep property names to match JSON payload from JS (camelCase)
        private class DomElementJson
        {
            public string? tagName { get; set; }
            public string? id { get; set; }
            public string? className { get; set; }
            public Dictionary<string, string>? attributes { get; set; }
            public Dictionary<string, string>? styles { get; set; }
            public string? textContent { get; set; }
            public string? path { get; set; }
            public int childCount { get; set; }
        }

        private class PerformanceDataJson
        {
            public long loadTime { get; set; }
            public long domContentLoaded { get; set; }
            public double firstPaint { get; set; }
            public double firstContentfulPaint { get; set; }
            public long memoryUsed { get; set; }
            public long memoryLimit { get; set; }
            public int resourceCount { get; set; }
        }

        private class PageResourceJson
        {
            public string? type { get; set; }
            public string? url { get; set; }
            public string? content { get; set; }
            public long size { get; set; }
        }

        private class StorageItemJson
        {
            public string? key { get; set; }
            public string? value { get; set; }
        }

        #endregion
        
        public void Dispose()
        {
            DetachLocalWebView();
            UnsubscribeFromTab(_activeTab);
            _playwrightService?.Dispose();
            _dataService?.Dispose();
        }
    }
}

