using System.Windows.Media;
using SoundSplit.Core;

namespace SoundSplit.ViewModels
{
    /// <summary>Looks up the vector icons defined in Themes/Icons.xaml.</summary>
    internal static class Glyphs
    {
        public static Geometry? Get(string key) =>
            System.Windows.Application.Current?.TryFindResource(key) as Geometry;

        public static Geometry? Default => Get("I.Auto");
        public static Geometry? Other => Get("I.Volume");

        public static Geometry? For(DeviceKind kind) => Get(kind switch
        {
            DeviceKind.Speaker => "I.Speaker",
            DeviceKind.Headphones => "I.Headphones",
            DeviceKind.Display => "I.Monitor",
            DeviceKind.Bluetooth => "I.Bluetooth",
            DeviceKind.Microphone => "I.Mic",
            _ => "I.Volume",
        });
    }

    /// <summary>One entry in an app's "play on…" dropdown.</summary>
    public sealed class DeviceOption : Observable
    {
        public DeviceOption(string id, string label, Geometry? glyph, bool missing = false)
        {
            Id = id; _label = label; _glyph = glyph; _missing = missing;
        }

        public string Id { get; }
        private string _label; public string Label { get => _label; set => Set(ref _label, value); }
        private Geometry? _glyph; public Geometry? Glyph { get => _glyph; set => Set(ref _glyph, value); }
        private bool _missing; public bool IsMissing { get => _missing; set => Set(ref _missing, value); }
        public override string ToString() => Label;
    }

    /// <summary>A speaker/headphone card in the "Your devices" column.</summary>
    public sealed class DeviceItem : Observable
    {
        public DeviceItem(string id) => Id = id;
        public string Id { get; }
        private string _name = ""; public string Name { get => _name; set => Set(ref _name, value); }
        private Geometry? _glyph; public Geometry? Glyph { get => _glyph; set => Set(ref _glyph, value); }
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
