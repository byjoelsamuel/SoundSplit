using System;
using System.IO;
using SoundSplit.Core;
using Xunit;

namespace SoundSplit.Tests
{
    public class SettingsTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "soundsplit-tests-" + Guid.NewGuid().ToString("N"));

        public SettingsTests() => AppSettings.FolderOverride = _dir;

        public void Dispose()
        {
            AppSettings.FolderOverride = null;
            try { Directory.Delete(_dir, true); } catch { }
        }

        [Fact]
        public void Rules_survive_a_save_and_load()
        {
            var s = new AppSettings { Enabled = false, Theme = "Midnight", Minimal = true };
            s.Rules["spotify.exe"] = new RouteRule { DisplayName = "Spotify", OutputDeviceId = "dac", OutputDeviceName = "DAC" };
            s.Save();

            var loaded = AppSettings.Load();
            Assert.False(loaded.Enabled);
            Assert.Equal("Midnight", loaded.Theme);
            Assert.True(loaded.Minimal);
            Assert.Equal("dac", loaded.Rules["spotify.exe"].OutputDeviceId);
        }

        // Fault: JSON loading drops the case-insensitive comparer, so "Spotify.exe" and
        // "spotify.exe" become two rules and the saved one is never found.
        [Fact]
        public void Rule_lookup_ignores_case_after_loading()
        {
            var s = new AppSettings();
            s.Rules["Spotify.exe"] = new RouteRule { OutputDeviceId = "dac" };
            s.Save();
            Assert.True(AppSettings.Load().Rules.ContainsKey("spotify.exe"));
        }

        [Fact]
        public void A_corrupt_file_falls_back_to_defaults()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "settings.json"), "{ not json");
            var s = AppSettings.Load();
            Assert.True(s.Enabled);
            Assert.Equal("Light", s.Theme);
            Assert.Empty(s.Rules);
        }

        // Fault: --demo mode overwriting the user's real settings.
        [Fact]
        public void Demo_settings_are_never_written()
        {
            new AppSettings { DoNotSave = true, Theme = "Classic" }.Save();
            Assert.False(File.Exists(Path.Combine(_dir, "settings.json")));
        }
    }
}
