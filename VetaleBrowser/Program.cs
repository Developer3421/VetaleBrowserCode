using Avalonia;
using System;
using WebViewControl;

namespace VetaleBrowser;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Ensure GPU rendering and related Chromium features are enabled for CEF (CefGlue) before any WebView is created.
        ConfigureWebEngines();

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
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

            // Define Chromium/CEF switches focused on GPU and performance.
            var switches = new (string key, string? value)[]
            {
                ("enable-gpu", null),
                ("ignore-gpu-blocklist", null),
                ("disable-software-rasterizer", null),
                ("enable-gpu-rasterization", null),
                ("enable-zero-copy", null),
                ("enable-native-gpu-memory-buffers", null),
                ("enable-accelerated-video-decode", null),
                ("enable-accelerated-video-encode", null),
                ("enable-media-foundation-widevine", null),
                ("enable-webgl", null),
                ("use-angle", "d3d11"),
                ("user-data-dir", userDataDir),
                ("disk-cache-dir", diskCacheDir),
                ("enable-features", "CanvasOopRasterization,UseSkiaRenderer,PlatformHEVCDecoderSupport,SharedArrayBuffer,AllowContentInitiatedDataUrlNavigations"),
                ("disable-features", "CalculateNativeWinOcclusion")
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