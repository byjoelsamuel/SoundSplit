using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace SoundSplit.ViewModels
{
    /// <summary>One app card: icon, name, where it's playing, its device picker and volume.</summary>
    public sealed class AppItem : Observable
    {
        private readonly MainViewModel _owner;
        private DateTime _lastUserVolume = DateTime.MinValue;

        internal AppItem(MainViewModel owner, string key, string displayName, string? exePath, ImageSource? icon)
        {
            _owner = owner;
            Key = key;
            _displayName = displayName;
            ExePath = exePath;
            _icon = icon;
        }

        public string Key { get; }
        public string? ExePath { get; set; }
        public List<int> Pids { get; } = new();

        private string _displayName;
        public string DisplayName { get => _displayName; set { if (Set(ref _displayName, value)) Raise(nameof(Initial)); } }
        public string Initial => DisplayName.Length > 0 ? DisplayName.Substring(0, 1).ToUpperInvariant() : "?";

        private ImageSource? _icon;
        public ImageSource? Icon { get => _icon; set { if (Set(ref _icon, value)) Raise(nameof(HasIcon)); } }
        public bool HasIcon => _icon != null;

        private bool _isPlaying; public bool IsPlaying { get => _isPlaying; set => Set(ref _isPlaying, value); }
        private bool _isRunning; public bool IsRunning { get => _isRunning; set => Set(ref _isRunning, value); }
        private bool _isPinned; public bool IsPinned { get => _isPinned; set => Set(ref _isPinned, value); }
        private string _status = ""; public string Status { get => _status; set => Set(ref _status, value); }
        private bool _isRouted; public bool IsRouted { get => _isRouted; set => Set(ref _isRouted, value); }

        // ----- Output / mic choice. "" means "Default" (follow Windows). -----
        private string _outputId = "";
        public string? OutputId
        {
            get => _outputId;
            set
            {
                // ComboBoxes briefly push null while their item list is being rebuilt: ignore that.
                if (value == null || value == _outputId) return;
                _outputId = value;
                Raise();
                _owner.OnRouteChanged(this, false, value);
            }
        }

        private string _inputId = "";
        public string? InputId
        {
            get => _inputId;
            set
            {
                if (value == null || value == _inputId) return;
                _inputId = value;
                Raise();
                _owner.OnRouteChanged(this, true, value);
            }
        }

        /// <summary>Set from saved rules without triggering a routing change.</summary>
        internal void LoadRoute(string? outputId, string? inputId)
        {
            _outputId = outputId ?? "";
            _inputId = inputId ?? "";
            Raise(nameof(OutputId));
            Raise(nameof(InputId));
        }

        internal void RefreshBindings()
        {
            Raise(nameof(OutputId));
            Raise(nameof(InputId));
        }

        // ----- Volume (same as the Windows Volume Mixer slider for this app) -----
        private double _volume = 100;
        public double Volume
        {
            get => _volume;
            set
            {
                value = Math.Round(Math.Clamp(value, 0, 100));
                if (!Set(ref _volume, value)) return;
                _lastUserVolume = DateTime.UtcNow;
                _owner.OnVolumeChanged(this, (float)(value / 100.0), null);
            }
        }

        private bool _isMuted;
        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                if (!Set(ref _isMuted, value)) return;
                _lastUserVolume = DateTime.UtcNow;
                _owner.OnVolumeChanged(this, null, value);
            }
        }

        internal void SyncVolume(float volume, bool muted)
        {
            // Don't fight the user while they're dragging the slider.
            if ((DateTime.UtcNow - _lastUserVolume).TotalSeconds < 2) return;
            var v = Math.Round(volume * 100);
            if (Math.Abs(v - _volume) >= 1) { _volume = v; Raise(nameof(Volume)); }
            if (muted != _isMuted) { _isMuted = muted; Raise(nameof(IsMuted)); }
        }
    }
}
