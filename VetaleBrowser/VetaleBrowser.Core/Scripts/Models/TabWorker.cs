using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WebViewControl;
using Avalonia.Threading;

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

        // Fullscreen poller to catch content-initiated fullscreen when events aren't exposed
        private readonly DispatcherTimer _fullscreenPollTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
        private bool _lastFullscreenState;

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
            WebView = new WebView
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            };

            Manager = new GlobalManagers.WebViewManager();
            Manager.Initialize(WebView);

            // Forward manager-initiated navigations to Address property
            Manager.Navigated += (_, url) => Address = url;

            // Observe WebView property changes to keep state up-to-date
            WebView.PropertyChanged += WebViewOnPropertyChanged;

            // Try to hook fullscreen events via reflection
            TryHookFullscreenEvents();

            // Start polling for fullscreen changes as a robust fallback
            _fullscreenPollTimer.Tick += (_, __) => PollFullscreenAsync();
            _fullscreenPollTimer.Start();
        }

        private void WebViewOnPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
        {
            var name = e.Property.Name;
            if (string.IsNullOrEmpty(name)) return;

            if (name == "Address")
            {
                Address = WebView.Address;
                
                // Re-inject fullscreen listener on new pages
                InjectFullscreenListener();

                // Re-apply mute state on navigation to ensure it persists across page loads
                if (IsMuted)
                {
                    ApplyMuteState();
                }
            }
            else if (name == "Title")
            {
                try
                {
                    var t = WebView.GetType().GetProperty("Title")?.GetValue(WebView) as string;
                    if (!string.IsNullOrWhiteSpace(t)) Title = t;
                }
                catch
                {
                    // ignore
                }
            }
            else if (name == "CanGoBack" || name == "CanGoForward")
            {
                // No-op here; consumers can read from WebView
            }
            // Detect fullscreen changes exposed as properties on WebView (varies by platform/version)
            else if (name.Equals("IsFullscreen", StringComparison.OrdinalIgnoreCase)
                     || name.Equals("IsFullScreen", StringComparison.OrdinalIgnoreCase)
                     || name.Equals("Fullscreen", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    bool isFs = false;
                    var t = WebView.GetType();
                    var p = t.GetProperty("IsFullscreen") ?? t.GetProperty("IsFullScreen") ?? t.GetProperty("Fullscreen");
                    if (p != null && p.PropertyType == typeof(bool))
                    {
                        isFs = (bool)(p.GetValue(WebView) ?? false);
                    }
                    System.Diagnostics.Debug.WriteLine($"[TabWorker] Fullscreen property changed: {isFs}");
                    FullscreenChanged?.Invoke(this, isFs);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TabWorker] Failed to read fullscreen property: {ex.Message}");
                }
            }
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
            // Try EvaluateScript or ExecuteScript, handling different return types
            var t = WebView.GetType();
            var method = t.GetMethod("EvaluateScript") ?? t.GetMethod("ExecuteScript");
            if (method == null)
            {
                return false;
            }

            object? callResult;
            try
            {
                callResult = method.Invoke(WebView, new object[] { script });
            }
            catch
            {
                return false;
            }

            // If it's a Task, await it and get its Result
            if (callResult is System.Threading.Tasks.Task task)
            {
                await task.ConfigureAwait(false);
                var resultProp = task.GetType().GetProperty("Result");
                callResult = resultProp?.GetValue(task);
            }

            if (callResult is bool b) return b;
            if (callResult is string s)
            {
                if (bool.TryParse(s.Trim(), out var parsed)) return parsed;
                if (string.Equals(s.Trim(), "1", StringComparison.Ordinal)) return true;
                if (string.Equals(s.Trim(), "0", StringComparison.Ordinal)) return false;
            }

            var resProp = callResult?.GetType().GetProperty("Result");
            if (resProp != null)
            {
                var inner = resProp.GetValue(callResult);
                if (inner is bool ib) return ib;
                if (inner is string istring && bool.TryParse(istring, out var ibool)) return ibool;
            }

            return false;
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
            try
            {
                // Try to find and subscribe to fullscreen events
                var eventInfo = WebView.GetType().GetEvent("IsFullscreenChanged");
                if (eventInfo != null)
                {
                    var handler = new EventHandler<bool>((s, isFullscreen) =>
                    {
                        System.Diagnostics.Debug.WriteLine($"[TabWorker] Fullscreen changed: {isFullscreen}");
                        FullscreenChanged?.Invoke(this, isFullscreen);
                    });
                    eventInfo.AddEventHandler(WebView, handler);
                    System.Diagnostics.Debug.WriteLine("[TabWorker] Hooked to IsFullscreenChanged event");
                    return;
                }

                // Try alternative event names
                var altEvent = WebView.GetType().GetEvent("FullscreenChanged") 
                    ?? WebView.GetType().GetEvent("FullScreenChanged");
                if (altEvent != null)
                {
                    var handler = new EventHandler<bool>((s, isFullscreen) =>
                    {
                        System.Diagnostics.Debug.WriteLine($"[TabWorker] Fullscreen changed (alt): {isFullscreen}");
                        FullscreenChanged?.Invoke(this, isFullscreen);
                    });
                    altEvent.AddEventHandler(WebView, handler);
                    System.Diagnostics.Debug.WriteLine("[TabWorker] Hooked to alternative fullscreen event");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("[TabWorker] No fullscreen event found - will inject JavaScript listener");
                
                // Inject JavaScript listener for fullscreen changes as a fallback (logs only)
                InjectFullscreenListener();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker] Failed to hook fullscreen events: {ex.Message}");
            }
        }

        private void InjectFullscreenListener()
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

                var execMethod = WebView.GetType().GetMethod("ExecuteScript") 
                    ?? WebView.GetType().GetMethod("EvaluateScript");
                
                if (execMethod != null)
                {
                    execMethod.Invoke(WebView, new object[] { script });
                    System.Diagnostics.Debug.WriteLine("[TabWorker] Fullscreen listener injected via JavaScript");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker] Failed to inject fullscreen listener: {ex.Message}");
            }
        }

        private void ApplyMuteState()
        {
            try
            {
                // First, try to mute at the browser host level (affects entire process/browser instance)
                var t = WebView.GetType();
                var browserHostProp = t.GetProperty("BrowserHost") 
                    ?? t.GetProperty("Host")
                    ?? t.GetProperty("BrowserHost", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (browserHostProp != null)
                {
                    var browserHost = browserHostProp.GetValue(WebView);
                    if (TrySetAudioMutedOnHost(browserHost, _isMuted)) return;
                }

                // Try reflection for IsAudioMuted property on WebView itself
                var prop = t.GetProperty("IsAudioMuted") 
                           ?? t.GetProperty("AudioMuted")
                           ?? t.GetProperty("IsAudioMuted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(WebView, _isMuted);
                    System.Diagnostics.Debug.WriteLine($"[TabWorker] Audio muted via WebView property: {_isMuted}");
                    return;
                }

                // Access underlying CEF browser and mute via Host
                var cefBrowser = t.GetProperty("Browser")?.GetValue(WebView)
                              ?? t.GetProperty("CefBrowser")?.GetValue(WebView)
                              ?? t.GetMethod("GetBrowser")?.Invoke(WebView, Array.Empty<object>())
                              ?? t.GetField("Browser", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(WebView);
                if (cefBrowser != null)
                {
                    // Host as property or GetHost method
                    var host = cefBrowser.GetType().GetProperty("Host")?.GetValue(cefBrowser)
                              ?? cefBrowser.GetType().GetMethod("GetHost")?.Invoke(cefBrowser, Array.Empty<object>());
                    if (TrySetAudioMutedOnHost(host, _isMuted)) return;
                }

                // JavaScript fallback: iterate over media elements
                try
                {
                    var js = _isMuted
                        ? "(function(){try{document.querySelectorAll('video,audio').forEach(m=>{m.muted=true; m.volume=0;});}catch(e){}})();"
                        : "(function(){try{document.querySelectorAll('video,audio').forEach(m=>{m.muted=false; if(m.volume===0) m.volume=1.0;});}catch(e){}})();";
                    var execMethod = t.GetMethod("ExecuteScript") 
                        ?? t.GetMethod("EvaluateScript")
                        ?? t.GetMethod("EvaluateScriptAsync");
                    if (execMethod != null)
                    {
                        _ = execMethod.Invoke(WebView, new object[] { js });
                        System.Diagnostics.Debug.WriteLine($"[TabWorker] Audio state applied via JavaScript fallback: {_isMuted}");
                        return;
                    }
                }
                catch (Exception jsEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[TabWorker] JS fallback for mute failed: {jsEx.Message}");
                }

                System.Diagnostics.Debug.WriteLine("[TabWorker] No host-level mute path found; JS fallback unavailable");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker] Failed to apply mute state: {ex.Message}");
            }
        }

        private static bool TrySetAudioMutedOnHost(object? host, bool muted)
        {
            if (host == null) return false;
            try
            {
                var ht = host.GetType();
                // Prefer SetAudioMuted method
                var setAudioMuted = ht.GetMethod("SetAudioMuted") 
                                   ?? ht.GetMethod("set_AudioMuted")
                                   ?? ht.GetMethod("SetAudioMuted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (setAudioMuted != null)
                {
                    setAudioMuted.Invoke(host, new object[] { muted });
                    System.Diagnostics.Debug.WriteLine($"[TabWorker] Audio muted via Host.SetAudioMuted: {muted}");
                    return true;
                }

                // Try property assignment
                var audioMutedProp = ht.GetProperty("AudioMuted") 
                                   ?? ht.GetProperty("IsAudioMuted");
                if (audioMutedProp != null && audioMutedProp.CanWrite)
                {
                    audioMutedProp.SetValue(host, muted);
                    System.Diagnostics.Debug.WriteLine($"[TabWorker] Audio muted via Host.AudioMuted property: {muted}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker] Host mute attempt failed: {ex.Message}");
            }
            return false;
        }

        public void Dispose()
        {
            try
            {
                WebView.PropertyChanged -= WebViewOnPropertyChanged;
                Manager.Dispose();
                WebView.Dispose();
                _fullscreenPollTimer.Stop();
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
