using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SoundSplit.Core
{
    internal sealed record UpdateInfo(string Version, string DownloadUrl, string Notes);

    /// <summary>Checks GitHub Releases for a newer SoundSplit.exe and swaps it in.</summary>
    internal static class Updates
    {
        private const string Latest = "https://api.github.com/repos/byjoelsamuel/SoundSplit/releases/latest";
        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("SoundSplit-Windows");
            c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return c;
        }

        public static string CurrentVersion =>
            typeof(Updates).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

        /// <summary>The newer release, or null when up to date / offline. Never throws.</summary>
        public static async Task<UpdateInfo?> CheckAsync()
        {
            try
            {
                using var doc = JsonDocument.Parse(await Http.GetStringAsync(Latest));
                var root = doc.RootElement;
                var tag = root.GetProperty("tag_name").GetString() ?? "";
                if (!UpdateVersion.IsNewer(tag, CurrentVersion)) return null;
                var exe = root.GetProperty("assets").EnumerateArray()
                    .FirstOrDefault(a => string.Equals(a.GetProperty("name").GetString(), "SoundSplit.exe", StringComparison.OrdinalIgnoreCase));
                if (exe.ValueKind == JsonValueKind.Undefined) return null;
                return new UpdateInfo(UpdateVersion.Clean(tag), exe.GetProperty("browser_download_url").GetString() ?? "",
                    root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "");
            }
            catch { return null; }
        }

        /// <summary>
        /// Downloads the new exe next to a temp folder and starts a tiny script that waits for this
        /// process to exit, replaces the exe and starts it again. The caller then quits the app.
        /// </summary>
        public static async Task<bool> DownloadAndScheduleAsync(UpdateInfo info, IProgress<double> progress, CancellationToken ct = default)
        {
            var current = Environment.ProcessPath;
            if (string.IsNullOrEmpty(current)) return false;
            var dir = Path.Combine(Path.GetTempPath(), "SoundSplit-update");
            Directory.CreateDirectory(dir);
            var newExe = Path.Combine(dir, "SoundSplit.exe");

            using (var response = await Http.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? 0;
                await using var src = await response.Content.ReadAsStreamAsync(ct);
                await using var dst = File.Create(newExe);
                var buffer = new byte[81920];
                long done = 0;
                int n;
                while ((n = await src.ReadAsync(buffer, ct)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, n), ct);
                    done += n;
                    if (total > 0) progress.Report((double)done / total);
                }
            }

            var pid = Environment.ProcessId;
            var script = Path.Combine(dir, "apply-update.cmd");
            File.WriteAllText(script,
                "@echo off\r\n" +
                ":wait\r\n" +
                $"tasklist /FI \"PID eq {pid}\" | find \"{pid}\" >nul && (timeout /t 1 /nobreak >nul & goto wait)\r\n" +
                $"move /y \"{newExe}\" \"{current}\" >nul || (start \"\" \"https://github.com/byjoelsamuel/SoundSplit/releases/latest\" & exit /b 1)\r\n" +
                $"start \"\" \"{current}\"\r\n" +
                "del \"%~f0\"\r\n");
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
            });
            return true;
        }
    }
}
