using System;
using CefSharp.Avalonia;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.Browser;

/// <summary>
/// Central CEF configuration for the CefSharp.Avalonia engine.
/// Typed <see cref="CefSettings"/> are applied per-view by <see cref="CefSharpAdapter"/>.
/// </summary>
public static class CefBrowserConfig
{
    public static readonly string UserDataDir;
    public static readonly string DiskCacheDir;

    static CefBrowserConfig()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        UserDataDir = System.IO.Path.Combine(appData, "VetaleBrowser", "cef_data");
        DiskCacheDir = System.IO.Path.Combine(appData, "VetaleBrowser", "cef_cache");
        EnsureDirectories();
    }

    public static void EnsureDirectories()
    {
        try { System.IO.Directory.CreateDirectory(UserDataDir); } catch { }
        try { System.IO.Directory.CreateDirectory(DiskCacheDir); } catch { }
    }

    public static CefSettings CreateSettings()
    {
        var settings = new CefSettings
        {
            UserDataPath = UserDataDir,
            CachePath = DiskCacheDir,
            RootCachePath = UserDataDir,
            // Як у звичайних браузерах: куки/сесія переживають перезапуск,
            // Google не розлогінюється і не віддає спрощену сторінку при старті
            PersistSessionCookies = true,
            PersistUserPreferences = true,
            // Chrome runtime замість Alloy: тільки в ньому доступні
            // WebUI-сторінки chrome:// (gpu, version, ...). Без цього білий екран.
            ChromeRuntime = true,
            // Windowed рендеринг: без OSR CEF не форсує --disable-gpu-compositing (як у Lite)
            WindowlessRenderingEnabled = false,
            // ПОВНІ ЛОГИ (діагностика спрощеного рендера): verbose + файл
            LogSeverity = CefLogSeverity.Verbose,
            LogFile = System.IO.Path.Combine(UserDataDir, "cef.log"),
            // Small V8 heap: one renderer, low RAM footprint
            JavascriptFlags = "--max-old-space-size=128 --optimize-for-size",
        };
        // ПОВНИЙ СПИСОК ІЗ Vetale Browser Lite (ідеальна комбінація).
        // CommandLineSwitches тут — List<string>, тому формат "key=value".
        // NOTE: без software fallback жоден збій GPU = білі сторінки,
        // тому "disable-software-rasterizer" НЕ додаємо (як у Lite).
        try
        {
            var switches = settings.CommandLineSwitches;

            // GPU
            switches.Add("enable-gpu=1");
            switches.Add("ignore-gpu-blocklist=1");

            // ДІАГНОСТИКА: повні логи CEF + verbose, без внутрішнього блоклиста графіки
            switches.Add("enable-logging=1");
            switches.Add("v=1");

            // GPU COMPOSITING
            switches.Remove("disable-gpu");
            switches.Remove("disable-gpu-compositing");
            switches.Remove("disable-gpu-vsync");
            switches.Add("enable-gpu-compositing=1");

            // GPU RASTERIZATION (без zero-copy: з ним відеокадри відриваються
            // від вікна при репарентінгу WebView у фулскріні)
            switches.Add("enable-gpu-rasterization=1");
            switches.Add("enable-oop-rasterization=1");

            // DIRECT3D / ANGLE (D3D11 поверх D3D12 — DirectX 12 шлях)
            switches.Add("use-angle=d3d11on12");

            // WEBGL / WEBGPU
            switches.Add("enable-webgl=1");
            switches.Add("enable-webgl2=1");
            switches.Add("enable-unsafe-webgpu=1");

            // EXPERIMENTAL GPU FEATURES (один ключ)
            switches.Add("enable-features=WebNN,WebMachineLearningNeuralNetwork,WebGPU,SkiaGraphite,Accelerated2dCanvas,CanvasOopRasterization");

            // HARDWARE VIDEO (тільки декодування/кодування; без video-frames
            // буферів і без hardware overlays — вони не слідують за вікном
            // при фулскріні: відео гуляє, відривається, не вміщається)
            switches.Add("enable-accelerated-video-decode=1");
            switches.Add("enable-accelerated-video-encode=1");

            // VIDEO GPU MEMORY + HARDWARE OVERLAYS: ВИМКНЕНО.
            // У віконному (HWND) режимі оверлеї не слідують за WebView при
            // переходах між вкладками/фулскріном: відео гуляє, частково
            // відокремлюється або не вміщається. Відео йде звичайним композитом.
            switches.Add("disable-hardware-overlays=1");

            // RASTER THREADS
            switches.Add("num-raster-threads=4");
            switches.Add("max-tiles-for-interest-area=512");
            switches.Add("enable-smooth-scrolling=1");

            // CANVAS
            switches.Add("enable-accelerated-2d-canvas=1");

            // BACKGROUND PERFORMANCE (не тротлити фон — як у Lite)
            switches.Add("disable-background-timer-throttling=1");
            switches.Add("disable-renderer-backgrounding=1");
            switches.Add("disable-backgrounding-occluded-windows=1");

            // V-SYNC: НЕ вимикаємо gpu vsync (як у Lite)

            // MEDIA / JAVASCRIPT / NETWORK
            switches.Add("enable-media-stream=1");
            switches.Add("enable-javascript=1");
            switches.Add("enable-quic=1");
        }
        catch { }
        // DevTools-порт потрібен завжди (не тільки DEBUG): через нього йде
        // детект HTML5-фулскріна (JS-моста в обгортці нема).
        settings.RemoteDebuggingPort = 9223;
        return settings;
    }
}
