using System;
using System.Linq;
using System.Windows;

namespace SoundSplit
{
    /// <summary>Swaps the colour/shape palette at runtime. Every control uses DynamicResource, so it updates live.</summary>
    internal static class ThemeManager
    {
        public static readonly string[] Names = { "Aero", "Classic", "Midnight" };

        public static void Apply(string name)
        {
            if (!Names.Contains(name)) name = "Aero";
            var dict = new ResourceDictionary
            {
                Source = new Uri($"/SoundSplit;component/Themes/{name}.xaml", UriKind.Relative)
            };
            var merged = System.Windows.Application.Current.Resources.MergedDictionaries;
            // Slot 0 is always the active theme, slot 1 the control styles (see App.xaml).
            if (merged.Count > 0) merged[0] = dict;
            else merged.Add(dict);
        }
    }
}
