using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WebViewControl;
using Avalonia.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using VetaleBrowser.VetaleBrowser.Core.Services.Windows;
using VetaleBrowser.VetaleBrowser.Core.Services;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.Models
{
    /// <summary>
    /// Represents a browser worker that owns its own WebView (CefGlue-based via WebViewControl) and navigation manager.
    /// </summary>
    public sealed class TabWorker : IDisposable, INotifyPropertyChanged
    {
        public Guid Id { get; } = Guid.NewGuid();

        public WebView WebView { get; }
        public GlobalManagers.WebViewManager Manager { get; }

        private string? _title;
        private string? _address;
        private bool _isActive;
        private bool _isMuted;
        
        // Track OS process IDs associated with this tab's rendering/audible activity (best-effort, OS-level, non-CEF-specific)
        private readonly HashSet<int> _relatedPids = new();
        private readonly DispatcherTimer _pidRefreshTimer = new() { Interval = TimeSpan.FromSeconds(2) };

        // Fullscreen poller to catch content-initiated fullscreen when events aren't exposed
        private readonly DispatcherTimer _fullscreenPollTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
        private bool _lastFullscreenState;

        // New: subprocess launched per tab (system-level process)
        private readonly TabSubprocessService _subprocess;

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
                } 
            }
        }

        public event EventHandler<string?>? TitleChanged;
        public event EventHandler<string?>? AddressChanged;
        public event EventHandler<bool>? FullscreenChanged;

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
                WebView = new WebView
                {
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                };
                
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

                // Forward manager-initiated navigations to Address property
                Manager.Navigated += (_, url) => Address = url;

                // Observe WebView property changes to keep state up-to-date
                WebView.PropertyChanged += WebViewOnPropertyChanged;

                // Initialize subprocess title
                TryUpdateSubprocessTitle();
                
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Setting up fullscreen events...");
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
            var name = e.Property.Name;
            if (string.IsNullOrEmpty(name)) return;

            if (name == "Address")
            {
                Address = WebView.Address;
                
                // Re-inject guards and fullscreen listener on new pages
                InjectNavigationGuards();
                InjectFullscreenListener();

                // Kick a short PID refresh burst after navigation
                SchedulePidRefreshBurst();

                // Update subprocess title on navigation
                TryUpdateSubprocessTitle();

                // Re-apply mute state on navigation to ensure it persists across page loads
                if (IsMuted)
                {
                    ApplyMuteState();
                }
            }
            else if (name == "Title")
            {
                // Direct access to Title property
                var t = WebView.Title;
                if (!string.IsNullOrWhiteSpace(t))
                {
                    // Prefix with Tab: requested
                    Title = $"Tab: {t}";
                    TryUpdateSubprocessTitle();
                }
            }
            else if (name == "CanGoBack" || name == "CanGoForward")
            {
                // No-op here; consumers can read from WebView
            }
            // Note: WebViewControl may not expose fullscreen as a property - polling handles this
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
                bool isFs = await EvaluateScriptAsBoolAsync("!!(document.fullscreenElement||document.webkitFullscreenElement||document.msFullscreenElement)");
                if (isFs != _lastFullscreenState)
                {
                    _lastFullscreenState = isFs;
                    System.Diagnostics.Debug.WriteLine($"[TabWorker] Fullscreen polled: {isFs}");
                    FullscreenChanged?.Invoke(this, isFs);
                }
            }
            catch (Exception ex)
            {
                // swallow polling errors, keep timer running
                System.Diagnostics.Debug.WriteLine($"[TabWorker] Fullscreen poll failed: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task<bool> EvaluateScriptAsBoolAsync(string script)
        {
            try
            {
                // Use direct EvaluateScript from WebViewControl (returns Task<object>)
                var result = await WebView.EvaluateScript<object>(script);
                
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

        public async void Navigate(string url)
        {
            try
            {
                await Manager.NavigateAsync(url);
            }
            catch
            {
                // ignore navigation errors here
            }
        }

        public void ToggleMute()
        {
            IsMuted = !IsMuted;
        }

        private void TryHookFullscreenEvents()
        {
            // WebViewControl (CefGlue-based) doesn't expose fullscreen events directly
            // We rely on JavaScript injection and polling instead
            System.Diagnostics.Debug.WriteLine("[TabWorker] Using JavaScript polling for fullscreen detection");
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
                await WebView.EvaluateScript<object>(script);
                System.Diagnostics.Debug.WriteLine("[TabWorker] Fullscreen listener injected via JavaScript");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker] Failed to inject fullscreen listener: {ex.Message}");
            }
        }

        private async void InjectNavigationGuards()
        {
            try
            {
                var js = @"
                    (function(){
                        if(window.__vetale_no_external__) return; 
                        window.__vetale_no_external__ = true;
                        // Override window.open to open in the same tab
                        try {
                            var originalOpen = window.open;
                            window.open = function(url, name, specs){
                                try{
                                    if(url){ location.href = url; }
                                }catch(e){}
                                return null;
                            };
                        } catch(e) {}
                        
                        function isExternalScheme(u){
                            try{
                                var a = document.createElement('a');
                                a.href = u;
                                var p = a.protocol ? a.protocol.toLowerCase() : '';
                                if(!p) return false;
                                if(p === 'http:' || p === 'https:') return false;
                                return true; // everything else is treated as external
                            }catch(e){ return false; }
                        }
                        
                        // Intercept anchor clicks with target=_blank or external schemes
                        document.addEventListener('click', function(e){
                            try{
                                var el = e.target;
                                while(el && el.tagName !== 'A'){ el = el.parentElement; }
                                if(!el) return;
                                var href = el.getAttribute('href');
                                if(!href) return;
                                var target = el.getAttribute('target');
                                if((target && target.toLowerCase() === '_blank') || isExternalScheme(href)){
                                    e.preventDefault(); e.stopPropagation();
                                    if(!isExternalScheme(href)){
                                        try{ location.href = href; }catch(_){ }
                                    } // else: block external app invocation silently
                                }
                            }catch(_){ }
                        }, true);
                        
                        // Prevent form target=_blank popups
                        document.addEventListener('submit', function(e){
                            try{
                                var f = e.target; if(!f) return;
                                var t = f.getAttribute('target');
                                if(t && t.toLowerCase() === '_blank'){
                                    e.preventDefault();
                                    try{ f.removeAttribute('target'); f.submit(); }catch(_){ }
                                }
                            }catch(_){ }
                        }, true);
                    })();
                ";

                await WebView.EvaluateScript<object>(js);
                Debug.WriteLine("[TabWorker] Navigation guards injected");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TabWorker] Failed to inject navigation guards: {ex.Message}");
            }
        }


        private async void ApplyMuteState()
        {
            try
            {
                bool appliedAtSystemLevel = false;

                // Try to mute per related OS process using Windows audio sessions (system level)
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _relatedPids.Count > 0)
                {
                    foreach (var pid in _relatedPids)
                    {
                        try
                        {
                            if (WindowsAudioSessionService.TrySetProcessMute(pid, _isMuted))
                            {
                                appliedAtSystemLevel = true;
                            }
                        }
                        catch { }
                    }
                }

                if (appliedAtSystemLevel)
                {
                    Debug.WriteLine($"[TabWorker] Audio {( _isMuted ? "muted" : "unmuted" )} at system level for PIDs: {string.Join(",", _relatedPids)}");
                    return;
                }

                // WebViewControl (CefGlue) doesn't expose direct AudioMuted properties
                // Use JavaScript fallback to control media elements
                try
                {
                    var js = _isMuted
                        ? "(function(){try{document.querySelectorAll('video,audio').forEach(m=>{m.muted=true; m.volume=0;});}catch(e){}})();"
                        : "(function(){try{document.querySelectorAll('video,audio').forEach(m=>{m.muted=false; if(m.volume===0) m.volume=1.0;});}catch(e){}})();";
                    
                    // Use direct EvaluateScript from WebViewControl (async) with explicit type
                    await WebView.EvaluateScript<object>(js);
                    System.Diagnostics.Debug.WriteLine($"[TabWorker] Audio state applied via JavaScript: {_isMuted}");
                }
                catch (Exception jsEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[TabWorker] JS mute failed: {jsEx.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker] Failed to apply mute state: {ex.Message}");
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

        public void Dispose()
        {
            try
            {
                WebView.PropertyChanged -= WebViewOnPropertyChanged;
                Manager.Dispose();
                WebView.Dispose();
                _fullscreenPollTimer.Stop();
                _pidRefreshTimer.Stop();
                
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
