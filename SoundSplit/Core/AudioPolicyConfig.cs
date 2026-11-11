using System;
using System.Runtime.InteropServices;

namespace SoundSplit.Core
{
    // ---------------------------------------------------------------------------------
    // Per-app audio routing.
    //
    // Windows 10 (1803+) and Windows 11 have a built-in, but undocumented, WinRT class
    // called "Windows.Media.Internal.AudioPolicyConfig". It is exactly what the
    // Settings app uses under "App volume and device preferences" / "Volume mixer"
    // to send one app to a different speaker. We talk to the same class, so the
    // routing is done by Windows itself: no drivers, no virtual cables, no lag.
    //
    // The interface ID changed in Windows build 21390, so both variants are declared.
    // The 19 "Unused" slots are methods we never call; they only keep the vtable
    // layout correct so SetPersistedDefaultAudioEndpoint lands on the right slot.
    // ---------------------------------------------------------------------------------

    internal enum EDataFlow { Render = 0, Capture = 1 }
    internal enum ERole { Console = 0, Multimedia = 1, Communications = 2 }

    [ComImport, Guid("2a59116d-6c4f-45e0-a74f-707e3fef9258"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioPolicyConfigFactory21H2
    {
        // IInspectable
        [PreserveSig] int GetIids(out int iidCount, out IntPtr iids);
        [PreserveSig] int GetRuntimeClassName(out IntPtr className);
        [PreserveSig] int GetTrustLevel(out int trustLevel);

        [PreserveSig] int Unused01(); [PreserveSig] int Unused02(); [PreserveSig] int Unused03();
        [PreserveSig] int Unused04(); [PreserveSig] int Unused05(); [PreserveSig] int Unused06();
        [PreserveSig] int Unused07(); [PreserveSig] int Unused08(); [PreserveSig] int Unused09();
        [PreserveSig] int Unused10(); [PreserveSig] int Unused11(); [PreserveSig] int Unused12();
        [PreserveSig] int Unused13(); [PreserveSig] int Unused14(); [PreserveSig] int Unused15();
        [PreserveSig] int Unused16(); [PreserveSig] int Unused17(); [PreserveSig] int Unused18();
        [PreserveSig] int Unused19();

        [PreserveSig] int SetPersistedDefaultAudioEndpoint(uint processId, EDataFlow flow, ERole role, IntPtr deviceId);
        [PreserveSig] int GetPersistedDefaultAudioEndpoint(uint processId, EDataFlow flow, ERole role, out IntPtr deviceId);
        [PreserveSig] int ClearAllPersistedApplicationDefaultEndpoints();
    }

    [ComImport, Guid("ab3d4648-e242-459f-b02f-541c70306324"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioPolicyConfigFactoryDownlevel
    {
        // IInspectable
        [PreserveSig] int GetIids(out int iidCount, out IntPtr iids);
        [PreserveSig] int GetRuntimeClassName(out IntPtr className);
        [PreserveSig] int GetTrustLevel(out int trustLevel);

        [PreserveSig] int Unused01(); [PreserveSig] int Unused02(); [PreserveSig] int Unused03();
        [PreserveSig] int Unused04(); [PreserveSig] int Unused05(); [PreserveSig] int Unused06();
        [PreserveSig] int Unused07(); [PreserveSig] int Unused08(); [PreserveSig] int Unused09();
        [PreserveSig] int Unused10(); [PreserveSig] int Unused11(); [PreserveSig] int Unused12();
        [PreserveSig] int Unused13(); [PreserveSig] int Unused14(); [PreserveSig] int Unused15();
        [PreserveSig] int Unused16(); [PreserveSig] int Unused17(); [PreserveSig] int Unused18();
        [PreserveSig] int Unused19();

        [PreserveSig] int SetPersistedDefaultAudioEndpoint(uint processId, EDataFlow flow, ERole role, IntPtr deviceId);
        [PreserveSig] int GetPersistedDefaultAudioEndpoint(uint processId, EDataFlow flow, ERole role, out IntPtr deviceId);
        [PreserveSig] int ClearAllPersistedApplicationDefaultEndpoints();
    }

    /// <summary>Friendly wrapper around Windows' per-app default audio device API.</summary>
    internal sealed class AudioPolicyConfig
    {
        private const string ClassName = "Windows.Media.Internal.AudioPolicyConfig";
        private const string MmDevApiToken = @"\\?\SWD#MMDEVAPI#";
        private const string RenderInterface = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";
        private const string CaptureInterface = "#{2eef81be-33fa-4800-9670-1cd474972c3f}";

        private readonly IAudioPolicyConfigFactory21H2? _new;
        private readonly IAudioPolicyConfigFactoryDownlevel? _old;

        public bool IsAvailable => _new != null || _old != null;
        public string? Error { get; }

        public AudioPolicyConfig()
        {
            try
            {
                var build = Environment.OSVersion.Version.Build;
                if (build < 17134)
                {
                    Error = "Per-app audio needs Windows 10 version 1803 (April 2018 Update) or newer.";
                    return;
                }

                // Windows changed the interface ID over the years, and which build got which ID
                // isn't documented. So ask for both and keep whichever one Windows hands back.
                Exception? last = null;
                try { _new = (IAudioPolicyConfigFactory21H2)Activate(typeof(IAudioPolicyConfigFactory21H2).GUID); }
                catch (Exception ex) { last = ex; }

                if (_new == null)
                {
                    try { _old = (IAudioPolicyConfigFactoryDownlevel)Activate(typeof(IAudioPolicyConfigFactoryDownlevel).GUID); }
                    catch (Exception ex) { last = ex; }
                }

                if (!IsAvailable)
                    Error = $"Windows refused the per-app audio API (build {build}): {last?.Message} [0x{last?.HResult:X8}]";
            }
            catch (Exception ex)
            {
                Error = "Windows refused the per-app audio API: " + ex.Message;
            }
        }

        private static object Activate(Guid iid)
        {
            var hr = WindowsCreateString(ClassName, (uint)ClassName.Length, out var hClass);
            Marshal.ThrowExceptionForHR(hr);
            try
            {
                hr = RoGetActivationFactory(hClass, ref iid, out var factoryPtr);
                Marshal.ThrowExceptionForHR(hr);
                try { return Marshal.GetObjectForIUnknown(factoryPtr); }
                finally { Marshal.Release(factoryPtr); }
            }
            finally { WindowsDeleteString(hClass); }
        }

        /// <summary>
        /// Sends the app that owns <paramref name="processId"/> to <paramref name="mmDeviceId"/>.
        /// Pass null/empty to go back to "Default" (follow the system's main device).
        /// Windows remembers this per app, even after a reboot.
        /// </summary>
        public bool SetAppOutput(int processId, string? mmDeviceId) =>
            SetAppEndpoint(processId, mmDeviceId, EDataFlow.Render);

        public bool SetAppInput(int processId, string? mmDeviceId) =>
            SetAppEndpoint(processId, mmDeviceId, EDataFlow.Capture);

        private bool SetAppEndpoint(int processId, string? mmDeviceId, EDataFlow flow)
        {
            if (!IsAvailable) return false;
            var hString = IntPtr.Zero;
            try
            {
                if (!string.IsNullOrWhiteSpace(mmDeviceId))
                {
                    var full = MmDevApiToken + mmDeviceId + (flow == EDataFlow.Render ? RenderInterface : CaptureInterface);
                    if (WindowsCreateString(full, (uint)full.Length, out hString) != 0) return false;
                }

                var ok = true;
                foreach (var role in new[] { ERole.Console, ERole.Multimedia })
                {
                    var hr = _new != null
                        ? _new.SetPersistedDefaultAudioEndpoint((uint)processId, flow, role, hString)
                        : _old!.SetPersistedDefaultAudioEndpoint((uint)processId, flow, role, hString);
                    ok &= hr >= 0;
                }
                return ok;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (hString != IntPtr.Zero) WindowsDeleteString(hString);
            }
        }

        /// <summary>Returns the MMDevice id an app is pinned to, or null when it follows the default.</summary>
        public string? GetAppOutput(int processId)
        {
            if (!IsAvailable) return null;
            try
            {
                var hr = _new != null
                    ? _new.GetPersistedDefaultAudioEndpoint((uint)processId, EDataFlow.Render, ERole.Multimedia, out var h)
                    : _old!.GetPersistedDefaultAudioEndpoint((uint)processId, EDataFlow.Render, ERole.Multimedia, out h);
                if (hr < 0 || h == IntPtr.Zero) return null;
                try
                {
                    var raw = Marshal.PtrToStringUni(WindowsGetStringRawBuffer(h, out var len), (int)len);
                    return Unpack(raw);
                }
                finally { WindowsDeleteString(h); }
            }
            catch { return null; }
        }

        /// <summary>Wipes every per-app device choice on this PC (same as "Reset" in Windows Settings).</summary>
        public bool ClearAll()
        {
            if (!IsAvailable) return false;
            try
            {
                return (_new != null ? _new.ClearAllPersistedApplicationDefaultEndpoints()
                                     : _old!.ClearAllPersistedApplicationDefaultEndpoints()) >= 0;
            }
            catch { return false; }
        }

        private static string? Unpack(string? full)
        {
            if (string.IsNullOrEmpty(full)) return null;
            var s = full;
            if (s.StartsWith(MmDevApiToken, StringComparison.OrdinalIgnoreCase)) s = s.Substring(MmDevApiToken.Length);
            if (s.EndsWith(RenderInterface, StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - RenderInterface.Length);
            if (s.EndsWith(CaptureInterface, StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - CaptureInterface.Length);
            return s;
        }

        [DllImport("combase.dll")]
        private static extern int RoGetActivationFactory(IntPtr activatableClassId, ref Guid iid, out IntPtr factory);

        [DllImport("combase.dll", CharSet = CharSet.Unicode)]
        private static extern int WindowsCreateString(string sourceString, uint length, out IntPtr hstring);

        [DllImport("combase.dll")]
        private static extern int WindowsDeleteString(IntPtr hstring);

        [DllImport("combase.dll")]
        private static extern IntPtr WindowsGetStringRawBuffer(IntPtr hstring, out uint length);
    }
}
