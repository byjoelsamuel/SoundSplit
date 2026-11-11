using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.CoreAudioApi;

namespace SoundSplit.Core
{
    internal enum DeviceKind { Speaker, Headphones, Display, Bluetooth, Microphone, Other }

    internal sealed record AudioDevice(string Id, string Name, bool IsDefault, DeviceKind Kind, bool IsInput);

    internal sealed record SessionSnapshot(int ProcessId, string DeviceId, string DeviceName, bool IsActive, float Volume, bool IsMuted);

    /// <summary>Reads speakers / headphones / mics and the apps currently using them (Windows Core Audio).</summary>
    internal sealed class AudioDevices : IDisposable
    {
        private readonly MMDeviceEnumerator _enumerator = new();

        public List<AudioDevice> GetDevices(DataFlow flow)
        {
            var list = new List<AudioDevice>();
            string? defaultId = null;
            try
            {
                if (_enumerator.HasDefaultAudioEndpoint(flow, Role.Multimedia))
                {
                    using var def = _enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
                    defaultId = def.ID;
                }
            }
            catch { /* no default device */ }

            try
            {
                foreach (var d in _enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
                {
                    using (d)
                    {
                        string name;
                        try { name = d.FriendlyName; } catch { name = "Audio device"; }
                        list.Add(new AudioDevice(d.ID, name, d.ID == defaultId, Guess(name, flow), flow == DataFlow.Capture));
                    }
                }
            }
            catch { /* audio service restarting */ }

            return list.OrderByDescending(d => d.IsDefault).ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        /// <summary>Every audio session (app) on every active output device.</summary>
        public List<SessionSnapshot> GetSessions()
        {
            var result = new List<SessionSnapshot>();
            MMDeviceCollection devices;
            try { devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active); }
            catch { return result; }

            foreach (var device in devices)
            {
                using (device)
                {
                    try
                    {
                        string name;
                        try { name = device.FriendlyName; } catch { name = "Audio device"; }
                        var manager = device.AudioSessionManager;
                        manager.RefreshSessions();
                        var sessions = manager.Sessions;
                        for (var i = 0; i < sessions.Count; i++)
                        {
                            using var s = sessions[i];
                            try
                            {
                                if (s.IsSystemSoundsSession) continue;
                                var state = s.State;
                                if (state == NAudio.CoreAudioApi.Interfaces.AudioSessionState.AudioSessionStateExpired) continue;
                                var pid = (int)s.GetProcessID;
                                if (pid <= 0) continue;
                                result.Add(new SessionSnapshot(pid, device.ID, name,
                                    state == NAudio.CoreAudioApi.Interfaces.AudioSessionState.AudioSessionStateActive,
                                    s.SimpleAudioVolume.Volume, s.SimpleAudioVolume.Mute));
                            }
                            catch { /* session vanished mid-read */ }
                        }
                    }
                    catch { /* device vanished mid-read */ }
                }
            }
            return result;
        }

        /// <summary>Changes the per-app volume (the same slider as the Windows Volume Mixer).</summary>
        public void SetVolume(ICollection<int> processIds, float? volume, bool? mute)
        {
            try
            {
                foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                {
                    using (device)
                    {
                        try
                        {
                            var manager = device.AudioSessionManager;
                            manager.RefreshSessions();
                            var sessions = manager.Sessions;
                            for (var i = 0; i < sessions.Count; i++)
                            {
                                using var s = sessions[i];
                                try
                                {
                                    if (s.IsSystemSoundsSession || !processIds.Contains((int)s.GetProcessID)) continue;
                                    if (volume is float v) s.SimpleAudioVolume.Volume = Math.Clamp(v, 0f, 1f);
                                    if (mute is bool m) s.SimpleAudioVolume.Mute = m;
                                }
                                catch { }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private static DeviceKind Guess(string name, DataFlow flow)
        {
            var n = name.ToLowerInvariant();
            if (flow == DataFlow.Capture) return DeviceKind.Microphone;
            if (n.Contains("hands-free") || n.Contains("bluetooth") || n.Contains("buds") || n.Contains("airpods") || n.Contains("wh-") || n.Contains("wf-"))
                return DeviceKind.Bluetooth;
            if (n.Contains("headphone") || n.Contains("headset") || n.Contains("earphone") || n.Contains("iem") || n.Contains("dac") || n.Contains("usb audio"))
                return DeviceKind.Headphones;
            if (n.Contains("hdmi") || n.Contains("display") || n.Contains("nvidia") || n.Contains("amd high definition") || n.Contains("monitor") || n.Contains("tv"))
                return DeviceKind.Display;
            if (n.Contains("speaker") || n.Contains("realtek") || n.Contains("high definition audio"))
                return DeviceKind.Speaker;
            return DeviceKind.Other;
        }

        public void Dispose() => _enumerator.Dispose();
    }
}
