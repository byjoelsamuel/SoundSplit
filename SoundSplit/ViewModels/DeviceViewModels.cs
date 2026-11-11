using SoundSplit.Core;

namespace SoundSplit.ViewModels
{
    internal static class Glyphs
    {
        // Segoe MDL2 Assets / Segoe Fluent Icons code points (built into Windows 10/11)
        public const string Speaker = "";
        public const string Headphones = "";
        public const string Display = "";
        public const string Bluetooth = "";
        public const string Mic = "";
        public const string Default = "";   // home
        public const string Other = "";     // volume

        public static string For(DeviceKind kind) => kind switch
        {
            DeviceKind.Speaker => Speaker,
            DeviceKind.Headphones => Headphones,
            DeviceKind.Display => Display,
            DeviceKind.Bluetooth => Bluetooth,
            DeviceKind.Microphone => Mic,
            _ => Other,
        };
    }

    /// <summary>One entry in an app's "play on…" dropdown.</summary>
    public sealed class DeviceOption : Observable
    {
        public DeviceOption(string id, string label, string glyph, bool missing = false)
        {
            Id = id; _label = label; _glyph = glyph; _missing = missing;
        }

        public string Id { get; }
        private string _label; public string Label { get => _label; set => Set(ref _label, value); }
        private string _glyph; public string Glyph { get => _glyph; set => Set(ref _glyph, value); }
        private bool _missing; public bool IsMissing { get => _missing; set => Set(ref _missing, value); }
        public override string ToString() => Label;
    }

    /// <summary>A speaker/headphone card in the "Your devices" column.</summary>
    public sealed class DeviceItem : Observable
    {
        public DeviceItem(string id) => Id = id;
        public string Id { get; }
        private string _name = ""; public string Name { get => _name; set => Set(ref _name, value); }
        private string _glyph = Glyphs.Speaker; public string Glyph { get => _glyph; set => Set(ref _glyph, value); }
        private bool _isDefault; public bool IsDefault { get => _isDefault; set => Set(ref _isDefault, value); }
        private string _detail = ""; public string Detail { get => _detail; set => Set(ref _detail, value); }
    }

    /// <summary>A running app offered in the "Add an app" picker.</summary>
    public sealed class PickerItem
    {
        public string Key { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string? ExePath { get; init; }
        public System.Windows.Media.ImageSource? Icon { get; init; }
        public string Initial => DisplayName.Length > 0 ? DisplayName.Substring(0, 1).ToUpperInvariant() : "?";
    }
}
