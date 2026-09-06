using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VetaleBrowser.VetaleBrowser.Core.Services.Windows;

/// <summary>
/// Detects HTML5 video fullscreen (e.g. YouTube "F" button) WITHOUT a JS bridge.
/// The bundled CEF build renders in-page fullscreen itself in a separate
/// captionless top-level window covering a whole monitor. We poll for such
/// windows owned by the CEF native process and raise an event on change.
/// Windows-only; on other platforms the watcher stays idle.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class VideoFullscreenWatcher : IDisposable
{
    public event EventHandler<bool>? FullscreenChanged;

    private readonly Func<IntPtr> _mainWindowHandleProvider;
    private System.Threading.Timer? _timer;
    private bool _lastState;
    private int _stableCount;
    private bool _disposed;

    private const int PollMs = 400;
    private const int StablePolls = 2;

    public VideoFullscreenWatcher(Func<IntPtr> mainWindowHandleProvider)
    {
        _mainWindowHandleProvider = mainWindowHandleProvider;
    }

    public void Start()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        if (_timer != null) return;
        _timer = new System.Threading.Timer(_ => PollOnce(), null, PollMs, PollMs);
    }

    public void Stop()
    {
        try { _timer?.Dispose(); } catch { }
        _timer = null;
    }

    private void PollOnce()
    {
        try
        {
            bool found = FindCefFullscreenWindow();
            if (found == _lastState)
            {
                _stableCount = StablePolls; // already stable
                return;
            }

            _stableCount++;
            if (_stableCount >= StablePolls)
            {
                _stableCount = StablePolls;
                _lastState = found;
                try { FullscreenChanged?.Invoke(this, found); } catch { }
            }
        }
        catch { }
    }

    private bool FindCefFullscreenWindow()
    {
        IntPtr mainHwnd;
        try { mainHwnd = _mainWindowHandleProvider(); } catch { return false; }

        var monitors = GetMonitorRects();
        bool found = false;

        EnumWindows((hwnd, _) =>
        {
            if (found) return false;
            if (hwnd == IntPtr.Zero || hwnd == mainHwnd) return true;
            if (!IsWindowVisible(hwnd)) return true;

            int style = GetWindowLong(hwnd, GWL_STYLE);
            const int WS_CAPTION = 0x00C00000;
            if ((style & WS_CAPTION) != 0) return true; // has title bar -> not video overlay

            if (!GetWindowRect(hwnd, out RECT r)) return true;
            int w = r.Right - r.Left, h = r.Bottom - r.Top;
            if (w <= 0 || h <= 0) return true;

            bool coversMonitor = false;
            foreach (var m in monitors)
            {
                if (r.Left == m.Left && r.Top == m.Top &&
                    r.Right == m.Right && r.Bottom == m.Bottom)
                {
                    coversMonitor = true;
                    break;
                }
            }
            if (!coversMonitor) return true;

            // Must belong to the CEF native process (not our UI, not random overlay).
            try
            {
                GetWindowThreadProcessId(hwnd, out uint pid);
                using var proc = System.Diagnostics.Process.GetProcessById((int)pid);
                var name = proc.ProcessName ?? "";
                if (name.Equals("CefBrowser.Native", StringComparison.OrdinalIgnoreCase) ||
                    name.IndexOf("cef", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    found = true;
                    return false;
                }
            }
            catch { }

            return true;
        }, IntPtr.Zero);

        return found;
    }

    private static List<RECT> GetMonitorRects()
    {
        var list = new List<RECT>();
        try
        {
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMon, _, _, _) =>
            {
                var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(hMon, ref mi))
                    list.Add(mi.rcMonitor);
                return true;
            }, IntPtr.Zero);
        }
        catch { }
        return list;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr lprcClip, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private const int GWL_STYLE = -16;

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
}
