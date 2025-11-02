#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VetaleBrowser.VetaleBrowser.Core.Services.Windows
{
    /// <summary>
    /// Tracks and claims child process IDs spawned by the current application.
    /// Each claim operation returns only PIDs not yet claimed by any tab.
    /// Heuristic: assumes new renderer/utility processes appear after a tab navigates/initializes.
    /// </summary>
    internal static class TabProcessTracker
    {
        private static readonly object Sync = new();
        private static readonly HashSet<int> Claimed = new();

        public static IReadOnlyList<int> ClaimNewChildPids()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Array.Empty<int>();

            var currentPid = Process.GetCurrentProcess().Id;
            var children = WindowsProcessHelper.GetChildProcessIds(currentPid);
            if (children.Count == 0) return Array.Empty<int>();

            var result = new List<int>();
            lock (Sync)
            {
                foreach (var pid in children)
                {
                    if (Claimed.Add(pid))
                    {
                        result.Add(pid);
                    }
                }
            }
            return result;
        }

        public static void ReleasePids(IEnumerable<int> pids)
        {
            lock (Sync)
            {
                foreach (var pid in pids)
                {
                    Claimed.Remove(pid);
                }
            }
        }
    }
}
