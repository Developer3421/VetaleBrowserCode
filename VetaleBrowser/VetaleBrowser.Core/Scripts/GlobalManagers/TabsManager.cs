using System;
using System.Collections.Generic;
using VetaleBrowser.VetaleBrowser.Core.Scripts.Models;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.GlobalManagers
{
    /// <summary>
    /// Manages a collection of TabWorker instances: create, activate, close, and enumerate.
    /// </summary>
    public sealed class TabsManager : IDisposable
    {
        private readonly List<TabWorker> _workers = new();
        public IReadOnlyList<TabWorker> Workers => _workers;

        public TabWorker? Active { get; private set; }

        public event EventHandler<TabWorker>? TabCreated;
        public event EventHandler<TabWorker>? TabActivated;
        public event EventHandler<TabWorker>? TabClosed;

        public TabWorker Create(string? initialUrl = null)
        {
            var w = new TabWorker();
            _workers.Add(w);
            TabCreated?.Invoke(this, w);

            if (!string.IsNullOrWhiteSpace(initialUrl))
            {
                w.Navigate(initialUrl);
            }

            Activate(w);
            return w;
        }

        public void Activate(TabWorker worker)
        {
            if (worker == null) throw new ArgumentNullException(nameof(worker));
            foreach (var w in _workers) w.IsActive = false;
            worker.IsActive = true;
            Active = worker;
            TabActivated?.Invoke(this, worker);
        }

        public void Close(TabWorker worker)
        {
            if (worker == null) throw new ArgumentNullException(nameof(worker));

            // Determine if the closing tab is currently active
            var wasActive = ReferenceEquals(Active, worker);

            // Find and remove the worker first to avoid re-activating a closed instance
            var index = _workers.IndexOf(worker);
            if (index < 0)
                return; // already removed

            _workers.RemoveAt(index);

            // If closing the active tab, decide which remaining tab should become active
            if (wasActive)
            {
                TabWorker? next = null;
                if (_workers.Count > 0)
                {
                    // Prefer the previous neighbor; if none, take the first remaining
                    var nextIndex = Math.Max(0, Math.Min(index - 1, _workers.Count - 1));
                    next = _workers[nextIndex];
                }

                if (next != null)
                {
                    Activate(next);
                }
                else
                {
                    // No tabs left; clear active and let UI decide to create a default tab
                    foreach (var w in _workers) w.IsActive = false;
                    Active = null;
                }
            }

            // Notify listeners that a tab was closed (after Active is updated)
            TabClosed?.Invoke(this, worker);

            // Best-effort dispose of the closed worker
            try
            {
                worker.Dispose();
            }
            catch
            {
                // ignore dispose failures
            }
        }

        /// <summary>
        /// Apply a global mute state to all existing TabWorkers. This approximates process-level mute
        /// in environments where the CEF host-level API is not exposed by the wrapper.
        /// </summary>
        public void SetGlobalMute(bool muted)
        {
            foreach (var w in _workers.ToArray())
            {
                try { w.IsMuted = muted; } catch { /* ignore */ }
            }
        }

        public void Dispose()
        {
            foreach (var w in _workers.ToArray())
            {
                try { w.Dispose(); } catch { /* ignore */ }
            }
            _workers.Clear();
            Active = null;
        }
    }
}
