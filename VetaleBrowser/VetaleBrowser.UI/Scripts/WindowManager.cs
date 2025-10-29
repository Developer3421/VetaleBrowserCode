using System;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia.Input;
using Avalonia.Controls;

namespace VetaleBrowser.VetaleBrowser.UI.Scripts
{
    // WindowManager centralizes window/menu control logic so MainWindow stays minimal.
    public class WindowManager
    {
        private readonly Window _window;

        public WindowManager(Window window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
        }

        // Minimize the window
        public void Minimize()
        {
            try
            {
                _window.WindowState = WindowState.Minimized;
            }
            catch
            {
                // best-effort
            }
        }

        // Toggle between maximized and normal
        public void ToggleMaximize()
        {
            try
            {
                _window.WindowState = _window.WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
            catch
            {
                // ignore
            }
        }

        // Close the window
        public void Close()
        {
            try
            {
                _window.Close();
            }
            catch
            {
                // ignore
            }
        }

        // Called when top bar is double-tapped
        public void OnTopBarDoubleTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        // Expose a method to invoke the OS snap layout UI (Win+Z on Windows 11)
        public void InvokeSystemSnapLayout()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;

            try
            {
                // Use keybd_event (simpler cross-version approach) to send Win+Z
                const byte vkLwin = 0x5B;
                const byte vkZ = 0x5A;
                const uint keyeventfKeyup = 0x0002;

                keybd_event(vkLwin, 0, 0, UIntPtr.Zero);
                // small delay to ensure the system registers the modifier
                Thread.Sleep(10);
                keybd_event(vkZ, 0, 0, UIntPtr.Zero);
                Thread.Sleep(10);
                keybd_event(vkZ, 0, keyeventfKeyup, UIntPtr.Zero);
                keybd_event(vkLwin, 0, keyeventfKeyup, UIntPtr.Zero);
            }
            catch
            {
                // best-effort; failures aren't fatal
            }
        }

        // Optional helper if callers want to trigger drag from here (will call Window.BeginMoveDrag if available)
        public void TryBeginMoveDrag(PointerPressedEventArgs e)
        {
            // Avalonia's PointerPressedEventArgs is non-null when this handler is invoked by the UI system
            try
            {
                _window.BeginMoveDrag(e);
            }
            catch
            {
                // Some platforms or versions might not allow BeginMoveDrag from here; ignore.
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    }
}
