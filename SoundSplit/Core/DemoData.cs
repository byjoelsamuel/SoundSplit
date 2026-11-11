using System.Collections.Generic;
using NAudio.CoreAudioApi;

namespace SoundSplit.Core
{
    /// <summary>
    /// Fake devices and apps for "SoundSplit.exe --demo" (screenshots / trying the UI on a PC
    /// without speakers). Nothing is routed in demo mode.
    /// </summary>
    internal static class DemoData
    {
        public static readonly Dictionary<int, string> Processes = new()
        {
            [9001] = "spotify.exe",
            [9002] = "chrome.exe",
            [9003] = "discord.exe",
            [9004] = "valorant.exe",
        };

        public static List<AudioDevice> Devices(DataFlow flow) => flow == DataFlow.Render
            ? new()
            {
                new("demo-spk", "Speakers (Realtek Audio)", true, DeviceKind.Speaker, false),
                new("demo-iem", "USB DAC (IEM Headphones)", false, DeviceKind.Headphones, false),
                new("demo-bt", "JBL Flip (Bluetooth)", false, DeviceKind.Bluetooth, false),
                new("demo-hdmi", "LG Monitor (HDMI)", false, DeviceKind.Display, false),
            }
            : new()
            {
                new("demo-mic", "Microphone Array", true, DeviceKind.Microphone, true),
                new("demo-usbmic", "USB Microphone", false, DeviceKind.Microphone, true),
            };

        public static List<SessionSnapshot> Sessions() => new()
        {
            new(9001, "demo-iem", "USB DAC (IEM Headphones)", true, 0.8f, false),
            new(9002, "demo-spk", "Speakers (Realtek Audio)", true, 0.65f, false),
            new(9003, "demo-spk", "Speakers (Realtek Audio)", false, 1f, false),
            new(9004, "demo-spk", "Speakers (Realtek Audio)", true, 0.4f, true),
        };
    }
}
