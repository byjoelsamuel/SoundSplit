using System;
using Microsoft.Win32;

namespace SoundSplit.Core
{
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
