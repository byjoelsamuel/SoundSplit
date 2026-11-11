using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace SoundSplit.Core
{
    /// <summary>One app's choice. Key in <see cref="AppSettings.Rules"/> is the exe name, e.g. "spotify.exe".</summary>
    internal sealed class RouteRule
    {
        public string DisplayName { get; set; } = "";
        public string? ExePath { get; set; }
        public string? OutputDeviceId { get; set; }
        public string? OutputDeviceName { get; set; }
        public string? InputDeviceId { get; set; }
        public string? InputDeviceName { get; set; }
        /// <summary>Keep showing this app in the list even when it is closed.</summary>
        public bool Pinned { get; set; }

        public bool HasRouting => !string.IsNullOrEmpty(OutputDeviceId) || !string.IsNullOrEmpty(InputDeviceId);
    }

    internal sealed class AppSettings
    {
        public bool Enabled { get; set; } = true;
        public string Theme { get; set; } = "Light";
        public bool CloseToTray { get; set; } = true;
        public bool ShowMicPicker { get; set; }
        public bool SeenTutorial { get; set; }
        public Dictionary<string, RouteRule> Rules { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Demo mode: keep changes in memory only.</summary>
        [JsonIgnore] public bool DoNotSave { get; set; }

        private static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SoundSplit");
        private static string FilePath => Path.Combine(Folder, "settings.json");
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Json);
                    if (s != null)
                    {
                        s.Rules = new Dictionary<string, RouteRule>(s.Rules ?? new(), StringComparer.OrdinalIgnoreCase);
                        return s;
                    }
                }
            }
            catch { /* corrupt file: start fresh */ }
            return new AppSettings();
        }

        public void Save()
        {
            if (DoNotSave) return;
            try
            {
                Directory.CreateDirectory(Folder);
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
                File.Move(tmp, FilePath, true);
            }
            catch { /* disk full / locked: not fatal */ }
        }
    }

    /// <summary>"Start with Windows" via the per-user Run key (no admin needed).</summary>
    internal static class Startup
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "SoundSplit";

        public static bool IsEnabled
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                    return key?.GetValue(ValueName) is string;
                }
                catch { return false; }
            }
        }

        public static void Set(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey);
                if (enable)
                {
                    var exe = Environment.ProcessPath ?? "";
                    key.SetValue(ValueName, $"\"{exe}\" --tray");
                }
                else key.DeleteValue(ValueName, false);
            }
            catch { }
        }
    }
}
