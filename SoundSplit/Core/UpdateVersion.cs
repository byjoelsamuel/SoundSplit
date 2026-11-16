using System;
using System.Linq;

namespace SoundSplit.Core
{
    /// <summary>Release-tag comparison ("v1.10.0" vs "1.9.0"). Kept free of I/O so it can be tested.</summary>
    internal static class UpdateVersion
    {
        public static string Clean(string tag) => tag.Trim().TrimStart('v', 'V');

        private static int[]? Parts(string v)
        {
            var core = Clean(v).Split('-')[0].Split('.');
            var parts = new int[core.Length];
            for (var i = 0; i < core.Length; i++)
                if (!int.TryParse(core[i], out parts[i])) return null;
            return parts;
        }

        /// <summary>True only if <paramref name="candidate"/> is strictly newer than <paramref name="current"/>.</summary>
        public static bool IsNewer(string candidate, string current)
        {
            var a = Parts(candidate);
            var b = Parts(current);
            if (a == null || b == null) return false;
            for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                var x = i < a.Length ? a[i] : 0;
                var y = i < b.Length ? b[i] : 0;
                if (x != y) return x > y;
            }
            return false;
        }
    }
}
