// filepath: e:\VetaleBrowser\VetaleBrowser\VetaleBrowser.Core\Services\Windows\WindowsProcessTitleService.cs
#nullable enable
using System;
using System.Runtime.InteropServices;

namespace VetaleBrowser.VetaleBrowser.Core.Services.Windows
{
    /// <summary>
    /// Best-effort helper to set a human-readable title/description on a process for display in Task Manager.
    /// Uses Windows 10+ SetProcessDescription if available.
    /// </summary>
    internal static class WindowsProcessTitleService
    {
        public static bool TrySetProcessTitle(int processId, string title)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;
            if (string.IsNullOrWhiteSpace(title)) return false;

            IntPtr hProc = IntPtr.Zero;
            try
            {
                hProc = OpenProcess(0x0200 /* PROCESS_SET_INFORMATION */ | 0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, (uint)processId);
                if (hProc == IntPtr.Zero)
                    return false;

                // Dynamically resolve SetProcessDescription to avoid load failures on older Windows.
                IntPtr hKernel = GetModuleHandle("kernel32.dll");
                if (hKernel == IntPtr.Zero)
                    hKernel = LoadLibrary("kernel32.dll");
                if (hKernel == IntPtr.Zero)
                    return false;

                IntPtr pfn = GetProcAddress(hKernel, "SetProcessDescription");
                if (pfn == IntPtr.Zero)
                {
                    // API not available on this OS
                    return false;
                }

                var setDesc = Marshal.GetDelegateForFunctionPointer<SetProcessDescriptionDelegate>(pfn);
                int hr = setDesc(hProc, title);
                return hr == 0; // S_OK
            }
            catch
            {
                return false;
            }
            finally
            {
                if (hProc != IntPtr.Zero) CloseHandle(hProc);
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate int SetProcessDescriptionDelegate(IntPtr hProcess, string lpDescription);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);
    }
}

