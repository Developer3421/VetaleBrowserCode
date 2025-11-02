#nullable enable
using System;
using System.Runtime.InteropServices;

namespace VetaleBrowser.VetaleBrowser.Core.Services.Windows
{
    /// <summary>
    /// Windows CoreAudio helper to mute/unmute audio sessions by process ID using IAudioSessionManager2.
    /// </summary>
    internal static class WindowsAudioSessionService
    {
        public static bool TrySetProcessMute(int processId, bool mute)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return false;

            try
            {
                // Get default render (output) device
                if (ComImports.MMDeviceEnumerator_Create(out var deviceEnumerator) != 0 || deviceEnumerator == null)
                    return false;

                try
                {
                    var hr = deviceEnumerator.GetDefaultAudioEndpoint(ComImports.EDataFlow.eRender, ComImports.ERole.eMultimedia, out var device);
                    if (hr != 0 || device == null)
                        return false;

                    try
                    {
                        var iid = typeof(ComImports.IAudioSessionManager2).GUID;
                        object? obj;
                        hr = device.Activate(ref iid, ComImports.CLSCTX.CLSCTX_ALL, IntPtr.Zero, out obj);
                        if (hr != 0 || obj is not ComImports.IAudioSessionManager2 mgr)
                            return false;

                        try
                        {
                            hr = mgr.GetSessionEnumerator(out var enumerator);
                            if (hr != 0 || enumerator == null)
                                return false;

                            try
                            {
                                enumerator.GetCount(out var count);
                                for (int i = 0; i < count; i++)
                                {
                                    if (enumerator.GetSession(i, out var control) != 0 || control == null)
                                        continue;

                                    try
                                    {
                                        var control2 = control as ComImports.IAudioSessionControl2;
                                        if (control2 == null)
                                        {
                                            // Try QI to IAudioSessionControl2
                                            control2 = (ComImports.IAudioSessionControl2)control;
                                        }

                                        if (control2.GetProcessId(out var pid) == 0 && pid == processId)
                                        {
                                            // Query session's simple audio volume interface
                                            var vol = control as ComImports.ISimpleAudioVolume;
                                            if (vol != null)
                                            {
                                                vol.SetMute(mute, Guid.Empty);
                                                Marshal.ReleaseComObject(vol);
                                                return true;
                                            }

                                            // Fallback: resolve session's grouping GUID and ask manager for volume
                                            if (control.GetGroupingParam(out var grouping) == 0)
                                            {
                                                if (mgr.GetSimpleAudioVolume(ref grouping, 0, out var vol2) == 0 && vol2 != null)
                                                {
                                                    vol2.SetMute(mute, Guid.Empty);
                                                    Marshal.ReleaseComObject(vol2);
                                                    return true;
                                                }
                                            }
                                        }
                                    }
                                    catch
                                    {
                                        // ignore this session
                                    }
                                    finally
                                    {
                                        Marshal.ReleaseComObject(control);
                                    }
                                }
                            }
                            finally
                            {
                                Marshal.ReleaseComObject(enumerator);
                            }
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(mgr);
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(device);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(deviceEnumerator);
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static class ComImports
        {
            // COM base
            [Flags]
            public enum CLSCTX : uint
            {
                CLSCTX_INPROC_SERVER = 0x1,
                CLSCTX_INPROC_HANDLER = 0x2,
                CLSCTX_LOCAL_SERVER = 0x4,
                CLSCTX_REMOTE_SERVER = 0x10,
                CLSCTX_ALL = CLSCTX_INPROC_SERVER | CLSCTX_INPROC_HANDLER | CLSCTX_LOCAL_SERVER | CLSCTX_REMOTE_SERVER
            }

            public enum EDataFlow
            {
                eRender,
                eCapture,
                eAll,
            }

            public enum ERole
            {
                eConsole,
                eMultimedia,
                eCommunications
            }

            // Declare events interface before any references
            [ComImport]
            [Guid("BFA971F1-4D5E-40BB-935E-967039BFBEE4")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IAudioSessionEvents
            {
                int OnDisplayNameChanged([MarshalAs(UnmanagedType.LPWStr)] string newDisplayName, Guid eventContext);
                int OnIconPathChanged([MarshalAs(UnmanagedType.LPWStr)] string newIconPath, Guid eventContext);
                int OnSimpleVolumeChanged(float newVolume, bool newMute, Guid eventContext);
                int OnChannelVolumeChanged(uint channelCount, IntPtr newChannelVolumeArray, uint changedChannel, Guid eventContext);
                int OnGroupingParamChanged(ref Guid newGroupingParam, Guid eventContext);
                int OnStateChanged(int newState);
                int OnSessionDisconnected(int disconnectReason);
            }

            [ComImport]
            [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IMMDeviceEnumerator
            {
                int NotImpl1();

                int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice? ppDevice);
                // We only need the default endpoint for our scenario; other methods omitted.
            }

            [ComImport]
            [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IMMDevice
            {
                int Activate(ref Guid iid, CLSCTX dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object? ppInterface);
            }

            [ComImport]
            [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IAudioSessionManager2
            {
                // IAudioSessionManager
                int GetAudioSessionControl(ref Guid audioSessionGuid, uint streamFlags, out IAudioSessionControl? sessionControl);
                int GetSimpleAudioVolume(ref Guid audioSessionGuid, uint streamFlags, out ISimpleAudioVolume? audioVolume);

                // IAudioSessionManager2
                int GetSessionEnumerator(out IAudioSessionEnumerator? sessionEnum);
                int RegisterSessionNotification(IAudioSessionNotification sessionNotification);
                int UnregisterSessionNotification(IAudioSessionNotification sessionNotification);
                int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionId, IAudioVolumeDuckNotification duckNotification);
                int UnregisterDuckNotification(IAudioVolumeDuckNotification duckNotification);
            }

            [ComImport]
            [Guid("F6E4C0A0-46C9-342E-8BE8-5A0D6F8E86A8")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IAudioSessionEnumerator
            {
                int GetCount(out int sessionCount);
                int GetSession(int sessionCount, out IAudioSessionControl? session);
            }

            [ComImport]
            [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IAudioSessionControl
            {
                int GetState(out int state);
                int GetDisplayName(out IntPtr name);
                int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, Guid eventContext);
                int GetIconPath(out IntPtr path);
                int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string value, Guid eventContext);
                int GetGroupingParam(out Guid groupingParam);
                int SetGroupingParam(Guid @override, Guid eventContext);
                int RegisterAudioSessionNotification(IAudioSessionEvents newNotifications);
                int UnregisterAudioSessionNotification(IAudioSessionEvents newNotifications);
            }

            [ComImport]
            [Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IAudioSessionControl2
            {
                // IAudioSessionControl
                int GetState(out int state);
                int GetDisplayName(out IntPtr name);
                int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, Guid eventContext);
                int GetIconPath(out IntPtr path);
                int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string value, Guid eventContext);
                int GetGroupingParam(out Guid groupingParam);
                int SetGroupingParam(Guid @override, Guid eventContext);
                int RegisterAudioSessionNotification(IAudioSessionEvents newNotifications);
                int UnregisterAudioSessionNotification(IAudioSessionEvents newNotifications);

                // IAudioSessionControl2
                int GetSessionIdentifier(out IntPtr pRetVal);
                int GetSessionInstanceIdentifier(out IntPtr pRetVal);
                int GetProcessId(out int pRetVal);
                int IsSystemSoundsSession();
                int SetDuckingPreference(bool optOut);
            }

            [ComImport]
            [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface ISimpleAudioVolume
            {
                int SetMasterVolume(float fLevel, Guid eventContext);
                int GetMasterVolume(out float pfLevel);
                int SetMute(bool bMute, Guid eventContext);
                int GetMute(out bool pbMute);
            }

            [ComImport]
            [Guid("641DD20B-4D41-49CC-ABA3-174B9477BB08")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IAudioSessionNotification
            {
                int OnSessionCreated(IAudioSessionControl newSession);
            }

            [ComImport]
            [Guid("C3B284D4-6D2C-40AE-BD35-DFCB18DF7B52")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IAudioVolumeDuckNotification
            {
                int OnVolumeDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionId, int countCommunicationSessions);
                int OnVolumeUnduckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionId);
            }

            [ComImport]
            [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
            private class MMDeviceEnumeratorComObject { }

            public static int MMDeviceEnumerator_Create(out IMMDeviceEnumerator? enumerator)
            {
                var clsid = typeof(MMDeviceEnumeratorComObject).GUID;
                var iid = typeof(IMMDeviceEnumerator).GUID;
                var hr = CoCreateInstance(ref clsid, null, CLSCTX.CLSCTX_ALL, ref iid, out var obj);
                enumerator = obj as IMMDeviceEnumerator;
                return hr;
            }

            [DllImport("ole32.dll")]
            private static extern int CoCreateInstance(ref Guid rclsid, [MarshalAs(UnmanagedType.IUnknown)] object? pUnkOuter, CLSCTX dwClsContext, ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object? ppv);
        }
    }
}
