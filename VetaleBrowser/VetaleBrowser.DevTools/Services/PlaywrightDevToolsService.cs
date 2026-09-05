using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
using VetaleBrowser.VetaleBrowser.Database.Models;
using VetaleBrowser.VetaleBrowser.Database.Services;

namespace VetaleBrowser.VetaleBrowser.DevTools.Services
{
    /// <summary>
    /// DevTools service using Microsoft Playwright.
    /// Provides functionality for Elements, Performance, Application and Sources.
    /// Singleton - one instance per application.
    /// </summary>
    public class PlaywrightDevToolsService : IDisposable
    {
        private static PlaywrightDevToolsService? _instance;
        private static readonly object _lock = new object();
        
        private IPlaywright? _playwright;
        private IBrowser? _browser;
        private IPage? _page;
        private readonly DevToolsDataService _dataService;
        private string _sessionId;
        private bool _isInitialized;
        private static bool _browsersInstalled;

        public event EventHandler<string>? NavigationCompleted;
        public event EventHandler<DomElement>? DomElementCaptured;
        public event EventHandler<PerformanceSnapshot>? PerformanceSnapshotCaptured;
        public event EventHandler<PageResource>? ResourceCaptured;
        public event EventHandler<StorageItem>? StorageItemCaptured;

        private PlaywrightDevToolsService(DevToolsDataService dataService)
        {
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _sessionId = Guid.NewGuid().ToString();
        }
        
        public static PlaywrightDevToolsService GetInstance(DevToolsDataService dataService)
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new PlaywrightDevToolsService(dataService);
                    }
                }
            }
            return _instance;
        }

        /// <summary>
        /// Initialize Playwright browser.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            try
            {
                await EnsureBrowsersInstalledAsync();

                _playwright = await Playwright.CreateAsync();
                _browser = await _playwright.Chromium.LaunchAsync(new()
                {
                    Headless = true, // Run in headless mode - no visible window
                    Args = new[] { "--disable-blink-features=AutomationControlled" }
                });

                var context = await _browser.NewContextAsync(new()
                {
                    ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
                });

                _page = await context.NewPageAsync();

                _isInitialized = true;
                Debug.WriteLine("[PlaywrightDevTools] Initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaywrightDevTools] Initialization failed: {ex.Message}");
                throw;
            }
        }

        private static async Task EnsureBrowsersInstalledAsync()
        {
            if (_browsersInstalled) return;
            try
            {
                // Install Chromium only (faster) — adjust if needed
                var code = await Task.Run(() => Microsoft.Playwright.Program.Main(new[] { "install", "chromium" }));
                if (code == 0)
                {
                    _browsersInstalled = true;
                    Debug.WriteLine("[PlaywrightDevTools] Playwright browsers installed (chromium)");
                }
                else
                {
                    Debug.WriteLine("[PlaywrightDevTools] Playwright install exited with code " + code);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[PlaywrightDevTools] Browser install failed: " + ex.Message);
                // Continue; launch may still work if already installed by another process
            }
        }

        /// <summary>
        /// Navigate to a URL.
        /// </summary>
        public async Task NavigateAsync(string url)
        {
            if (!_isInitialized)
                await InitializeAsync();

            if (_page == null)
                throw new InvalidOperationException("Page is not initialized");

            try
            {
                await _page.GotoAsync(url, new()
                {
                    WaitUntil = WaitUntilState.NetworkIdle,
                    Timeout = 30000
                });

                _sessionId = Guid.NewGuid().ToString();
                NavigationCompleted?.Invoke(this, url);
                Debug.WriteLine($"[PlaywrightDevTools] Navigated to: {url}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaywrightDevTools] Navigation failed: {ex.Message}");
                throw;
            }
        }


        #region Elements - DOM Inspection

        /// <summary>
        /// Captures the full DOM structure of the page.
        /// </summary>
        public async Task<List<DomElement>> CaptureDomStructureAsync()
        {
            if (_page == null)
                throw new InvalidOperationException("Page is not initialized");

            try
            {
                await _dataService.ClearDomElementsAsync(_sessionId);

                var script = @"
                    (function() {
                        function serializeElement(element, path = '0') {
                            if (!element || !element.tagName) return null;
                            
                            const attrs = {};
                            if (element.attributes) {
                                for (let i = 0; i < element.attributes.length; i++) {
                                    const attr = element.attributes[i];
                                    attrs[attr.name] = attr.value;
                                }
                            }
                            
                            const computedStyle = window.getComputedStyle(element);
                            const styles = {
                                display: computedStyle.display,
                                position: computedStyle.position,
                                width: computedStyle.width,
                                height: computedStyle.height,
                                margin: computedStyle.margin,
                                padding: computedStyle.padding,
                                color: computedStyle.color,
                                backgroundColor: computedStyle.backgroundColor,
                                fontSize: computedStyle.fontSize,
                                fontFamily: computedStyle.fontFamily,
                                border: computedStyle.border,
                                zIndex: computedStyle.zIndex,
                                opacity: computedStyle.opacity,
                                visibility: computedStyle.visibility
                            };
                            
                            const rect = element.getBoundingClientRect();
                            
                            return {
                                tagName: element.tagName.toLowerCase(),
                                id: element.id || '',
                                className: element.className || '',
                                attributes: attrs,
                                textContent: element.childNodes.length === 1 && element.childNodes[0].nodeType === 3 
                                    ? element.childNodes[0].textContent.trim().substring(0, 200) 
                                    : '',
                                path: path,
                                styles: styles,
                                childCount: element.children.length,
                                offsetTop: element.offsetTop,
                                offsetLeft: element.offsetLeft,
                                scrollHeight: element.scrollHeight,
                                scrollWidth: element.scrollWidth,
                                boundingRect: {
                                    top: rect.top,
                                    left: rect.left,
                                    width: rect.width,
                                    height: rect.height
                                }
                            };
                        }
                        
                        function traverseDOM(element, depth = 0, maxDepth = 15, path = '0') {
                            const results = [];
                            
                            if (depth > maxDepth) return results;
                            
                            const serialized = serializeElement(element, path);
                            if (serialized) {
                                results.push(serialized);
                                
                                for (let i = 0; i < element.children.length && i < 100; i++) {
                                    const childPath = path + '.' + i;
                                    const childResults = traverseDOM(element.children[i], depth + 1, maxDepth, childPath);
                                    results.push(...childResults);
                                }
                            }
                            
                            return results;
                        }
                        
                        return traverseDOM(document.documentElement);
                    })();
                ";

                var jsonResult = await _page.EvaluateAsync<JsonElement>(script);
                var elements = new List<DomElement>();

                foreach (var item in jsonResult.EnumerateArray())
                {
                    var element = ParseDomElement(item);
                    if (element != null)
                    {
                        await _dataService.SaveDomElementAsync(element);
                        elements.Add(element);
                        DomElementCaptured?.Invoke(this, element);
                    }
                }

                Debug.WriteLine($"[PlaywrightDevTools] Captured {elements.Count} DOM elements");
                return elements;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaywrightDevTools] DOM capture failed: {ex.Message}");
                return new List<DomElement>();
            }
        }

        /// <summary>
        /// Gets details of a specific element by selector.
        /// </summary>
        public async Task<DomElement?> GetElementDetailsAsync(string selector)
        {
            if (_page == null)
                throw new InvalidOperationException("Page is not initialized");

            try
            {
                var element = await _page.QuerySelectorAsync(selector);
                if (element == null) return null;

                var script = @"(element) => {
                    const attrs = {};
                    for (let i = 0; i < element.attributes.length; i++) {
                        const attr = element.attributes[i];
                        attrs[attr.name] = attr.value;
                    }
                    
                    const computedStyle = window.getComputedStyle(element);
                    const allStyles = {};
                    for (let i = 0; i < computedStyle.length; i++) {
                        const prop = computedStyle[i];
                        allStyles[prop] = computedStyle.getPropertyValue(prop);
                    }
                    
                    return {
                        tagName: element.tagName.toLowerCase(),
                        id: element.id || '',
                        className: element.className || '',
                        attributes: attrs,
                        textContent: element.textContent?.substring(0, 500) || '',
                        innerHTML: element.innerHTML?.substring(0, 1000) || '',
                        styles: allStyles,
                        offsetTop: element.offsetTop,
                        offsetLeft: element.offsetLeft
                    };
                }";

                var result = await element.EvaluateAsync<JsonElement>(script);
                return ParseDomElement(result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaywrightDevTools] Get element details failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Performs a CSS selector search.
        /// </summary>
        public async Task<List<DomElement>> QuerySelectorAllAsync(string selector)
        {
            if (_page == null)
                throw new InvalidOperationException("Page is not initialized");

            try
            {
                var elements = await _page.QuerySelectorAllAsync(selector);
                var results = new List<DomElement>();

                foreach (var element in elements)
                {
                    var script = @"(el) => ({
                        tagName: el.tagName.toLowerCase(),
                        id: el.id || '',
                        className: el.className || '',
                        textContent: el.textContent?.substring(0, 100) || ''
                    })";

                    var data = await element.EvaluateAsync<JsonElement>(script);
                    var domElement = ParseDomElement(data);
                    if (domElement != null)
                        results.Add(domElement);
                }

                return results;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaywrightDevTools] QuerySelectorAll failed: {ex.Message}");
                return new List<DomElement>();
            }
        }

        private DomElement? ParseDomElement(JsonElement item)
        {
            try
            {
                var attributes = new Dictionary<string, object>();
                if (item.TryGetProperty("id", out var id))
                    attributes["id"] = id.GetString() ?? "";
                if (item.TryGetProperty("className", out var cls))
                    attributes["class"] = cls.GetString() ?? "";
                if (item.TryGetProperty("textContent", out var txt))
                    attributes["textContent"] = txt.GetString() ?? "";

                return new DomElement
                {
                    SessionId = _sessionId,
                    TagName = item.TryGetProperty("tagName", out var tag) ? tag.GetString() ?? "" : "",
                    Attributes = item.TryGetProperty("attributes", out var attrs) ? attrs.ToString() : JsonSerializer.Serialize(attributes),
                    InnerHtml = item.TryGetProperty("innerHTML", out var html) ? html.GetString() ?? "" : null,
                    ComputedStyles = item.TryGetProperty("styles", out var styles) ? styles.ToString() : "{}",
                    ElementPath = item.TryGetProperty("path", out var path) ? path.GetString() ?? "" : "",
                    CapturedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaywrightDevTools] Parse DOM element failed: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Performance - Metrics & Timing

        /// <summary>
        /// Captures performance metrics of the page.
        /// </summary>
        public async Task<PerformanceSnapshot?> CapturePerformanceSnapshotAsync()
        {
            if (_page == null)
                throw new InvalidOperationException("Page is not initialized");

            try
            {
                // Retrieve Navigation Timing API data
                var timingScript = @"
                    (function() {
                        const perf = window.performance;
                        const timing = perf.timing;
                        const navigation = perf.navigation;
                        const memory = perf.memory || {};
                        
                        const loadTime = timing.loadEventEnd - timing.navigationStart;
                        const domContentLoaded = timing.domContentLoadedEventEnd - timing.navigationStart;
                        const firstPaint = perf.getEntriesByType('paint').find(e => e.name === 'first-paint');
                        const firstContentfulPaint = perf.getEntriesByType('paint').find(e => e.name === 'first-contentful-paint');
                        
                        const resources = perf.getEntriesByType('resource').map(r => ({
                            name: r.name,
                            duration: r.duration,
                            type: r.initiatorType,
                            transferSize: r.transferSize,
                            startTime: r.startTime
                        }));
                        
                        return {
                            loadTime: loadTime,
                            domContentLoadedTime: domContentLoaded,
                            firstPaintTime: firstPaint ? firstPaint.startTime : 0,
                            firstContentfulPaintTime: firstContentfulPaint ? firstContentfulPaint.startTime : 0,
                            memoryUsed: memory.usedJSHeapSize || 0,
                            memoryLimit: memory.jsHeapSizeLimit || 0,
                            navigationStart: timing.navigationStart,
                            responseEnd: timing.responseEnd - timing.navigationStart,
                            domInteractive: timing.domInteractive - timing.navigationStart,
                            resources: resources,
                            navigationType: navigation.type,
                            redirectCount: navigation.redirectCount
                        };
                    })();
                ";

                var result = await _page.EvaluateAsync<JsonElement>(timingScript);

                var snapshot = new PerformanceSnapshot
                {
                    SessionId = _sessionId,
                    Url = _page.Url,
                    LoadTime = result.TryGetProperty("loadTime", out var lt) ? lt.GetInt64() : 0,
                    DomContentLoadedTime = result.TryGetProperty("domContentLoadedTime", out var dcl) ? dcl.GetInt64() : 0,
                    FirstPaintTime = result.TryGetProperty("firstPaintTime", out var fp) ? (long)fp.GetDouble() : 0,
                    MemoryUsed = result.TryGetProperty("memoryUsed", out var mem) ? mem.GetInt64() : 0,
                    CapturedAt = DateTime.UtcNow,
                    ResourceTimings = result.TryGetProperty("resources", out var res) ? res.ToString() : "[]"
                };

                await _dataService.SavePerformanceSnapshotAsync(snapshot);
                PerformanceSnapshotCaptured?.Invoke(this, snapshot);

                Debug.WriteLine($"[PlaywrightDevTools] Performance snapshot captured: Load={snapshot.LoadTime}ms, DOMContentLoaded={snapshot.DomContentLoadedTime}ms");
                return snapshot;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaywrightDevTools] Performance capture failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets Core Web Vitals metrics.
        /// </summary>
        public async Task<Dictionary<string, double>> GetCoreWebVitalsAsync()
        {
            if (_page == null)
                throw new InvalidOperationException("Page is not initialized");

            try
            {
                var script = @"
                    new Promise((resolve) => {
                        const vitals = {};
                        
                        // LCP - Largest Contentful Paint
                        new PerformanceObserver((list) => {
                            const entries = list.getEntries();
                            const lastEntry = entries[entries.length - 1];
                            vitals.LCP = lastEntry.renderTime || lastEntry.loadTime;
                        }).observe({type: 'largest-contentful-paint', buffered: true});
                        
                        // FID - First Input Delay
                        new PerformanceObserver((list) => {
                            list.getEntries().forEach(entry => {
                                vitals.FID = entry.processingStart - entry.startTime;
                            });
                        }).observe({type: 'first-input', buffered: true});
                        
                        // CLS - Cumulative Layout Shift
                        let clsValue = 0;
                        new PerformanceObserver((list) => {
                            list.getEntries().forEach(entry => {
                                if (!entry.hadRecentInput) {
                                    clsValue += entry.value;
                                    vitals.CLS = clsValue;
                                }
                            });
                        }).observe({type: 'layout-shift', buffered: true});
                        
                        setTimeout(() => resolve(vitals), 1000);
                    });
                ";

                var result = await _page.EvaluateAsync<JsonElement>(script);
                var metrics = new Dictionary<string, double>();

                if (result.TryGetProperty("LCP", out var lcp))
                    metrics["LCP"] = lcp.GetDouble();
                if (result.TryGetProperty("FID", out var fid))
                    metrics["FID"] = fid.GetDouble();
                if (result.TryGetProperty("CLS", out var cls))
                    metrics["CLS"] = cls.GetDouble();

                return metrics;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaywrightDevTools] Core Web Vitals failed: {ex.Message}");
                return new Dictionary<string, double>();
            }
        }

        #endregion

        #region Application - Storage & Cookies

        /// <summary>
        /// Captures all storage types (localStorage, sessionStorage, cookies, indexedDB).
        /// </summary>
        public async Task<List<StorageItem>> CaptureStorageAsync()
        {
            if (_page == null)
                throw new InvalidOperationException("Page is not initialized");

            var items = new List<StorageItem>();

            try
            {
                // LocalStorage
                var localStorageScript = @"
                    (() => {
                        const items = [];
                        for (let i = 0; i < localStorage.length; i++) {
                            const key = localStorage.key(i);
                            items.push({
                                key: key,
                                value: localStorage.getItem(key)
                            });
                        }
                        return items;
                    })();
                ";

                var localStorageResult = await _page.EvaluateAsync<JsonElement>(localStorageScript);
                foreach (var item in localStorageResult.EnumerateArray())
                {
                    var storageItem = new StorageItem
                    {
                        SessionId = _sessionId,
                        StorageType = "localStorage",
                        Key = item.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "",
                        EncryptedValue = item.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "",
                        CapturedAt = DateTime.UtcNow
                    };
                    items.Add(storageItem);
                    await _dataService.SaveStorageItemAsync(storageItem);
                }

                // SessionStorage
                var sessionStorageScript = @"
                    (() => {
                        const items = [];
                        for (let i = 0; i < sessionStorage.length; i++) {
                            const key = sessionStorage.key(i);
                            items.push({
                                key: key,
                                value: sessionStorage.getItem(key)
                            });
                        }
                        return items;
                    })();
                ";

                var sessionStorageResult = await _page.EvaluateAsync<JsonElement>(sessionStorageScript);
                foreach (var item in sessionStorageResult.EnumerateArray())
                {
                    var storageItem = new StorageItem
                    {
                        SessionId = _sessionId,
                        StorageType = "sessionStorage",
                        Key = item.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "",
                        EncryptedValue = item.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "",
                        CapturedAt = DateTime.UtcNow
                    };
                    items.Add(storageItem);
                    await _dataService.SaveStorageItemAsync(storageItem);
                }

                // Cookies
                var cookies = await _page.Context.CookiesAsync();
                foreach (var cookie in cookies)
                {
                    var storageItem = new StorageItem
                    {
                        SessionId = _sessionId,
                        StorageType = "cookies",
                        Key = cookie.Name,
                        EncryptedValue = cookie.Value,
                        Domain = cookie.Domain,
                        ExpiresAt = cookie.Expires > 0 
                            ? DateTimeOffset.FromUnixTimeSeconds((long)cookie.Expires).DateTime 
                            : null,
                        CapturedAt = DateTime.UtcNow
                    };
                    items.Add(storageItem);
                    await _dataService.SaveStorageItemAsync(storageItem);
                }

                // IndexedDB (simplified)
                var indexedDbScript = @"
                    (async () => {
                        const dbs = await window.indexedDB.databases();
                        return dbs.map(db => ({
                            name: db.name,
                            version: db.version
                        }));
                    })();
                ";

                try
                {
                    var indexedDbResult = await _page.EvaluateAsync<JsonElement>(indexedDbScript);
                    foreach (var item in indexedDbResult.EnumerateArray())
                    {
                        var storageItem = new StorageItem
                        {
                            SessionId = _sessionId,
                            StorageType = "indexedDB",
                            Key = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                            EncryptedValue = item.TryGetProperty("version", out var v) ? $"version: {v.GetInt32()}" : "",
                            CapturedAt = DateTime.UtcNow
                        };
                        items.Add(storageItem);
                        await _dataService.SaveStorageItemAsync(storageItem);
                    }
                }
                catch
                {
                    // IndexedDB may not be available
                }

                Debug.WriteLine($"[PlaywrightDevTools] Captured {items.Count} storage items");
                items.ForEach(item => StorageItemCaptured?.Invoke(this, item));

                return items;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaywrightDevTools] Storage capture failed: {ex.Message}");
                return items;
            }
        }

        /// <summary>
        /// Sets a value in localStorage.
        /// </summary>
        public async Task SetLocalStorageAsync(string key, string value)
        {
            if (_page == null)
                throw new InvalidOperationException("Page is not initialized");

            await _page.EvaluateAsync($"localStorage.setItem('{key}', '{value}')");
        }

        /// <summary>
        /// Removes a key from localStorage.
        /// </summary>
        public async Task RemoveLocalStorageAsync(string key)
        {
            if (_page == null)
                throw new InvalidOperationException("Page is not initialized");

            await _page.EvaluateAsync($"localStorage.removeItem('{key}')");
        }

        /// <summary>
        /// Clears all cookies.
        /// </summary>
        public async Task ClearCookiesAsync()
        {
            if (_page == null)
                throw new InvalidOperationException("Page is not initialized");

            await _page.Context.ClearCookiesAsync();
        }

        #endregion

        #region Sources - Resources & Scripts

        /// <summary>
        /// Captures all page resources (scripts, stylesheets, images, etc.).
        /// </summary>
        public async Task<List<PageResource>> CapturePageResourcesAsync()
        {
            if (_page == null)
                throw new InvalidOperationException("Page is not initialized");

            var resources = new List<PageResource>();

            try
            {
                var script = @"
                    (function() {
                        const resources = [];
                        
                        // Scripts
                        document.querySelectorAll('script').forEach(script => {
                            resources.push({
                                type: 'script',
                                url: script.src || 'inline',
                                content: script.src ? '' : script.textContent?.substring(0, 10000)
                            });
                        });
                        
                        // Stylesheets
                        document.querySelectorAll('link[rel=""stylesheet""]').forEach(link => {
                            resources.push({
                                type: 'stylesheet',
                                url: link.href,
                                content: ''
                            });
                        });
                        
                        // Inline styles
                        document.querySelectorAll('style').forEach(style => {
                            resources.push({
                                type: 'inline-style',
                                url: 'inline',
                                content: style.textContent?.substring(0, 10000)
                            });
                        });
                        
                        // Images
                        document.querySelectorAll('img').forEach(img => {
                            resources.push({
                                type: 'image',
                                url: img.src,
                                content: ''
                            });
                        });
                        
                        return resources;
                    })();
                ";

                var result = await _page.EvaluateAsync<JsonElement>(script);

                foreach (var item in result.EnumerateArray())
                {
                    var resource = new PageResource
                    {
                        SessionId = _sessionId,
                        Url = item.TryGetProperty("url", out var url) ? url.GetString() ?? "" : "",
                        Type = item.TryGetProperty("type", out var type) ? type.GetString() ?? "" : "",
                        EncryptedContent = item.TryGetProperty("content", out var content) ? content.GetString() ?? "" : "",
                        CapturedAt = DateTime.UtcNow
                    };

                    // If this is an external resource, try to load its content
                    if (!string.IsNullOrEmpty(resource.Url) && resource.Url != "inline" && string.IsNullOrEmpty(resource.EncryptedContent))
                    {
                        try
                        {
                            // Use fetch via JavaScript to load the resource
                            var fetchScript = $@"
                                fetch('{resource.Url}')
                                    .then(r => r.text())
                                    .catch(e => '');
                            ";
                            var fetchedContent = await _page.EvaluateAsync<string>(fetchScript);
                            if (!string.IsNullOrEmpty(fetchedContent) && fetchedContent.Length > 50000)
                            {
                                resource.EncryptedContent = fetchedContent.Substring(0, 50000) + "... [truncated]";
                            }
                            else
                            {
                                resource.EncryptedContent = fetchedContent;
                            }
                        }
                        catch
                        {
                            // Failed to load external resource
                        }
                    }

                    resources.Add(resource);
                    await _dataService.SavePageResourceAsync(resource);
                    ResourceCaptured?.Invoke(this, resource);
                }

                Debug.WriteLine($"[PlaywrightDevTools] Captured {resources.Count} page resources");
                return resources;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaywrightDevTools] Resources capture failed: {ex.Message}");
                return resources;
            }
        }

        /// <summary>
        /// Gets the HTML content of the page.
        /// </summary>
        public async Task<string> GetPageHtmlAsync()
        {
            if (_page == null)
                throw new InvalidOperationException("Page is not initialized");

            return await _page.ContentAsync();
        }

        /// <summary>
        /// Executes JavaScript code on the page.
        /// </summary>
        public async Task<string> ExecuteScriptAsync(string script)
        {
            if (_page == null)
                throw new InvalidOperationException("Page is not initialized");

            try
            {
                var result = await _page.EvaluateAsync<JsonElement>(script);
                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        #endregion

        #region Network Monitoring

        /// <summary>
        /// Monitors all network requests.
        /// </summary>
        public void EnableNetworkMonitoring(Action<string, string, int> onRequest)
        {
            if (_page == null)
                throw new InvalidOperationException("Page is not initialized");

            _page.Request += (_, request) =>
            {
                Debug.WriteLine($"[Network] Request: {request.Method} {request.Url}");
            };

            _page.Response += (_, response) =>
            {
                onRequest?.Invoke(response.Request.Method, response.Url, response.Status);
                Debug.WriteLine($"[Network] Response: {response.Status} {response.Url}");
            };
        }

        #endregion

        public string CurrentUrl => _page?.Url ?? "";
        public string CurrentTitle => _page?.TitleAsync().Result ?? "";
        public string SessionId => _sessionId;
        public bool IsInitialized => _isInitialized;

        public void Dispose()
        {
            if (_page != null)
                _page.CloseAsync().Wait();
            if (_browser != null)
                _browser.CloseAsync().Wait();
            _playwright?.Dispose();
            _dataService?.Dispose();

            Debug.WriteLine("[PlaywrightDevTools] Disposed");
        }
    }
}

