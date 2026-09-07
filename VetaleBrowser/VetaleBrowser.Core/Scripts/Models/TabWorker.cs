using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using VetaleBrowser.VetaleBrowser.Core.Scripts.Browser;
using Avalonia.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using VetaleBrowser.VetaleBrowser.Core.Services.Windows;
using VetaleBrowser.VetaleBrowser.Core.Services;
using VetaleBrowser.VetaleBrowser.Core.Scripts.ErrorHandlers;
using VetaleBrowser.VetaleBrowser.UI.Services;
using Avalonia.Controls;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.Models
{
    /// <summary>
    /// Represents a single entry in the tab navigation history    /// </summary>
    public class NavigationEntry
    {
        /// <summary>Page URL (can be either http(s):// or vetale://)</summary>
        public string Url { get; set; } = string.Empty;
        
        /// <summary>Page title</summary>
        public string? Title { get; set; }
        
        /// <summary>Is this an internal page (vetale://)</summary>
        public bool IsInternal { get; set; }
        
        /// <summary>UserControl for internal pages (VetaleSearchHomePage, etc.)</summary>
        public UserControl? InternalPageContent { get; set; }
        
        /// <summary>Timestamp of when the entry was created</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Manages the navigation history for a tab (forward/back stack)
    /// MEMORY OPTIMIZED: Limited history size, cleanup of old entries
    /// </summary>
    public class NavigationHistory
    {
        private readonly List<NavigationEntry> _entries = new();
        private int _currentIndex = -1;
        
        // MEMORY OPTIMIZATION: Limit history size
        private const int MaxHistoryEntries = 50;

        public event EventHandler? HistoryChanged;

        /// <summary>Current entry in the history</summary>
        public NavigationEntry? CurrentEntry => _currentIndex >= 0 && _currentIndex < _entries.Count 
            ? _entries[_currentIndex] 
            : null;

        /// <summary>Whether it is possible to go back</summary>
        public bool CanGoBack => _currentIndex > 0;

        /// <summary>Whether it is possible to go forward</summary>
        public bool CanGoForward => _currentIndex >= 0 && _currentIndex < _entries.Count - 1;

        /// <summary>Adds a new entry to history (removes all "forward" entries)</summary>
        public void AddEntry(NavigationEntry entry)
        {
            // DEDUP: typing the same URL twice or re-firing AddressChanged
            // must not create phantom entries that break Back/Forward.
            if (CurrentEntry != null &&
                string.Equals(CurrentEntry.Url, entry.Url, StringComparison.OrdinalIgnoreCase))
            {
                // Refresh content holder if a newer one arrived.
                if (entry.InternalPageContent != null)
                    CurrentEntry.InternalPageContent = entry.InternalPageContent;
                CurrentEntry.Title = entry.Title ?? CurrentEntry.Title;
                return;
            }

            // Remove all entries after the current one (on new navigation)
            if (_currentIndex < _entries.Count - 1)
            {
                // MEMORY OPTIMIZATION: Clear InternalPageContent before removal
                for (int i = _currentIndex + 1; i < _entries.Count; i++)
                {
                    _entries[i].InternalPageContent = null;
                }
                _entries.RemoveRange(_currentIndex + 1, _entries.Count - _currentIndex - 1);
            }

            _entries.Add(entry);
            _currentIndex = _entries.Count - 1;
            
            // MEMORY OPTIMIZATION: Remove oldest entries if over limit
            while (_entries.Count > MaxHistoryEntries && _currentIndex > 0)
            {
                _entries[0].InternalPageContent = null; // Clear reference
                _entries.RemoveAt(0);
                _currentIndex--;
            }
            
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Goes back one entry in history</summary>
        public NavigationEntry? GoBack()
        {
            if (!CanGoBack) return null;
            
            _currentIndex--;
            HistoryChanged?.Invoke(this, EventArgs.Empty);
            return CurrentEntry;
        }

        /// <summary>Goes forward one entry in history</summary>
        public NavigationEntry? GoForward()
        {
            if (!CanGoForward) return null;
            
            _currentIndex++;
            HistoryChanged?.Invoke(this, EventArgs.Empty);
            return CurrentEntry;
        }

        /// <summary>Clears the entire history</summary>
        public void Clear()
        {
            // MEMORY OPTIMIZATION: Clear all references first
            foreach (var entry in _entries)
            {
                entry.InternalPageContent = null;
            }
            _entries.Clear();
            _currentIndex = -1;
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Number of entries in history</summary>
        public int Count => _entries.Count;
    }

    /// <summary>
    /// Represents a browser worker that owns its own WebView (CefGlue-based via WebViewControl) and navigation manager.
    /// </summary>
    public sealed class TabWorker : IDisposable, INotifyPropertyChanged
    {
        public Guid Id { get; } = Guid.NewGuid();

        public IBrowserView WebView { get; }
        public GlobalManagers.WebViewManager Manager { get; }
        public NavigationHistory History { get; } = new NavigationHistory();
        // TabWorker owns the browser history. Chromium's history is deliberately
        // not exposed here because it also contains redirects and programmatic
        // loads which do not belong to the tab's navigation stack.
        // PRO-style: unified state = own history OR engine history (in-page navigations).
        public bool CanGoBack => History.CanGoBack || (WebView?.CanGoBack == true);
        public bool CanGoForward => History.CanGoForward || (WebView?.CanGoForward == true);
        
        /// <summary>
        /// WebView error handler for localization of CefGlue errors
        /// </summary>
        public WebViewErrorHandler ErrorHandler { get; }

        private string? _title;
        private string? _address;
        private bool _isActive;
        private bool _isMuted;
        
        // Track OS process IDs associated with this tab's rendering/audible activity (best-effort, OS-level, non-CEF-specific)
        private readonly HashSet<int> _relatedPids = new();
        private readonly DispatcherTimer _pidRefreshTimer = new() { Interval = TimeSpan.FromSeconds(2) };
        
        // Timer for delayed mute reapplication after navigation
        private readonly DispatcherTimer _muteReapplyTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
        private int _muteReapplyCount;
        private readonly SemaphoreSlim _muteApplyLock = new(1, 1);

        // Fullscreen poller to catch content-initiated fullscreen when events aren't exposed
        // (DevTools evaluate per poll - 750ms щоб не душити localhost сокетами)
        private readonly DispatcherTimer _fullscreenPollTimer = new() { Interval = TimeSpan.FromMilliseconds(750) };
        private bool _lastFullscreenState;

        // New: subprocess launched per tab (system-level process)
        private readonly TabSubprocessService _subprocess;
        
        // Flag to prevent recursive calls during navigation
        private bool _isNavigating;
        private string? _pendingNavigationUrl;

        // Monotonic navigation generation: stale async pre-checks must not
        // override a newer navigation or a Back/Forward step.
        private int _navSeq;

        private string? _prevAddress;
        private bool _historyNavigationInProgress;
        private string? _historyNavigationTarget;
        private bool _ignoreNextProgrammaticAddressChange;
        private bool _blockingDownloadNav;
        private static readonly HttpClient _httpHead = new HttpClient() { Timeout = TimeSpan.FromSeconds(10) };

        public string? Title
        {
            get => _title;
            private set { if (_title != value) { _title = value; OnPropertyChanged(); TitleChanged?.Invoke(this, value); } }
        }

        public string? Address
        {
            get => _address;
            private set { if (_address != value) { _address = value; OnPropertyChanged(); AddressChanged?.Invoke(this, value); } }
        }

        public bool IsActive
        {
            get => _isActive;
            set { if (_isActive != value) { _isActive = value; OnPropertyChanged(); } }
        }

        public bool IsMuted
        {
            get => _isMuted;
            set 
            { 
                if (_isMuted != value) 
                { 
                    _isMuted = value; 
                    OnPropertyChanged(); 
                    ApplyMuteState();
                    
                    // If mute is enabled, inject interceptor to capture new AudioContext instances
                    if (_isMuted)
                    {
                        _ = InjectAudioInterceptorAsync();
                    }
                } 
            }
        }

        public event EventHandler<string?>? TitleChanged;
        public event EventHandler<string?>? AddressChanged;
        public event EventHandler<string?>? FaviconChanged;
        public string? FaviconUrl { get; private set; }
        public event EventHandler<bool>? FullscreenChanged;
        public event EventHandler<NavigationEntry>? NavigationChanged;
        
        /// <summary>
        /// Event fired when an error occurs with localized content
        /// </summary>
        public event EventHandler<BrowserErrorEventArgs>? ErrorOccurred;

        public TabWorker()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Constructor started");
                
                // Start dedicated subprocess for this tab (created via the OS) and track its PID
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Creating subprocess service...");
                _subprocess = new TabSubprocessService(Id);
                if (_subprocess.ProcessId.HasValue)
                {
                    _relatedPids.Add(_subprocess.ProcessId.Value);
                    System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Subprocess PID: {_subprocess.ProcessId.Value}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Subprocess has no PID");
                }

                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Creating WebView...");
                WebView = new CefGlueAdapter();
                WebView.View.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
                WebView.View.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
                
                if (WebView == null)
                {
                    throw new InvalidOperationException("WebView creation returned null");
                }
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] WebView created successfully");

                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Creating WebViewManager...");
                Manager = new GlobalManagers.WebViewManager();
                if (Manager == null)
                {
                    throw new InvalidOperationException("WebViewManager creation returned null");
                }
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] WebViewManager created");
                
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Initializing Manager...");
                Manager.Initialize(WebView);
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Manager initialized");
                
                // Initialize error handler for localized error pages
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Creating ErrorHandler...");
                ErrorHandler = new WebViewErrorHandler(WebView);
                ErrorHandler.ErrorOccurred += OnErrorHandlerError;
                ErrorHandler.Attach();
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] ErrorHandler initialized");

                // Forward manager-initiated navigations to Address property
                Manager.Navigated += (_, url) => Address = url;

                // Observe WebView property changes to keep state up-to-date
                WebView.PropertyChanged += WebViewOnPropertyChanged;

                // Initialize subprocess title
                TryUpdateSubprocessTitle();
                
                // Subscribe to navigation history changes
                History.HistoryChanged += (_, __) => OnPropertyChanged(nameof(History));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] ERROR in constructor (before hooks): {ex}");
                throw;
            }

            try
            {
                // Try to hook fullscreen events via reflection
                TryHookFullscreenEvents();

                // Also inject client-side navigation guards to keep navigation in-tab
                InjectNavigationGuards();

                // Start polling for fullscreen changes as a robust fallback
                _fullscreenPollTimer.Tick += (_, __) => PollFullscreenAsync();
                _fullscreenPollTimer.Start();

                // Periodically refresh related PIDs shortly after navigation/content changes
                _pidRefreshTimer.Tick += (_, __) => RefreshRelatedProcessesBestEffort();
                
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Constructor completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] ERROR in constructor (hooks): {ex}");
                // Continue - these are non-critical features
            }
        }

        private void WebViewOnPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
        {
            // CefSharp.Avalonia reports URL changes as "Url" while the
            // browser abstraction exposes them as Address.
            if (e.Property?.Name is "Address" or "Url")
            {
                var newAddr = WebView.Address;
                var prev = _prevAddress;
                _prevAddress = newAddr;
                
                // Check if this is an external protocol and block it
                if (!string.IsNullOrEmpty(newAddr) && IsExternalProtocol(newAddr))
                {
                    System.Diagnostics.Debug.WriteLine($"[TabWorker] BLOCKED external protocol: {newAddr}");
                    // Go back to the previous page
                    if (!string.IsNullOrEmpty(prev))
                    {
                        try { WebView.Address = prev; } catch { }
                    }
                    return;
                }
                
                // Re-inject JavaScript guards after each navigation
                InjectNavigationGuards();

                // SYNC in-page navigations (link clicks, form submits, JS navigations)
                // into tab history. Without this, Back/Forward buttons never activate
                // because History stays at 1 entry. Dedup in AddEntry protects against
                // double-recording of programmatic Navigate()/GoBack()/GoForward().
                if (!string.IsNullOrEmpty(newAddr))
                {
                    if (_historyNavigationInProgress)
                    {
                        Address = newAddr;
                        // Engine-driven Back/Forward (target==null): sync tab history
                        // so Back/Forward buttons, address bar and favicon stay correct.
                        if (string.IsNullOrWhiteSpace(_historyNavigationTarget))
                        {
                            var cur2 = History.CurrentEntry;
                            if (cur2 == null || !string.Equals(cur2.Url, newAddr, StringComparison.OrdinalIgnoreCase))
                            {
                                History.AddEntry(new NavigationEntry { Url = newAddr, IsInternal = false, Timestamp = DateTime.UtcNow });
                                NavigationChanged?.Invoke(this, History.CurrentEntry!);
                            }
                            _historyNavigationInProgress = false;
                            _ = RefreshFaviconAsync();
                        }
                        else if (string.Equals(_historyNavigationTarget, newAddr, StringComparison.OrdinalIgnoreCase))
                        {
                            _historyNavigationInProgress = false;
                            _historyNavigationTarget = null;
                        }
                        return;
                    }

                    if (_ignoreNextProgrammaticAddressChange)
                    {
                        _ignoreNextProgrammaticAddressChange = false;
                        Address = newAddr;
                        return;
                    }

                    var cur = History.CurrentEntry;
                    if (cur == null || !string.Equals(cur.Url, newAddr, StringComparison.OrdinalIgnoreCase))
                    {
                        var inPageEntry = new NavigationEntry
                        {
                            Url = newAddr,
                            IsInternal = false,
                            Timestamp = DateTime.UtcNow
                        };
                        History.AddEntry(inPageEntry);
                        Address = newAddr;
                        NavigationChanged?.Invoke(this, inPageEntry);
                        System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] In-page nav recorded: {newAddr}");
                    }
                    else if (!string.Equals(Address, newAddr, StringComparison.OrdinalIgnoreCase))
                    {
                        Address = newAddr;
                    }
                }
                
                // Re-apply mute state after each navigation
                // This ensures that mute works for new content (games, WebGL, new tabs)
                if (_isMuted)
                {
                    // Give time for new content to initialize
                    ScheduleMuteReapply();
                }
            }
            else if (e.Property?.Name == "Title")
            {
                var t = WebView.Title;
                if (!string.IsNullOrWhiteSpace(t))
                {
                    Title = t.Trim();
                    TryUpdateSubprocessTitle();
                    _ = RefreshFaviconAsync();
                }
            }
            else if (e.Property?.Name == "CanGoBack" || e.Property?.Name == "CanGoForward")
            {
                OnPropertyChanged(nameof(History));
                if (History.CurrentEntry != null)
                    NavigationChanged?.Invoke(this, History.CurrentEntry);
                else
                {
                    OnPropertyChanged(nameof(CanGoBack));
                    OnPropertyChanged(nameof(CanGoForward));
                }
            }
        }
        
        /// <summary>
        /// Checks whether the URL is an external protocol (intent://, tel://, mailto://, etc.)
        /// </summary>
        private static bool IsExternalProtocol(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            
            var u = url.Trim().ToLowerInvariant();
            
            // Allowed protocols - everything within the browser
            if (u.StartsWith("http://") || u.StartsWith("https://") || 
                u.StartsWith("file://") || u.StartsWith("data:") || 
                u.StartsWith("javascript:") || u.StartsWith("blob:") ||
                u.StartsWith("about:") || u.StartsWith("vetale:") ||
                u.StartsWith("chrome:") || u.StartsWith("edge:") ||
                u.StartsWith("brave:") || u.StartsWith("opera:") ||
                u.StartsWith("chromium:") || u.StartsWith("view-source:"))
            {
                return false;
            }
            
            // Blocked protocols
            string[] blockedProtocols = {
                "intent:", "android-app:", "market:", "tel:", "mailto:", 
                "sms:", "whatsapp:", "tg:", "viber:", "skype:", "zoom:",
                "ms-", "vnd.", "app:", "custom:", "myapp:"
            };
            
            foreach (var p in blockedProtocols)
            {
                if (u.StartsWith(p) || u.Contains("://" + p)) return true;
            }
            
            // If there is a ":" within the first 20 characters and it is not http/https - block
            var colonIndex = u.IndexOf(':');
            if (colonIndex > 0 && colonIndex < 20)
            {
                var protocol = u.Substring(0, colonIndex);
                // Check whether this is not a regular URL
                if (protocol != "http" && protocol != "https" && protocol != "file" && 
                    protocol != "data" && protocol != "javascript" && protocol != "blob" &&
                    protocol != "about" && protocol != "vetale" && protocol != "chrome" &&
                    protocol != "edge" && protocol != "brave" && protocol != "opera" &&
                    protocol != "chromium" && protocol != "view-source")
                {
                    return true;
                }
            }
            
            return false;
        }

        private void SchedulePidRefreshBurst()
        {
            try
            {
                // Run a few quick refresh cycles to catch newly spawned render processes after navigation
                int remaining = 3;
                _pidRefreshTimer.Tag = remaining; // store state via Tag-like pattern using extension below
                _pidRefreshTimer.Stop();
                _pidRefreshTimer.Tick -= PidRefreshTick;
                _pidRefreshTimer.Tick += PidRefreshTick;
                _pidRefreshTimer.Start();
            }
            catch { }
        }

        private void PidRefreshTick(object? sender, EventArgs e)
        {
            RefreshRelatedProcessesBestEffort();
            if (_pidRefreshTimer.Tag is int left)
            {
                left--;
                if (left <= 0)
                {
                    _pidRefreshTimer.Stop();
                    _pidRefreshTimer.Tick -= PidRefreshTick;
                    _pidRefreshTimer.Tag = null;
                }
                else
                {
                    _pidRefreshTimer.Tag = left;
                }
            }
        }

        private void RefreshRelatedProcessesBestEffort()
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

                // Claim new child PIDs not yet assigned to other tabs
                var newOnes = TabProcessTracker.ClaimNewChildPids();
                if (newOnes.Count == 0) return;

                foreach (var pid in newOnes)
                {
                    _relatedPids.Add(pid);
                }
            }
            catch { }
        }

        private async void PollFullscreenAsync()
        {
            try
            {
                // Primary: DevTools Runtime.evaluate (JS bridge in this wrapper is a stub).
                // Single source of the 2 states lives here on the worker;
                // the window only hosts containers.
                bool? viaDevTools = await VetaleBrowser.Core.Scripts.Browser.CefDevToolsClient.IsDocumentFullscreenAsync(Address);
                bool isFs = viaDevTools ?? await EvaluateScriptAsBoolAsync("!!(document.fullscreenElement||document.webkitFullscreenElement||document.msFullscreenElement)");
                if (isFs != _lastFullscreenState)
                {
                    _lastFullscreenState = isFs;
                    FullscreenChanged?.Invoke(this, isFs);
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Explicitly exits document fullscreen (no toggle) — for the way back.
        /// </summary>
        public async System.Threading.Tasks.Task RequestVideoFullscreenExitAsync()
        {
            try
            {
                await VetaleBrowser.Core.Scripts.Browser.CefDevToolsClient.EvaluateBooleanAsync(
                    Address, "!!(document.exitFullscreen? (document.exitFullscreen(), true) : false)");
            }
            catch { }
        }
        /// <summary>
        /// Toggles the 2 video-fullscreen states on the worker (not on the window):
        /// requests/exits document fullscreen via DevTools; the poller syncs state
        /// and the window follows with the video page. No-op when DevTools is down.
        /// </summary>
        public async System.Threading.Tasks.Task<bool?> RequestVideoFullscreenToggleAsync()
        {
            try
            {
                var current = await VetaleBrowser.Core.Scripts.Browser.CefDevToolsClient.IsDocumentFullscreenAsync(Address);
                if (current == null)
                    return null;
                if (current == true)
                {
                    await VetaleBrowser.Core.Scripts.Browser.CefDevToolsClient.EvaluateBooleanAsync(
                        Address, "!!(document.exitFullscreen? (document.exitFullscreen(), true) : false)");
                }
                else
                {
                    await VetaleBrowser.Core.Scripts.Browser.CefDevToolsClient.EvaluateBooleanAsync(
                        Address, "(function(){var v=document.querySelector('video');if(v){if(v.requestFullscreen){v.requestFullscreen();return true;}if(v.webkitRequestFullscreen){v.webkitRequestFullscreen();return true;}}var p=document.querySelector('#player')||document.documentElement;if(p&&p.requestFullscreen){p.requestFullscreen();return true;}return false;})()");
                }
                return current == false;
            }
            catch
            {
                return null;
            }
        }

        private async System.Threading.Tasks.Task<bool> EvaluateScriptAsBoolAsync(string script)
        {
            try
            {
                // Use direct EvaluateScript from WebViewControl (returns Task<object>)
                var result = await WebView.EvaluateScriptAsync<object>(script);
                
                if (result is bool b) return b;
                if (result is string s)
                {
                    if (bool.TryParse(s.Trim(), out var parsed)) return parsed;
                    if (string.Equals(s.Trim(), "1", StringComparison.Ordinal)) return true;
                    if (string.Equals(s.Trim(), "0", StringComparison.Ordinal)) return false;
                    // Check if it's "true" or "false" in lowercase
                    if (string.Equals(s.Trim(), "true", StringComparison.OrdinalIgnoreCase)) return true;
                    if (string.Equals(s.Trim(), "false", StringComparison.OrdinalIgnoreCase)) return false;
                }
                if (result is int i) return i != 0;
                if (result is long l) return l != 0;
                
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker] Script evaluation failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Navigate to a URL (supports both regular http(s):// and internal vetale:// URLs)
        /// MEMORY OPTIMIZATION: Forced memory cleanup on navigation
        /// </summary>
        public async void Navigate(string url, UserControl? internalPageContent = null)
        {
            if (_isNavigating) return; // Prevent recursion
            _ignoreNextProgrammaticAddressChange = false;
            
            try
            {
                _isNavigating = true;
                
                if (string.IsNullOrWhiteSpace(url))
                {
                    System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Navigate: empty URL");
                    return;
                }

                // Skip no-op re-navigation to the same URL (prevents history spam
                // from address-bar sync loops and double Enter presses).
                if (string.Equals(Address, url, StringComparison.OrdinalIgnoreCase) &&
                    History.CurrentEntry != null &&
                    string.Equals(History.CurrentEntry.Url, url, StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Navigate: same URL, skipped: {url}");
                    return;
                }

                // A single address submission can be observed more than once
                // while the previous pre-check is still running. Do not queue
                // the same browser load repeatedly; CefSharp can fail when
                // several loads are started for one request.
                if (string.Equals(_pendingNavigationUrl, url, StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Navigate: duplicate pending URL, skipped: {url}");
                    return;
                }
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Navigate: {url}");

                // MEMORY OPTIMIZATION: Forced memory cleanup before navigation
                // This is critical for AMD cards (RX 5700 XT) where WebView textures are not released
                TriggerMemoryCleanup();

                // Determine whether this is an internal URL
                // (vetale:// pages + chrome:// pages served built-in - engine has no WebUI scheme)
                bool isInternal = url.StartsWith("vetale://", StringComparison.OrdinalIgnoreCase)
                    || ChromiumInternalHandler.IsChromiumInternalUrl(url);

                // Set pending URL for error handler
                ErrorHandler?.SetPendingNavigation(url);

                if (isInternal && internalPageContent == null)
                {
                    // Built-in chrome:// pages (gpu, version): create content here
                    // so back/forward and address-bar navigation both work
                    internalPageContent = ChromiumInternalHandler.CreatePageContent(url);
                }

                // Create a history entry
                var entry = new NavigationEntry
                {
                    Url = url,
                    IsInternal = isInternal,
                    InternalPageContent = internalPageContent,
                    Timestamp = DateTime.UtcNow
                };

                // Add to history
                History.AddEntry(entry);

                // Update Address
                Address = url;

                if (isInternal)
                {
                    // For internal URLs, do not call WebView.Navigate
                    System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Internal navigation to: {url}");
                    
                    // Title will be set via the NavigationChanged event
                    Title = GetInternalPageTitle(url);
                }
                else
                {
                    // For external URLs - optionally check availability via HTTP pre-check
                    // This provides fast detection of DNS errors and unreachable servers
                    System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] External navigation to: {url}");
                    
                    // Pre-check for fast error detection (does not block navigation)
                    _ignoreNextProgrammaticAddressChange = true;
                    _pendingNavigationUrl = url;
                    _navSeq++;
                    _ = PreCheckAndNavigateAsync(url, _navSeq);
                }

                // Notify about navigation change
                NavigationChanged?.Invoke(this, entry);
                
                // MEMORY OPTIMIZATION: Cleanup after navigation
                _ = Task.Delay(500).ContinueWith(_ => TriggerMemoryCleanup());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Navigate error: {ex.Message}");
            }
            finally
            {
                _isNavigating = false;
            }
        }
        
        /// <summary>
        /// Async URL check and navigation
        /// </summary>
        private async Task PreCheckAndNavigateAsync(string url, int seq)
        {
            try
            {
                // CRITICAL: Block external protocols BEFORE any navigation
                if (IsExternalProtocol(url))
                {
                    System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] BLOCKED external protocol in PreCheck: {url}");
                    return; // Do nothing - just block
                }

                // Check URL availability via HTTP HEAD request
                if (ErrorHandler != null)
                {
                    var (isSuccess, errorCode, errorMessage) = await ErrorHandler.PreCheckUrlAsync(url);

                    // A newer Navigate or a Back/Forward step superseded this one:
                    // never touch the WebView or the error UI for a stale URL.
                    if (seq != _navSeq)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] PreCheck result stale for {url}, skipping");
                        return;
                    }

                    if (!isSuccess)
                    {
                        _ignoreNextProgrammaticAddressChange = false;
                        System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] PreCheck failed: {errorCode} - {errorMessage}");

                        // Report the error via ErrorHandler
                        // This will show the error page faster than WebView
                        if (errorCode >= 400)
                        {
                            ErrorHandler.ReportHttpError(errorCode, url);
                        }
                        else
                        {
                            ErrorHandler.ReportError(errorCode, url, errorMessage);
                        }
                        return; // Do not navigate in WebView
                    }
                }

                if (seq != _navSeq) return;

                // If pre-check passed - navigate in WebView
                await Manager.NavigateAsync(url);
            }
            catch (Exception ex)
            {
                _ignoreNextProgrammaticAddressChange = false;
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] PreCheckAndNavigateAsync error: {ex.Message}");
                // Fallback - navigate in WebView directly (only if not an external protocol
                // and still the latest navigation)
                if (seq == _navSeq && !IsExternalProtocol(url))
                {
                    await Manager.NavigateAsync(url);
                }
            }
            finally
            {
                if (seq == _navSeq &&
                    string.Equals(_pendingNavigationUrl, url, StringComparison.OrdinalIgnoreCase))
                {
                    _pendingNavigationUrl = null;
                }
            }
        }
        
        /// <summary>
        /// Error handler from WebViewErrorHandler
        /// Forwards the event to TabWorker subscribers
        /// </summary>
        private void OnErrorHandlerError(object? sender, BrowserErrorEventArgs e)
        {
            try
            {
                Debug.WriteLine($"[TabWorker {Id}] Error received: {e.Error.Title}");
                ErrorOccurred?.Invoke(this, e);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TabWorker {Id}] OnErrorHandlerError failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Reports a CefGlue error manually
        /// Can be called from outside when an error is detected
        /// </summary>
        public void ReportError(int errorCode, string failedUrl, string? errorText = null)
        {
            ErrorHandler?.ReportError(errorCode, failedUrl, errorText);
        }
        
        /// <summary>
        /// Reports an HTTP server error
        /// </summary>
        public void ReportHttpError(int httpStatusCode, string failedUrl)
        {
            ErrorHandler?.ReportHttpError(httpStatusCode, failedUrl);
        }
        
        /// <summary>
        /// MEMORY OPTIMIZATION: Forced memory release
        /// Critical for AMD cards where GPU textures are not released automatically
        /// </summary>
        private static int _lastGcGeneration = 0;
        private void TriggerMemoryCleanup()
        {
            try
            {
                // Do not call GC too often (at least once every 3 seconds)
                var currentGen = GC.CollectionCount(2);
                if (currentGen == _lastGcGeneration)
                {
                    // No GC Gen2 has occurred yet - we can run it
                    GC.Collect(1, GCCollectionMode.Optimized, false);
                }
                _lastGcGeneration = GC.CollectionCount(2);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TabWorker] Memory cleanup error: {ex.Message}");
            }
        }

        /// <summary>
        /// Goes back one page in history (PRO behaviour: internal history first,
        /// then engine history for in-page steps like SPA/redirects).
        /// </summary>
        public void GoBack()
        {
            // Internal page target -> always via tab history (engine knows nothing about vetale://).
            var prev = History.CanGoBack ? PeekBack() : null;
            if (prev != null && prev.IsInternal)
            {
                var entry = History.GoBack();
                if (entry != null) NavigateToHistoryEntry(entry);
                return;
            }
            // External: prefer tab history so address bar + tab title stay in sync,
            // fall back to engine history when tab history is empty (e.g. iframe/SPA steps).
            if (History.CanGoBack)
            {
                var entry = History.GoBack();
                if (entry != null) NavigateToHistoryEntry(entry);
                return;
            }
            try
            {
                if (WebView?.CanGoBack == true)
                {
                    _historyNavigationInProgress = true;
                    _historyNavigationTarget = null; // accept whatever engine lands on
                    WebView.GoBack();
                }
            }
            catch { }
        }

        private NavigationEntry? PeekBack()
        {
            try
            {
                var f = typeof(NavigationHistory).GetField("_entries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var idxF = typeof(NavigationHistory).GetField("_currentIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f?.GetValue(History) is System.Collections.IList list && idxF?.GetValue(History) is int idx && idx > 0)
                    return list[idx - 1] as NavigationEntry;
            }
            catch { }
            return null;
        }

        private NavigationEntry? PeekForward()
        {
            try
            {
                var f = typeof(NavigationHistory).GetField("_entries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var idxF = typeof(NavigationHistory).GetField("_currentIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f?.GetValue(History) is System.Collections.IList list && idxF?.GetValue(History) is int idx && idx + 1 < list.Count)
                    return list[idx + 1] as NavigationEntry;
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Goes forward one page in history (PRO behaviour, mirror of GoBack).
        /// </summary>
        public void GoForward()
        {
            var next = History.CanGoForward ? PeekForward() : null;
            if (next != null && next.IsInternal)
            {
                var entry = History.GoForward();
                if (entry != null) NavigateToHistoryEntry(entry);
                return;
            }
            if (History.CanGoForward)
            {
                var entry = History.GoForward();
                if (entry != null) NavigateToHistoryEntry(entry);
                return;
            }
            try
            {
                if (WebView?.CanGoForward == true)
                {
                    _historyNavigationInProgress = true;
                    _historyNavigationTarget = null;
                    WebView.GoForward();
                }
            }
            catch { }
        }

        /// <summary>
        /// Navigate to an existing history entry (without adding a new entry)
        /// </summary>
        private async void NavigateToHistoryEntry(NavigationEntry entry)
        {
            if (_isNavigating) return;
            
            try
            {
                _isNavigating = true;

                Address = entry.Url;
                ErrorHandler?.SetPendingNavigation(entry.Url);

                if (entry.IsInternal)
                {
                    if (entry.InternalPageContent == null)
                    {
                        entry.InternalPageContent = VetaleBrowser.UI.Services.InternalUrlHandler.CreatePageContent(entry.Url)
                            ?? ChromiumInternalHandler.CreatePageContent(entry.Url);
                    }
                    else
                    {
                        // Refresh query state: forward/back between vetale://search and
                        // vetale://search?q=... reuses the cached control, so push the
                        // query from the URL into it, otherwise stale content is shown.
                        RefreshInternalPageQuery(entry);
                    }
                    Title = entry.Title ?? GetInternalPageTitle(entry.Url);
                }
                else
                {
                    // Invalidate any stale PreCheck from a previous Navigate()
                    // and use the same navigation path as Navigate() (Address setter
                    // via Manager.NavigateAsync). LoadUrl/NavigateAsync of the
                    // underlying adapter proved unreliable for history steps,
                    // leaving the old WebView content on screen.
                    _navSeq++;
                    _historyNavigationTarget = entry.Url;
                    _historyNavigationInProgress = true;
                    await Manager.NavigateWithoutHistoryAsync(entry.Url);
                }

                NavigationChanged?.Invoke(this, entry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] NavigateToHistoryEntry error: {ex.Message}");
            }
            finally
            {
                if (entry.IsInternal)
                {
                    _historyNavigationInProgress = false;
                    _historyNavigationTarget = null;
                }
                _isNavigating = false;
            }
        }

        /// <summary>
        /// Pushes the ?q= query from the entry URL into a cached internal page,
        /// so Back/Forward between search home and search-with-query shows the
        /// correct state instead of stale content.
        /// </summary>
        private static void RefreshInternalPageQuery(NavigationEntry entry)
        {
            try
            {
                var content = entry.InternalPageContent;
                if (content == null) return;
                var q = VetaleBrowser.UI.Services.InternalUrlHandler.GetQueryParameter(entry.Url, "q");
                if (content is VetaleBrowser.UI.Pages.VetaleSearchHomePage home)
                {
                    if (!string.IsNullOrWhiteSpace(q)) home.SetQuery(q!);
                }
                else if (content is VetaleBrowser.UI.Pages.VetaleSearchResultsPage results)
                {
                    if (!string.IsNullOrWhiteSpace(q))
                    {
                        var mode = VetaleBrowser.UI.Services.InternalUrlHandler.GetQueryParameter(entry.Url, "mode");
                        results.SetSearchQuery(q!, mode);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker] RefreshInternalPageQuery error: {ex.Message}");
            }
        }

        /// <summary>
        /// PRO-style live favicon: reads &lt;link rel="icon"&gt; from the page via DevTools,
        /// falls back to /favicon.ico convention. Fires FaviconChanged for the UI.
        /// </summary>
        public async Task RefreshFaviconAsync()
        {
            try
            {
                var addr = Address ?? WebView?.Address;
                if (string.IsNullOrWhiteSpace(addr)) return;
                if (addr.StartsWith("vetale://", StringComparison.OrdinalIgnoreCase) ||
                    addr.StartsWith("chrome://", StringComparison.OrdinalIgnoreCase) ||
                    addr.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
                    return; // internal pages use bundled icons (handled by MainWindow)
                string? icon = null;
                try
                {
                    icon = await CefDevToolsClient.EvaluateStringAsync(
                        addr, "(function(){try{var l=document.querySelector('link[rel~=\"icon\"]');if(l&&l.href)return l.href;return location.origin+'/favicon.ico';}catch(e){return null;}})()");
                }
                catch { }
                if (string.IsNullOrWhiteSpace(icon)) return;
                if (string.Equals(FaviconUrl, icon, StringComparison.OrdinalIgnoreCase)) return;
                FaviconUrl = icon;
                FaviconChanged?.Invoke(this, icon);
            }
            catch { }
        }

        /// <summary>
        /// Gets the title for an internal page by URL
        /// </summary>
        private string GetInternalPageTitle(string url)
        {
            if (url.StartsWith("vetale://search?", StringComparison.OrdinalIgnoreCase))
            {
                // Extract the query from the URL
                try
                {
                    var uri = new Uri(url);
                    var queryParams = uri.Query.TrimStart('?').Split('&')
                        .Select(p => p.Split('='))
                        .Where(parts => parts.Length == 2)
                        .ToDictionary(parts => parts[0], parts => Uri.UnescapeDataString(parts[1]));
                    
                    if (queryParams.TryGetValue("q", out var q) && !string.IsNullOrWhiteSpace(q))
                    {
                        return $"{q} - Vetale Search";
                    }
                }
                catch { }
                
                return "Vetale Search";
            }
            else if (url.Equals("vetale://search", StringComparison.OrdinalIgnoreCase))
            {
                return "Vetale Search";
            }
            else if (url.StartsWith("vetale://bookmarks", StringComparison.OrdinalIgnoreCase))
            {
                return "Закладки";
            }
            else if (url.StartsWith("vetale://history", StringComparison.OrdinalIgnoreCase))
            {
                return "Історія";
            }
            else if (url.StartsWith("vetale://settings", StringComparison.OrdinalIgnoreCase))
            {
                return "Налаштування";
            }
            else if (url.StartsWith("vetale://downloads", StringComparison.OrdinalIgnoreCase))
            {
                return "Завантаження";
            }
            else if (ChromiumInternalHandler.IsChromiumInternalUrl(url))
            {
                return ChromiumInternalHandler.GetPageTitle(url);
            }
            
            return "Vetale Browser";
        }

        public void ToggleMute()
        {
            IsMuted = !IsMuted;
        }

        private void TryHookFullscreenEvents()
        {
            // WebViewControl (CefGlue-based) doesn't expose fullscreen events directly
            // We rely on JavaScript injection and polling instead
            InjectFullscreenListener();
        }

        private async void InjectFullscreenListener()
        {
            try
            {
                // JavaScript to listen for fullscreen changes (debug logging)
                var script = @"
                    (function() {
                        if (window.__fullscreenListenerInjected) return;
                        window.__fullscreenListenerInjected = true;
                        
                        document.addEventListener('fullscreenchange', function() {
                            var isFullscreen = !!document.fullscreenElement;
                            console.log('Fullscreen changed:', isFullscreen);
                        });
                        
                        document.addEventListener('webkitfullscreenchange', function() {
                            var isFullscreen = !!document.webkitFullscreenElement;
                            console.log('Webkit fullscreen changed:', isFullscreen);
                        });
                    })();
                ";

                // Use direct EvaluateScript from WebViewControl with explicit type
                await WebView.EvaluateScriptAsync<object>(script);
            }
            catch
            {
            }
        }

        private async void InjectNavigationGuards()
        {
            try
            {
                var js = @"
                    (function(){
                        if(window.__vetale_no_external_v2__) return; 
                        window.__vetale_no_external_v2__ = true;
                        
                        // List of blocked protocols
                        var blockedProtocols = ['intent:', 'android-app:', 'market:', 'tel:', 'mailto:', 'sms:', 'whatsapp:', 'tg:', 'viber:', 'skype:', 'zoom:', 'ms-', 'vnd.'];
                        
                        function isBlockedUrl(url){
                            if(!url) return false;
                            var u = url.toString().toLowerCase().trim();
                            // Block all non-http/https protocols except file, javascript, data, blob
                            if(u.startsWith('http:') || u.startsWith('https:') || u.startsWith('file:') || 
                               u.startsWith('javascript:') || u.startsWith('data:') || u.startsWith('blob:') ||
                               u.startsWith('about:') || u.startsWith('vetale:')) {
                                return false;
                            }
                            // Block everything else
                            for(var i=0; i<blockedProtocols.length; i++){
                                if(u.indexOf(blockedProtocols[i]) !== -1) return true;
                            }
                            // If there is a ':' and it is not http/https - block
                            var colonIdx = u.indexOf(':');
                            if(colonIdx > 0 && colonIdx < 20) return true;
                            return false;
                        }
                        
                        // CRITICAL: Intercept location.href and location.assign
                        try {
                            var origLocationDescriptor = Object.getOwnPropertyDescriptor(window, 'location');
                            var origLocation = window.location;
                            
                            // Intercept window.location.href = ...
                            if(origLocation && origLocation.href !== undefined) {
                                var origHrefSetter = Object.getOwnPropertyDescriptor(Object.getPrototypeOf(origLocation), 'href');
                                if(origHrefSetter && origHrefSetter.set) {
                                    Object.defineProperty(origLocation, 'href', {
                                        get: function(){ return origHrefSetter.get.call(this); },
                                        set: function(v){
                                            if(isBlockedUrl(v)){
                                                console.log('[VetaleBrowser] Blocked external redirect:', v);
                                                return;
                                            }
                                            origHrefSetter.set.call(this, v);
                                        },
                                        configurable: true
                                    });
                                }
                            }
                        } catch(e){ console.log('[VetaleBrowser] location override failed:', e); }
                        
                        // Intercept location.assign and location.replace
                        try {
                            var origAssign = location.assign;
                            var origReplace = location.replace;
                            location.assign = function(url){
                                if(isBlockedUrl(url)){
                                    console.log('[VetaleBrowser] Blocked location.assign:', url);
                                    return;
                                }
                                return origAssign.call(location, url);
                            };
                            location.replace = function(url){
                                if(isBlockedUrl(url)){
                                    console.log('[VetaleBrowser] Blocked location.replace:', url);
                                    return;
                                }
                                return origReplace.call(location, url);
                            };
                        } catch(e){ console.log('[VetaleBrowser] location methods override failed:', e); }
                        
                        // Intercept window.open
                        try {
                            var originalOpen = window.open;
                            window.open = function(url, name, specs){
                                if(isBlockedUrl(url)){
                                    console.log('[VetaleBrowser] Blocked window.open:', url);
                                    return null;
                                }
                                // Open in the same tab instead of a new window
                                try{
                                    if(url){ location.href = url; }
                                }catch(e){}
                                return null;
                            };
                        } catch(e) {}
                        
                        // Intercept all link clicks
                        document.addEventListener('click', function(e){
                            try{
                                var el = e.target;
                                while(el && el.tagName !== 'A'){ el = el.parentElement; }
                                if(!el) return;
                                var href = el.getAttribute('href') || el.href;
                                if(!href) return;
                                
                                // Block external protocols
                                if(isBlockedUrl(href)){
                                    e.preventDefault(); 
                                    e.stopPropagation();
                                    e.stopImmediatePropagation();
                                    console.log('[VetaleBrowser] Blocked link click:', href);
                                    return false;
                                }
                                
                                // Open _blank in the same tab
                                var target = el.getAttribute('target');
                                if(target && target.toLowerCase() === '_blank'){
                                    e.preventDefault();
                                    e.stopPropagation();
                                    try{ location.href = href; }catch(_){ }
                                }
                            }catch(_){ }
                        }, true);
                        
                        // Intercept forms
                        document.addEventListener('submit', function(e){
                            try{
                                var f = e.target; if(!f) return;
                                var action = f.getAttribute('action') || '';
                                if(isBlockedUrl(action)){
                                    e.preventDefault();
                                    console.log('[VetaleBrowser] Blocked form submit:', action);
                                    return;
                                }
                                var t = f.getAttribute('target');
                                if(t && t.toLowerCase() === '_blank'){
                                    e.preventDefault();
                                    try{ f.removeAttribute('target'); f.submit(); }catch(_){ }
                                }
                            }catch(_){ }
                        }, true);
                        
                        // Block navigator.registerProtocolHandler
                        try {
                            if(navigator.registerProtocolHandler){
                                navigator.registerProtocolHandler = function(){ 
                                    console.log('[VetaleBrowser] Blocked registerProtocolHandler');
                                    return; 
                                };
                            }
                        } catch(e){}
                        
                        // ========== DOWNLOAD PROTECTION (SafeDownloadHandler) ==========
                        // Intercept automatic downloads without user confirmation
                        
                        // Track whether the user is actively clicking (to determine user-initiated actions)
                        window._vetaleUserClickActive = false;
                        window._vetaleLastClickTime = 0;
                        
                        document.addEventListener('mousedown', function(e){
                            window._vetaleUserClickActive = true;
                            window._vetaleLastClickTime = Date.now();
                        }, true);
                        
                        document.addEventListener('mouseup', function(e){
                            // Give a short delay after a click
                            setTimeout(function(){
                                window._vetaleUserClickActive = false;
                            }, 500);
                        }, true);
                        
                        // Intercept links with the download attribute
                        document.addEventListener('click', function(e){
                            try {
                                var el = e.target;
                                while(el && el.tagName !== 'A'){ el = el.parentElement; }
                                if(!el) return;
                                
                                var download = el.getAttribute('download');
                                var href = el.getAttribute('href') || el.href || '';
                                
                                // If there is a download attribute or blob URL - potential download
                                if(download !== null || href.startsWith('blob:') || href.startsWith('data:')) {
                                    var isUserClick = window._vetaleUserClickActive || (Date.now() - window._vetaleLastClickTime < 1000);
                                    if(!isUserClick){
                                        e.preventDefault();
                                        e.stopPropagation();
                                        e.stopImmediatePropagation();
                                        console.log('[VetaleBrowser] BLOCKED automatic download (no user interaction):', href);
                                        return false;
                                    }
                                    console.log('[VetaleBrowser] Download allowed (user click):', href);
                                }
                            } catch(_){}
                        }, true);
                        
                        // Intercept blob URL creation and automatic download
                        try {
                            var origCreateObjectURL = URL.createObjectURL;
                            URL.createObjectURL = function(blob){
                                var url = origCreateObjectURL.call(URL, blob);
                                console.log('[VetaleBrowser] Blob URL created:', url.substring(0, 50) + '...');
                                return url;
                            };
                        } catch(e){}
                        
                        // Intercept programmatic clicking on hidden elements (typical auto-download tactic)
                        try {
                            var origClick = HTMLElement.prototype.click;
                            HTMLElement.prototype.click = function(){
                                var el = this;
                                var isLink = el.tagName === 'A';
                                var hasDownload = isLink && el.hasAttribute('download');
                                var isHidden = (el.style.display === 'none' || el.offsetParent === null);
                                var isUserInitiated = window._vetaleUserClickActive || (Date.now() - window._vetaleLastClickTime < 500);
                                
                                if(hasDownload && isHidden && !isUserInitiated){
                                    console.log('[VetaleBrowser] BLOCKED programmatic click on hidden download link');
                                    return;
                                }
                                
                                return origClick.call(this);
                            };
                        } catch(e){}
                        
                        // Intercept dynamic addition of download links
                        try {
                            var origAppendChild = Node.prototype.appendChild;
                            Node.prototype.appendChild = function(child){
                                var result = origAppendChild.call(this, child);
                                
                                // If a hidden element with download is added - log a warning
                                if(child && child.tagName === 'A' && child.hasAttribute && child.hasAttribute('download')){
                                    var isHidden = !child.offsetParent;
                                    if(isHidden){
                                        console.log('[VetaleBrowser] Warning: Hidden download link added to DOM');
                                    }
                                }
                                
                                return result;
                            };
                        } catch(e){}
                        
                        console.log('[VetaleBrowser] Navigation guards v2 + Download protection active');
                    })();
                ";

                await WebView.EvaluateScriptAsync<object>(js);
                Debug.WriteLine("[TabWorker] Navigation guards v2 injected");
                
                // If the tab is muted, inject audio interceptor to capture new AudioContext instances
                if (_isMuted)
                {
                    await InjectAudioInterceptorAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TabWorker] Failed to inject navigation guards: {ex.Message}");
            }
        }

        /// <summary>
        /// Injects JavaScript code that intercepts AudioContext before it is created.
        /// This ensures that games like HexGL are muted from the very beginning.
        /// </summary>
        private async Task InjectAudioInterceptorAsync()
        {
            try
            {
                var js = @"(function(){
                    if (window._vetaleAudioInterceptorInjected) return;
                    window._vetaleAudioInterceptorInjected = true;
                    
                    // Initialize tracking arrays
                    window._vetaleAudioContexts = window._vetaleAudioContexts || [];
                    window._vetaleGainNodes = window._vetaleGainNodes || [];
                    window._vetaleAudioMuted = true;
                    
                    // Hook AudioContext
                    if (window.AudioContext && !window._vetaleOrigAudioContext) {
                        window._vetaleOrigAudioContext = window.AudioContext;
                        window.AudioContext = function() {
                            var ctx = new window._vetaleOrigAudioContext();
                            window._vetaleAudioContexts.push(ctx);
                            
                            // Auto-suspend if muted
                            if (window._vetaleAudioMuted) {
                                try { ctx.suspend(); } catch(e){}
                            }
                            
                            // Hook createGain to track gain nodes
                            var origCreateGain = ctx.createGain.bind(ctx);
                            ctx.createGain = function() {
                                var gain = origCreateGain();
                                window._vetaleGainNodes.push(gain);
                                if (window._vetaleAudioMuted) {
                                    try { gain.gain.value = 0; } catch(e){}
                                }
                                return gain;
                            };
                            
                            return ctx;
                        };
                        window.AudioContext.prototype = window._vetaleOrigAudioContext.prototype;
                    }
                    
                    // Hook webkitAudioContext
                    if (window.webkitAudioContext && !window._vetaleOrigWebkitAudioContext) {
                        window._vetaleOrigWebkitAudioContext = window.webkitAudioContext;
                        window.webkitAudioContext = function() {
                            var ctx = new window._vetaleOrigWebkitAudioContext();
                            window._vetaleAudioContexts.push(ctx);
                            
                            if (window._vetaleAudioMuted) {
                                try { ctx.suspend(); } catch(e){}
                            }
                            
                            var origCreateGain = ctx.createGain.bind(ctx);
                            ctx.createGain = function() {
                                var gain = origCreateGain();
                                window._vetaleGainNodes.push(gain);
                                if (window._vetaleAudioMuted) {
                                    try { gain.gain.value = 0; } catch(e){}
                                }
                                return gain;
                            };
                            
                            return ctx;
                        };
                        window.webkitAudioContext.prototype = window._vetaleOrigWebkitAudioContext.prototype;
                    }
                    
                    console.log('[VetaleBrowser] Audio interceptor injected (muted mode)');
                })();";
                
                await WebView.EvaluateScriptAsync<object>(js);
                Debug.WriteLine("[TabWorker] Audio interceptor injected");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TabWorker] Failed to inject audio interceptor: {ex.Message}");
            }
        }

        /// <summary>
        /// Attempt to get CefBrowserHost via reflection from WebViewControl
        /// </summary>
        private object? TryGetCefBrowserHost()
        {
            try
            {
                var webViewType = WebView.InnerView.GetType();
                object? browser = null;

                // CefSharp.Avalonia keeps the host directly on WebView in
                // some versions. Prefer it before walking through CefBrowser.
                string[] directHostNames = { "BrowserHost", "_browserHost", "Host", "_host" };
                foreach (var name in directHostNames)
                {
                    var hostProperty = webViewType.GetProperty(name,
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic);
                    var directHost = hostProperty?.GetValue(WebView.InnerView);
                    if (directHost != null &&
                        directHost.GetType().GetMethod("SetAudioMuted",
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.Instance) != null)
                    {
                        Debug.WriteLine($"[TabWorker] Found CefBrowserHost directly via '{name}' property");
                        return directHost;
                    }

                    var hostField = webViewType.GetField(name,
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic);
                    directHost = hostField?.GetValue(WebView.InnerView);
                    if (directHost != null &&
                        directHost.GetType().GetMethod("SetAudioMuted",
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.Instance) != null)
                    {
                        Debug.WriteLine($"[TabWorker] Found CefBrowserHost directly via '{name}' field");
                        return directHost;
                    }
                }
                
                // Search for browser using various property/field names
                string[] browserNames = { "Browser", "_browser", "browser", "chromiumBrowser", "_chromiumBrowser", 
                                          "InternalBrowser", "_internalBrowser", "CefBrowser", "_cefBrowser" };
                
                foreach (var name in browserNames)
                {
                    var browserProp = webViewType.GetProperty(name, 
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (browserProp != null)
                    {
                        browser = browserProp.GetValue(WebView.InnerView);
                        if (browser != null)
                        {
                            Debug.WriteLine($"[TabWorker] Found browser via property '{name}'");
                            break;
                        }
                    }
                    
                    var browserField = webViewType.GetField(name, 
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (browserField != null)
                    {
                        browser = browserField.GetValue(WebView.InnerView);
                        if (browser != null)
                        {
                            Debug.WriteLine($"[TabWorker] Found browser via field '{name}'");
                            break;
                        }
                    }
                }
                
                // If not found by name, search by type
                if (browser == null)
                {
                    foreach (var field in webViewType.GetFields(System.Reflection.BindingFlags.Instance | 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                    {
                        try
                        {
                            var val = field.GetValue(WebView.InnerView);
                            if (val != null)
                            {
                                var typeName = val.GetType().FullName ?? "";
                                if (typeName.Contains("CefBrowser") || typeName.Contains("Chromium") || typeName.Contains("Browser"))
                                {
                                    browser = val;
                                    Debug.WriteLine($"[TabWorker] Found browser via field type pattern '{field.Name}', type: {typeName}");
                                    break;
                                }
                            }
                        }
                        catch { }
                    }
                }

                if (browser == null)
                {
                    Debug.WriteLine("[TabWorker] Browser object not found in WebView");
                    return null;
                }

                // Get Host from browser
                var browserType = browser.GetType();
                object? host = null;
                
                // Search for Host using various names
                string[] hostNames = { "Host", "BrowserHost", "_host", "_browserHost" };
                
                foreach (var name in hostNames)
                {
                    var hostProp = browserType.GetProperty(name, 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (hostProp != null)
                    {
                        host = hostProp.GetValue(browser);
                        if (host != null)
                        {
                            Debug.WriteLine($"[TabWorker] Found host via property '{name}'");
                            break;
                        }
                    }
                }
                
                // Try the GetHost method
                if (host == null)
                {
                    var hostMethod = browserType.GetMethod("GetHost", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (hostMethod != null)
                    {
                        host = hostMethod.Invoke(browser, null);
                        if (host != null)
                        {
                            Debug.WriteLine("[TabWorker] Found host via GetHost() method");
                        }
                    }
                }

                return host;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TabWorker] TryGetCefBrowserHost failed: {ex.Message}");
                return null;
            }
        }


        private async void ApplyMuteState()
        {
            await _muteApplyLock.WaitAsync();
            try
            {
                Debug.WriteLine($"[TabWorker] ApplyMuteState called, _isMuted={_isMuted}, _relatedPids.Count={_relatedPids.Count}");
                
                bool applied = false;

                // Use the browser-level CEF API first. This mutes the whole
                // WebView and does not depend on page lifecycle events.
                applied = WebView.SetAudioMuted(_isMuted);
                if (applied)
                {
                    Debug.WriteLine($"[TabWorker] Audio {(_isMuted ? "muted" : "unmuted")} via WebView browser API");
                }
                
                // Priority 1: CEF native API via reflection (most reliable method)
                if (!applied) try
                {
                    var cefBrowserHost = TryGetCefBrowserHost();
                    if (cefBrowserHost != null)
                    {
                        var setAudioMutedMethod = cefBrowserHost.GetType().GetMethod("SetAudioMuted", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        
                        if (setAudioMutedMethod != null)
                        {
                            setAudioMutedMethod.Invoke(cefBrowserHost, new object[] { _isMuted });
                            applied = true;
                            Debug.WriteLine($"[TabWorker] Audio {(_isMuted ? "muted" : "unmuted")} via CEF native API");
                        }
                        else
                        {
                            Debug.WriteLine("[TabWorker] SetAudioMuted method not found on CefBrowserHost");
                        }
                    }
                    else
                    {
                        Debug.WriteLine("[TabWorker] CefBrowserHost is null");
                    }
                }
                catch (Exception cefEx)
                {
                    Debug.WriteLine($"[TabWorker] CEF native mute failed: {cefEx.Message}");
                }

                // Priority 2: Windows Audio Session API
                if (!applied && RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _relatedPids.Count > 0)
                {
                    var pidsCopy = _relatedPids.ToList();
                    foreach (var pid in pidsCopy)
                    {
                        try
                        {
                            if (WindowsAudioSessionService.TrySetProcessMute(pid, _isMuted))
                            {
                                applied = true;
                            }
                        }
                        catch { }
                    }
                    
                    if (applied)
                    {
                        Debug.WriteLine($"[TabWorker] Audio {(_isMuted ? "muted" : "unmuted")} at system level for PIDs: {string.Join(",", pidsCopy)}");
                    }
                }

                // Priority 3: JavaScript to control all media elements + Web Audio API
                // Always execute JavaScript as additional protection
                try
                {
                    var js = _isMuted
                        ? @"(function(){
                            try {
                                // Mute all video and audio elements
                                document.querySelectorAll('video,audio').forEach(function(m) {
                                    m.muted = true;
                                    m.volume = 0;
                                });
                                
                                // Initialize tracking array if not exists
                                if (!window._vetaleAudioContexts) {
                                    window._vetaleAudioContexts = [];
                                }
                                
                                // Suspend all tracked audio contexts
                                window._vetaleAudioContexts.forEach(function(ctx) { 
                                    try { 
                                        if (ctx && ctx.state !== 'closed') {
                                            ctx.suspend(); 
                                        }
                                    } catch(e){} 
                                });
                                
                                // Hook AudioContext to track new instances and auto-suspend them
                                if (window.AudioContext && !window._vetaleOrigAudioContext) {
                                    window._vetaleOrigAudioContext = window.AudioContext;
                                    window.AudioContext = function() {
                                        var ctx = new window._vetaleOrigAudioContext();
                                        window._vetaleAudioContexts.push(ctx);
                                        if (window._vetaleAudioMuted) {
                                            ctx.suspend();
                                        }
                                        return ctx;
                                    };
                                    window.AudioContext.prototype = window._vetaleOrigAudioContext.prototype;
                                }
                                if (window.webkitAudioContext && !window._vetaleOrigWebkitAudioContext) {
                                    window._vetaleOrigWebkitAudioContext = window.webkitAudioContext;
                                    window.webkitAudioContext = function() {
                                        var ctx = new window._vetaleOrigWebkitAudioContext();
                                        window._vetaleAudioContexts.push(ctx);
                                        if (window._vetaleAudioMuted) {
                                            ctx.suspend();
                                        }
                                        return ctx;
                                    };
                                    window.webkitAudioContext.prototype = window._vetaleOrigWebkitAudioContext.prototype;
                                }
                                
                                // Set muted flag
                                window._vetaleAudioMuted = true;
                                
                                // Also try to find and mute gain nodes (common pattern in games)
                                if (window._vetaleGainNodes) {
                                    window._vetaleGainNodes.forEach(function(g) { 
                                        try { g.gain.value = 0; } catch(e){} 
                                    });
                                }
                                
                            } catch(e) { console.log('[VetaleBrowser] Mute error:', e); }
                        })();"
                        : @"(function(){
                            try {
                                // Unmute all video and audio elements
                                document.querySelectorAll('video,audio').forEach(function(m) {
                                    m.muted = false;
                                    if (m.volume === 0) m.volume = 1.0;
                                });
                                
                                // Resume all tracked audio contexts
                                if (window._vetaleAudioContexts) {
                                    window._vetaleAudioContexts.forEach(function(ctx) { 
                                        try { 
                                            if (ctx && ctx.state !== 'closed') {
                                                ctx.resume(); 
                                            }
                                        } catch(e){} 
                                    });
                                }
                                
                                // Clear muted flag
                                window._vetaleAudioMuted = false;
                                
                                // Restore gain nodes
                                if (window._vetaleGainNodes) {
                                    window._vetaleGainNodes.forEach(function(g) { 
                                        try { g.gain.value = 1; } catch(e){} 
                                    });
                                }
                                
                            } catch(e) { console.log('[VetaleBrowser] Unmute error:', e); }
                        })();";
                    
                    await WebView.EvaluateScriptAsync<object>(js);
                    Debug.WriteLine($"[TabWorker] Audio state applied via JavaScript: {_isMuted}");
                }
                catch (Exception jsEx)
                {
                    Debug.WriteLine($"[TabWorker] JS mute failed: {jsEx.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TabWorker] Failed to apply mute state: {ex.Message}");
            }
            finally
            {
                _muteApplyLock.Release();
            }
        }
        
        /// <summary>
        /// Schedules a delayed re-application of mute after navigation.
        /// Called multiple times at an interval to ensure mute works for dynamic content.
        /// </summary>
        private void ScheduleMuteReapply()
        {
            _muteReapplyCount = 4; // Apply mute 4 times at 500ms intervals
            _muteReapplyTimer.Stop();
            _muteReapplyTimer.Tick -= MuteReapplyTick;
            _muteReapplyTimer.Tick += MuteReapplyTick;
            _muteReapplyTimer.Start();
        }
        
        private void MuteReapplyTick(object? sender, EventArgs e)
        {
            if (_muteReapplyCount <= 0)
            {
                _muteReapplyTimer.Stop();
                _muteReapplyTimer.Tick -= MuteReapplyTick;
                return;
            }
            
            _muteReapplyCount--;
            
            if (_isMuted)
            {
                // Update PIDs for new processes that may have appeared
                RefreshRelatedProcessesBestEffort();
                ApplyMuteState();
            }
            else
            {
                // Mute was disabled - stop the timer
                _muteReapplyTimer.Stop();
                _muteReapplyTimer.Tick -= MuteReapplyTick;
            }
        }

        private void TryUpdateSubprocessTitle()
        {
            try
            {
                // Direct access to Title property
                string? siteTitle = WebView.Title;
                
                if (string.IsNullOrWhiteSpace(siteTitle))
                {
                    // Fallback to host from Address
                    var addr = WebView.Address;
                    if (!string.IsNullOrWhiteSpace(addr) && Uri.TryCreate(addr, UriKind.Absolute, out var uri))
                    {
                        siteTitle = uri.Host;
                        if (siteTitle.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                            siteTitle = siteTitle.Substring(4);
                    }
                }

                if (string.IsNullOrWhiteSpace(siteTitle))
                {
                    siteTitle = "New Tab";
                }

                _subprocess.UpdateTitle($"Tab: {siteTitle}");
            }
            catch { }
        }

        private async Task TryInterceptDownloadAsync(string? url, string? previous)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url)) return;
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
                if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return;

                if (!ShouldTreatAsDownloadByExtension(uri))
                {
                    try
                    {
                        using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Head, uri);
                        using var resp = await _httpHead.SendAsync(req);
                        if (!resp.IsSuccessStatusCode) return;
                        var cd = resp.Content.Headers.ContentDisposition;
                        var isAttachment = cd != null && string.Equals(cd.DispositionType, "attachment", StringComparison.OrdinalIgnoreCase);
                        if (!isAttachment) return;
                    }
                    catch { return; }
                }

                var suggested = System.IO.Path.GetFileName(uri.LocalPath);
                _ = VetaleBrowser.Core.Scripts.GlobalManagers.DownloadManager.StartDownloadAsync(url, suggested);

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        _blockingDownloadNav = true;
                        if (CanGoBack)
                        {
                            GoBack();
                        }
                        else if (!string.IsNullOrWhiteSpace(previous))
                        {
                            _historyNavigationInProgress = true;
                            _historyNavigationTarget = previous;
                            WebView.Address = previous!;
                        }
                    }
                    finally
                    {
                        _blockingDownloadNav = false;
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker] TryInterceptDownloadAsync error: {ex.Message}");
            }
        }

        private static bool ShouldTreatAsDownloadByExtension(Uri uri)
        {
            try
            {
                var path = uri.LocalPath.ToLowerInvariant();
                string[] exts = new[]
                {
                    ".zip", ".7z", ".rar", ".tar", ".gz", ".bz2",
                    ".exe", ".msi", ".iso",
                    ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
                    ".mp3", ".mp4", ".mkv", ".avi", ".mov",
                    ".png", ".jpg", ".jpeg", ".gif", ".webp",
                    ".apk"
                };
                foreach (var ext in exts)
                {
                    if (path.EndsWith(ext)) return true;
                }
                return false;
            }
            catch { return false; }
        }

        public void Dispose()
        {
            try
            {
                WebView.PropertyChanged -= WebViewOnPropertyChanged;
                
                // Dispose error handler
                if (ErrorHandler != null)
                {
                    ErrorHandler.ErrorOccurred -= OnErrorHandlerError;
                    ErrorHandler.Dispose();
                }
                
                Manager.Dispose();
                WebView.Dispose();
                _fullscreenPollTimer.Stop();
                _pidRefreshTimer.Stop();
                _muteReapplyTimer.Stop();
                
                // Release claimed PIDs so other tabs or future workers can reuse if processes persist
                TabProcessTracker.ReleasePids(_relatedPids);

                // Stop subprocess
                _subprocess.Dispose();
            }
            catch
            {
                // best-effort
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
