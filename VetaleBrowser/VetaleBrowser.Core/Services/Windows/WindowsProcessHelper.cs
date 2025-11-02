using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace VetaleBrowser.VetaleBrowser.Core.Services.Windows
{
    /// <summary>
    /// Windows helper to enumerate child processes of a given parent PID using Toolhelp snapshots.
    /// </summary>
    internal static class WindowsProcessHelper
    {
        public static IReadOnlyList<int> GetChildProcessIds(int parentPid)
        {
            var result = new List<int>();
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return result;

            IntPtr snapshot = IntPtr.Zero;
            try
            {
                snapshot = CreateToolhelp32Snapshot(0x00000002, 0); // TH32CS_SNAPPROCESS
                if (snapshot == new IntPtr(-1))
                    return result;

                var pe = new ProcessEntry32 { dwSize = (uint)Marshal.SizeOf<ProcessEntry32>() };
                if (!Process32First(snapshot, ref pe))
                    return result;

                do
                {
                    if (pe.th32ParentProcessID == (uint)parentPid)
                    {
                        result.Add((int)pe.th32ProcessID);
                    }
                } while (Process32Next(snapshot, ref pe));
            }
            catch (Win32Exception)
            {
                // ignore
            }
            finally
            {
                if (snapshot != IntPtr.Zero && snapshot != new IntPtr(-1))
                    CloseHandle(snapshot);
            }

            return result;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct ProcessEntry32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool Process32First(IntPtr hSnapshot, ref ProcessEntry32 lppe);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool Process32Next(IntPtr hSnapshot, ref ProcessEntry32 lppe);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
    }
}
