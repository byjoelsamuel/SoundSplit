using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SoundSplit.Core
{
    /// <summary>Who is process 1234? Gives back the exe path, a nice name and its icon (cached).</summary>
    internal static class ProcessInfo
    {
        private static readonly Dictionary<int, string?> PathCache = new();
        private static readonly Dictionary<string, ImageSource?> IconCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> NameCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Lower-case exe file name, e.g. "spotify.exe". Used as the key for saved rules.</summary>
        public static string KeyFromPath(string path) => Path.GetFileName(path).ToLowerInvariant();

        public static string? GetPath(int pid)
        {
            if (PathCache.TryGetValue(pid, out var cached)) return cached;
            string? path = null;
            var h = OpenProcess(ProcessQueryLimitedInformation, false, pid);
            if (h != IntPtr.Zero)
            {
                try
                {
                    var sb = new StringBuilder(1024);
                    var size = sb.Capacity;
                    if (QueryFullProcessImageName(h, 0, sb, ref size)) path = sb.ToString(0, size);
                }
                finally { CloseHandle(h); }
            }
            PathCache[pid] = path;
            return path;
        }

        public static bool IsAlive(int pid)
        {
            var h = OpenProcess(ProcessQueryLimitedInformation, false, pid);
            if (h == IntPtr.Zero) return false;
            try { return GetExitCodeProcess(h, out var code) && code == StillActive; }
            finally { CloseHandle(h); }
        }

        /// <summary>Drops cached paths for processes that are gone (PIDs get reused).</summary>
        public static void Prune(ICollection<int> alivePids)
        {
            var dead = new List<int>();
            foreach (var pid in PathCache.Keys) if (!alivePids.Contains(pid)) dead.Add(pid);
            foreach (var pid in dead) PathCache.Remove(pid);
        }

        private static Dictionary<string, List<int>> _byExe = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Takes one list of all running processes per refresh (cheaper than asking Windows
        /// again for every single app) and forgets cached paths of processes that exited.
        /// </summary>
        public static void TakeSnapshot()
        {
            var map = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            var alive = new HashSet<int>();
            try
            {
                foreach (var p in Process.GetProcesses())
                {
                    using (p)
                    {
                        try
                        {
                            alive.Add(p.Id);
                            var key = (p.ProcessName + ".exe").ToLowerInvariant();
                            if (!map.TryGetValue(key, out var list)) map[key] = list = new List<int>();
                            list.Add(p.Id);
                        }
                        catch { }
                    }
                }
            }
            catch { return; }
            _byExe = map;
            Prune(alive);
        }

        /// <summary>All running process ids for an exe name such as "chrome.exe" (from the last snapshot).</summary>
        public static List<int> FindPidsByKey(string exeKey) =>
            _byExe.TryGetValue(exeKey, out var list) ? new List<int>(list) : new List<int>();

        public static string FriendlyName(string? path, string fallbackKey)
        {
            var key = path ?? fallbackKey;
            if (NameCache.TryGetValue(key, out var n)) return n;
            string? name = null;
            if (path != null)
            {
                try
                {
                    var info = FileVersionInfo.GetVersionInfo(path);
                    name = !string.IsNullOrWhiteSpace(info.FileDescription) ? info.FileDescription
                         : !string.IsNullOrWhiteSpace(info.ProductName) ? info.ProductName : null;
                }
                catch { }
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                var raw = Path.GetFileNameWithoutExtension(fallbackKey);
                name = raw.Length > 0 ? char.ToUpperInvariant(raw[0]) + raw.Substring(1) : fallbackKey;
            }
            name = name!.Trim();
            NameCache[key] = name;
            return name;
        }

        public static ImageSource? Icon(string? path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (IconCache.TryGetValue(path, out var cached)) return cached;
            ImageSource? img = null;
            try
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (icon != null)
                {
                    var bmp = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    bmp.Freeze();
                    img = bmp;
                }
            }
            catch { }
            IconCache[path] = img;
            return img;
        }

        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const uint StillActive = 259;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder exeName, ref int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
