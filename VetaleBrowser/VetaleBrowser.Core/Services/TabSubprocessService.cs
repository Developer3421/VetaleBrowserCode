// filepath: e:\VetaleBrowser\VetaleBrowser\VetaleBrowser.Core\Services\TabSubprocessService.cs
using System;
using System.Diagnostics;

namespace VetaleBrowser.VetaleBrowser.Core.Services
{
    /// <summary>
    /// Lightweight tab service that tracks tab identity without spawning separate processes.
    /// MEMORY OPTIMIZATION: No longer spawns subprocess per tab - was consuming ~50MB per tab.
    /// Uses virtual process tracking for tab identification and compatibility with existing code.
    /// </summary>
    public sealed class TabSubprocessService : IDisposable
    {
        // MEMORY OPTIMIZATION: Disabled subprocess spawning - was consuming ~50MB per tab
        private readonly Guid _tabId;
        private string _title = "New Tab";
        private bool _disposed;

        // Static counter for virtual "process" IDs (for compatibility with existing code)
        private static int _virtualPidCounter = 100000;
        private readonly int _virtualPid;

        public int? ProcessId => _disposed ? null : _virtualPid;
        public Guid TabId => _tabId;
        public string Title => _title;

        public TabSubprocessService(Guid tabId)
        {
            _tabId = tabId;
            _virtualPid = System.Threading.Interlocked.Increment(ref _virtualPidCounter);
            Debug.WriteLine($"[TabSubprocessService] Created virtual tab {_virtualPid} for {tabId} (no subprocess spawned - RAM optimized)");
        }

        public void UpdateTitle(string title)
        {
            if (_disposed) return;
            _title = title ?? "New Tab";
            Debug.WriteLine($"[TabSubprocessService] Tab {_virtualPid} title: {_title}");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Debug.WriteLine($"[TabSubprocessService] Disposed virtual tab {_virtualPid}");
        }
    }
}
