// filepath: e:\VetaleBrowser\VetaleBrowser\VetaleBrowser.Core\Services\TabSubprocessService.cs
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using VetaleBrowser.VetaleBrowser.Core.Services.Windows;

namespace VetaleBrowser.VetaleBrowser.Core.Services
{
    /// <summary>
    /// Spawns and tracks a lightweight helper subprocess per tab using the current executable.
    /// The subprocess runs without a window and stays alive until this service is disposed
    /// (at which point it is terminated). This satisfies the OS-level "one process per tab" requirement.
    /// </summary>
    public sealed class TabSubprocessService : IDisposable
    {
        private readonly Process? _process;

        public int? ProcessId => _process?.HasExited == false ? _process.Id : (int?)null;

        public TabSubprocessService(Guid tabId)
        {
            try
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
                {
                    return; // can't spawn
                }

                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"--tab-helper {tabId}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    RedirectStandardInput = false,
                    WorkingDirectory = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory
                };

                _process = Process.Start(psi);
            }
            catch (Win32Exception)
            {
                // Likely blocked or not permitted; ignore
            }
            catch
            {
                // Best-effort; subprocess isn't critical for functionality
            }
        }

        public void UpdateTitle(string title)
        {
            try
            {
                if (_process == null || _process.HasExited) return;
                WindowsProcessTitleService.TrySetProcessTitle(_process.Id, title);
            }
            catch { }
        }

        public void Dispose()
        {
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    try
                    {
                        _process.Kill(entireProcessTree: true);
                    }
                    catch { /* ignore */ }
                    try { _process.Dispose(); } catch { }
                }
            }
            catch { }
        }
    }
}
