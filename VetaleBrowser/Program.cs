using Avalonia;
using System;
using WebViewControl;
using System.Diagnostics;

namespace VetaleBrowser;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // MEMORY OPTIMIZATION: Set aggressive GC mode for better memory management
        System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
        
        // Skip tab-helper mode since we no longer spawn subprocesses (memory optimization)
        if (args is { Length: > 0 } && Array.Exists(args, a => a == "--tab-helper"))
        {
            // Immediately exit - subprocess mode is disabled for memory savings
            return;
        }

        // Configure CEF/WebView before any UI is created
        ConfigureWebEngines();

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args ?? Array.Empty<string>());
    }


    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    /// <summary>
    /// Set process-wide environment variables to enable GPU acceleration and useful features
    /// for Chromium-based engines (WebView2/CEF). Unknown flags are ignored by the engine.
    /// Must run before any WebView instance is created.
    /// </summary>
    private static void ConfigureWebEngines()
    {
        try
        {
            ConfigureCefSwitches();
        }
        catch
        {
            // Best-effort; ignore if anything goes wrong here.
        }
    }

    private static void ConfigureCefSwitches()
    {
        try
        {
            // Choose persistent data locations
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var userDataDir = System.IO.Path.Combine(appData, "VetaleBrowser", "cef_data");
            var diskCacheDir = System.IO.Path.Combine(appData, "VetaleBrowser", "cef_cache");
            try { System.IO.Directory.CreateDirectory(userDataDir); } catch { }
            try { System.IO.Directory.CreateDirectory(diskCacheDir); } catch { }

            // Define Chromium/CEF switches for PROFESSIONAL BROWSER performance
            // Modern Chrome-like User-Agent for full browser recognition
            var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 VetaleBrowser/1.0";
            
            // MEMORY OPTIMIZATION: Calculate cache sizes based on available RAM
            var availableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            var cacheSizeMB = Math.Min(512, Math.Max(64, (int)(availableMemory / (1024 * 1024 * 8)))); // 1/8 of available RAM, 64-512MB
            
            var switches = new (string key, string? value)[]
            {
                // === GPU ACCELERATION (Hardware rendering for WebGL games) ===
                // D3D11 ANGLE для WebGL - найкраща сумісність з іграми
                ("use-angle", "d3d11"),  // D3D11 для WebGL (потрібно для HexGL)
                
                // AMD memory leak mitigations
                ("disable-gpu-memory-buffer-compositor-resources", null), // Зменшує витоки на AMD
                ("disable-gpu-memory-buffer-video-frames", null), // Фікс витоків відео на AMD
                ("disable-zero-copy", null), // Вимкнути zero-copy для стабільності на AMD
                
                // GPU features
                ("enable-gpu-rasterization", null),
                ("enable-accelerated-video-decode", null),
                ("enable-accelerated-2d-canvas", null),
                ("enable-oop-rasterization", null), // Out-of-process rasterization
                
                // === AGGRESSIVE MEMORY OPTIMIZATION (500MB MAX per tab) ===
                // V8 heap - збільшено для підтримки WebGL ігор
                ("js-flags", "--max-old-space-size=128 --optimize-for-size --gc-interval=100"),
                
                // CRITICAL: Force single renderer process to limit RAM
                ("renderer-process-limit", "1"),
                
                // Aggressive memory limits
                ("disable-background-networking", null),
                ("disable-component-update", null),
                ("disable-client-side-phishing-detection", null),
                ("disable-sync", null),
                ("disable-translate", null),
                ("disable-background-timer-throttling", null),
                ("disable-extensions", null),
                ("disable-plugins", null),
                ("disable-spell-checking", null),
                ("disable-preconnect", null),
                ("disable-domain-reliability", null),
                // ("disable-reading-from-canvas", null), // Потрібно для ігор (читання canvas)
                ("disable-databases", null),
                // ("disable-local-storage", null), // Потрібно для ігор (HexGL)
                ("aggressive-cache-discard", null),
                // ("disable-gpu-shader-disk-cache", null), // Потрібно для WebGL ігор (шейдери)
                
                // Memory pressure handling
                ("memory-pressure-thresholds", "1024,2048,4096"),
                // Removed enable-low-end-device-mode to allow WebGL games to work
                
                // === WEBGL/GAMES SUPPORT ===
                ("enable-webgl", null),
                ("enable-webgl2", null),
                ("ignore-gpu-blocklist", null), // Дозволити GPU навіть якщо драйвер у "чорному списку"
                ("allow-file-access-from-files", null), // Дозволити XHR до file:// URL (потрібно для HexGL шейдерів)
                ("allow-file-access", null), // Дозволити Image() завантаження з file:// (потрібно для текстур)
                ("disable-web-security", null), // Вимкнути CORS для локальних файлів (потрібно для текстур гри)
                ("enable-unsafe-webgpu", null), // Дозволити WebGPU для сучасних ігор
                
                // GPU memory - без обмежень для ігор
                
                // === DISK CACHE (Мінімальний) ===
                ("user-data-dir", userDataDir),
                ("disk-cache-dir", diskCacheDir),
                ("disk-cache-size", (32 * 1024 * 1024).ToString()), // 32MB max
                // media-cache-size - без обмежень для ігор та відео
                
                // === PERFORMANCE ===
                ("enable-features", "BackForwardCache,LazyFrameLoading,LazyImageLoading"),
                
                // === PROCESS MODEL (Економія RAM) ===
                ("process-per-site", null),
                ("disable-site-isolation-trials", null),
                ("disable-site-isolation-for-policy", null),
                ("in-process-gpu", null), // GPU в основному процесі для економії RAM
                
                // === USER AGENT ===
                ("user-agent", userAgent),
                
                // === ПОВНА ЗАБОРОНА ПЕРЕНАПРАВЛЕННЯ НА ЗОВНІШНІ БРАУЗЕРИ ===
                // Значення "1" = увімкнено заборону, null/empty = функція активна
                ("disable-external-protocol-handler", "1"), // КРИТИЧНО: Вимкнути ВСІ зовнішні протоколи
                ("disable-prompt-on-repost", "1"),
                ("disable-hang-monitor", "1"),
                ("no-first-run", "1"),
                ("no-default-browser-check", "1"),
                ("disable-default-apps", "1"),
                ("disable-popup-blocking", "1"), // Popup всередині браузера
                ("disable-external-intent-requests", "1"), // Заборона зовнішніх intent
                ("disable-protocol-handler-check", "1"),
                ("disable-pdf-extension", "1"),
                
                // КРИТИЧНО: Заборона відкриття зовнішніх URL через систему
                ("disable-features", "CalculateNativeWinOcclusion,IsolateOrigins,SitePerProcess,AutofillServerCommunication,MediaRouter,Translate,OptimizationHints,GpuMemoryBufferVideoFrames,GpuMemoryBufferCompositorResources,RawDraw,CanvasOopRasterization,ExternalProtocolDialog,IntentPicker,NativeNotifications,OpenLinkInExternalApp,NavigateEventHandling,ExternalBrowserIntegration"),
                
                // Заборона будь-яких зовнішніх обробників URL
                ("autoplay-policy", "no-user-gesture-required"),
                ("disable-hang-monitor", "1"),
                ("disable-ipc-flooding-protection", "1"),
                
                // === V8 ===
                ("enable-v8-idle-tasks", null),
                ("v8-cache-options", "none"), // Вимкнути кеш V8 для економії RAM
                ("v8-cache-strategies-for-cache-storage", "off")
            };

#if DEBUG
            // Open DevTools remote debugging port in Debug builds
            var withDebug = new System.Collections.Generic.List<(string key, string? value)>(switches)
            {
                ("remote-debugging-port", "9223")
            };
            switches = withDebug.ToArray();
#endif

            // Try to find a way in the WebViewControl assembly to pass command-line switches before initialization.
            var webViewType = typeof(WebView);
            var asm = webViewType.Assembly;
            var applied = false;

            foreach (var t in asm.GetTypes())
            {
                try
                {
                    // 1) Look for a static IDictionary<string,string> switches holder
                    var props = t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    foreach (var p in props)
                    {
                        var name = p.Name.ToLowerInvariant();
                        if (name.Contains("commandline") || name.Contains("switch") || name.Contains("argument"))
                        {
                            var pt = p.PropertyType;
                            if (typeof(System.Collections.IDictionary).IsAssignableFrom(pt))
                            {
                                var dict = p.GetValue(null) as System.Collections.IDictionary;
                                if (dict != null)
                                {
                                    foreach (var (key, value) in switches)
                                    {
                                        var k = key;
                                        var v = value ?? string.Empty;
                                        if (dict.Contains(k)) dict[k] = v; else dict.Add(k, v);
                                    }
                                    applied = true;
                                }
                            }
                        }
                    }

                    // 2) Look for static methods to add switches
                    var methods = t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    foreach (var m in methods)
                    {
                        var n = m.Name.ToLowerInvariant();
                        if (!(n.Contains("commandline") || n.Contains("switch") || n.Contains("argument") || n.Contains("chromium")))
                            continue;

                        var pars = m.GetParameters();
                        try
                        {
                            if (pars.Length == 2 && pars[0].ParameterType == typeof(string) && pars[1].ParameterType == typeof(string))
                            {
                                foreach (var (key, value) in switches)
                                {
                                    m.Invoke(null, new object[] { key, value ?? string.Empty });
                                }
                                applied = true;
                            }
                            else if (pars.Length == 1 && pars[0].ParameterType == typeof(string))
                            {
                                foreach (var (key, value) in switches)
                                {
                                    var arg = value is null ? $"--{key}" : $"--{key}={value}";
                                    m.Invoke(null, new object[] { arg });
                                }
                                applied = true;
                            }
                        }
                        catch
                        {
                            // Ignore method-level failures
                        }
                    }
                }
                catch { /* ignore type-level reflection issues */ }
            }

            // Optional: as a fallback, try a known environment variable pattern recognized by some wrappers.
            if (!applied)
            {
                var argsJoined = string.Join(" ", System.Linq.Enumerable.Select(switches, s => s.value is null ? $"--{s.key}" : $"--{s.key}={s.value}"));
                Environment.SetEnvironmentVariable("CEF_ADDITIONAL_ARGS", argsJoined, EnvironmentVariableTarget.Process);
            }
        }
        catch
        {
            // no-op
        }
    }
}