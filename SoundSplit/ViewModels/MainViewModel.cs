using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using SoundSplit.Core;

namespace SoundSplit.ViewModels
{
    public sealed class MainViewModel : Observable, IDisposable
    {
        private readonly AppSettings _settings;
        private readonly AudioDevices _audio = new();
        private readonly Router _router;
        private readonly DispatcherTimer _timer;
        private readonly DispatcherTimer _toastTimer;
        private readonly int _myPid = Environment.ProcessId;
        private readonly bool _demo;
        private string _outSignature = "", _inSignature = "";

        public ObservableCollection<AppItem> Apps { get; } = new();
        public ObservableCollection<DeviceItem> OutputDevices { get; } = new();
        public ObservableCollection<DeviceOption> OutputOptions { get; } = new();
        public ObservableCollection<DeviceOption> InputOptions { get; } = new();
        public ObservableCollection<PickerItem> PickerApps { get; } = new();

        public ICommand ToggleEnabledCommand { get; }
        public ICommand TestDeviceCommand { get; }
        public ICommand OpenAddAppCommand { get; }
        public ICommand CloseOverlaysCommand { get; }
        public ICommand AddPickedAppCommand { get; }
        public ICommand BrowseExeCommand { get; }
        public ICommand RemoveAppCommand { get; }
        public ICommand ResetAppCommand { get; }
        public ICommand ToggleMuteCommand { get; }
        public ICommand AskResetAllCommand { get; }
        public ICommand ConfirmResetAllCommand { get; }
        public ICommand ShowTutorialCommand { get; }
        public ICommand OpenWindowsSoundSettingsCommand { get; }
        public ICommand SetThemeCommand { get; }
        public ICommand ShowSettingsCommand { get; }

        internal MainViewModel(AppSettings settings, bool demo = false)
        {
            _settings = settings;
            _demo = demo;
            var policy = new AudioPolicyConfig();
            _router = new Router(policy);
            IsSupported = demo || policy.IsAvailable;
            SupportMessage = policy.Error ?? "";

            ToggleEnabledCommand = new RelayCommand(() => IsEnabled = !IsEnabled);
            TestDeviceCommand = new RelayCommand(p => { if (p is string id) { _ = TestTone.PlayAsync(id); Toast("Playing a test chime… listen for it!"); } });
            OpenAddAppCommand = new RelayCommand(OpenAddApp);
            CloseOverlaysCommand = new RelayCommand(() => { ShowAddApp = false; ShowTutorial = false; ShowResetConfirm = false; ShowSettings = false; });
            AddPickedAppCommand = new RelayCommand(p => { if (p is PickerItem pi) AddApp(pi.Key, pi.DisplayName, pi.ExePath); });
            BrowseExeCommand = new RelayCommand(BrowseExe);
            RemoveAppCommand = new RelayCommand(p => { if (p is AppItem a) RemoveApp(a); });
            ResetAppCommand = new RelayCommand(p => { if (p is AppItem a) { a.OutputId = ""; a.InputId = ""; } });
            ToggleMuteCommand = new RelayCommand(p => { if (p is AppItem a) a.IsMuted = !a.IsMuted; });
            AskResetAllCommand = new RelayCommand(() => { ShowSettings = false; ShowResetConfirm = true; });
            ConfirmResetAllCommand = new RelayCommand(ResetAll);
            ShowTutorialCommand = new RelayCommand(() => { ShowSettings = false; ShowTutorial = true; });
            ShowSettingsCommand = new RelayCommand(() => ShowSettings = !ShowSettings);
            OpenWindowsSoundSettingsCommand = new RelayCommand(() =>
            {
                try { Process.Start(new ProcessStartInfo("ms-settings:apps-volume") { UseShellExecute = true }); }
                catch { try { Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true }); } catch { } }
            });
            SetThemeCommand = new RelayCommand(p => { if (p is string t) Theme = t; });

            _isEnabled = settings.Enabled;
            _closeToTray = settings.CloseToTray;
            _showMicPicker = settings.ShowMicPicker;
            _startWithWindows = Startup.IsEnabled;
            _showTutorial = !settings.SeenTutorial;

            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); ToastText = ""; };

            Refresh();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            _timer.Tick += (_, _) => Refresh();
            _timer.Start();
        }

        // ------------------------------------------------------------------ state

        public bool IsSupported { get; }
        public string SupportMessage { get; }

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (!Set(ref _isEnabled, value)) return;
                _settings.Enabled = value;
                _settings.Save();
                Refresh();
                Toast(value ? "SoundSplit is on. Your picks are live." : "SoundSplit is off. Every app follows Windows again.");
                EnabledChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler? EnabledChanged;

        public string Theme
        {
            get => _settings.Theme;
            set
            {
                if (_settings.Theme == value) return;
                _settings.Theme = value;
                _settings.Save();
                Raise();
                ThemeManager.Apply(value);
            }
        }

        private bool _closeToTray;
        public bool CloseToTray
        {
            get => _closeToTray;
            set { if (Set(ref _closeToTray, value)) { _settings.CloseToTray = value; _settings.Save(); } }
        }

        private bool _startWithWindows;
        public bool StartWithWindows
        {
            get => _startWithWindows;
            set { if (Set(ref _startWithWindows, value)) Startup.Set(value); }
        }

        private bool _showMicPicker;
        public bool ShowMicPicker
        {
            get => _showMicPicker;
            set { if (Set(ref _showMicPicker, value)) { _settings.ShowMicPicker = value; _settings.Save(); } }
        }

        private bool _showTutorial;
        public bool ShowTutorial
        {
            get => _showTutorial;
            set
            {
                if (!Set(ref _showTutorial, value)) return;
                if (!value && !_settings.SeenTutorial) { _settings.SeenTutorial = true; _settings.Save(); }
            }
        }

        private bool _showAddApp; public bool ShowAddApp { get => _showAddApp; set => Set(ref _showAddApp, value); }
        private bool _showResetConfirm; public bool ShowResetConfirm { get => _showResetConfirm; set => Set(ref _showResetConfirm, value); }
        private bool _showSettings; public bool ShowSettings { get => _showSettings; set => Set(ref _showSettings, value); }

        private string _toastText = "";
        public string ToastText { get => _toastText; private set { if (Set(ref _toastText, value)) Raise(nameof(HasToast)); } }
        public bool HasToast => !string.IsNullOrEmpty(_toastText);

        private string _summary = "";
        public string Summary { get => _summary; private set => Set(ref _summary, value); }

        private bool _hasApps; public bool HasApps { get => _hasApps; private set => Set(ref _hasApps, value); }

        public void Toast(string text)
        {
            ToastText = text;
            _toastTimer.Stop();
            _toastTimer.Start();
        }

        // ------------------------------------------------------------------ refresh loop

        public void Refresh()
        {
            ProcessInfo.TakeSnapshot();
            var outs = _demo ? DemoData.Devices(DataFlow.Render) : _audio.GetDevices(DataFlow.Render);
            var ins = _demo ? DemoData.Devices(DataFlow.Capture) : _audio.GetDevices(DataFlow.Capture);
            UpdateDevices(outs, ins);

            var sessions = (_demo ? DemoData.Sessions() : _audio.GetSessions()).Where(s => s.ProcessId != _myPid).ToList();
            var groups = new Dictionary<string, (string? Path, List<SessionSnapshot> Sessions)>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in sessions)
            {
                var path = _demo ? null : ProcessInfo.GetPath(s.ProcessId);
                string key;
                if (_demo) key = DemoData.Processes[s.ProcessId];
                else if (path != null) key = ProcessInfo.KeyFromPath(path);
                else
                {
                    try { using var p = Process.GetProcessById(s.ProcessId); key = (p.ProcessName + ".exe").ToLowerInvariant(); }
                    catch { continue; }
                }
                if (!groups.TryGetValue(key, out var g)) groups[key] = g = (path, new List<SessionSnapshot>());
                g.Sessions.Add(s);
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Apps currently using audio.
            foreach (var (key, g) in groups)
            {
                seen.Add(key);
                var item = GetOrCreate(key, g.Path);
                item.Pids.Clear();
                item.Pids.AddRange(g.Sessions.Select(s => s.ProcessId).Distinct());
                if (!_demo) foreach (var pid in ProcessInfo.FindPidsByKey(key)) if (!item.Pids.Contains(pid)) item.Pids.Add(pid);
                item.IsRunning = true;
                item.IsPlaying = g.Sessions.Any(s => s.IsActive);
                var active = g.Sessions.Where(s => s.IsActive).ToList();
                var where = (active.Count > 0 ? active : g.Sessions).Select(s => s.DeviceName).Distinct().ToList();
                item.Status = (item.IsPlaying ? "Playing on " : "Quiet · on ") + string.Join(" + ", where);
                var loudest = (active.Count > 0 ? active : g.Sessions).OrderByDescending(s => s.Volume).First();
                item.SyncVolume(loudest.Volume, g.Sessions.All(s => s.IsMuted));
            }

            // Saved apps that aren't making sound right now.
            foreach (var (key, rule) in _settings.Rules)
            {
                if (seen.Contains(key)) continue;
                seen.Add(key);
                var item = GetOrCreate(key, rule.ExePath, rule.DisplayName);
                item.Pids.Clear();
                item.Pids.AddRange(ProcessInfo.FindPidsByKey(key));
                item.IsRunning = item.Pids.Count > 0;
                item.IsPlaying = false;
                item.Status = item.IsRunning ? "Open · not playing anything yet" : "Not running · will switch when you open it";
            }

            // Drop cards for apps that closed and have nothing saved.
            foreach (var stale in Apps.Where(a => !seen.Contains(a.Key)).ToList()) Apps.Remove(stale);

            foreach (var app in Apps)
            {
                _settings.Rules.TryGetValue(app.Key, out var r);
                app.IsPinned = r?.Pinned == true;
                app.IsRouted = r?.HasRouting == true;
            }
            HasApps = Apps.Count > 0;

            if (!_demo) _router.Sync(_settings.Rules, IsEnabled,
                outs.Select(d => d.Id).ToHashSet(), ins.Select(d => d.Id).ToHashSet());

            // Device cards: how many apps are sent to each.
            foreach (var d in OutputDevices)
            {
                var n = _settings.Rules.Values.Count(r => r.OutputDeviceId == d.Id);
                d.Detail = (d.IsDefault ? "Windows default" : "Extra device") + (n > 0 ? $" · {n} app{(n == 1 ? "" : "s")} sent here" : "");
            }

            var routed = _settings.Rules.Values.Count(r => !string.IsNullOrEmpty(r.OutputDeviceId) || !string.IsNullOrEmpty(r.InputDeviceId));
            Summary = !IsSupported ? "Not supported on this Windows version"
                    : !IsEnabled ? "Off · every app follows Windows"
                    : routed == 0 ? "On · pick an app below to send it somewhere else"
                    : $"On · {routed} app{(routed == 1 ? "" : "s")} on a separate device";
        }

        private AppItem GetOrCreate(string key, string? path, string? savedName = null)
        {
            var item = Apps.FirstOrDefault(a => a.Key == key);
            if (item != null)
            {
                if (item.ExePath == null && path != null)
                {
                    item.ExePath = path;
                    item.Icon = ProcessInfo.Icon(path);
                    item.DisplayName = ProcessInfo.FriendlyName(path, key);
                }
                return item;
            }

            var name = !string.IsNullOrWhiteSpace(savedName) ? savedName! : ProcessInfo.FriendlyName(path, key);
            item = new AppItem(this, key, name, path, ProcessInfo.Icon(path));
            if (_settings.Rules.TryGetValue(key, out var rule)) item.LoadRoute(rule.OutputDeviceId, rule.InputDeviceId);

            // Keep the list alphabetical without reshuffling existing cards.
            var idx = 0;
            while (idx < Apps.Count && string.Compare(Apps[idx].DisplayName, name, StringComparison.CurrentCultureIgnoreCase) < 0) idx++;
            Apps.Insert(idx, item);
            return item;
        }

        private void UpdateDevices(List<AudioDevice> outs, List<AudioDevice> ins)
        {
            // Device cards
            foreach (var gone in OutputDevices.Where(d => outs.All(o => o.Id != d.Id)).ToList()) OutputDevices.Remove(gone);
            for (var i = 0; i < outs.Count; i++)
            {
                var o = outs[i];
                var item = OutputDevices.FirstOrDefault(d => d.Id == o.Id);
                if (item == null) { item = new DeviceItem(o.Id); OutputDevices.Insert(Math.Min(i, OutputDevices.Count), item); }
                item.Name = o.Name; item.IsDefault = o.IsDefault; item.Glyph = Glyphs.For(o.Kind);
            }

            // Dropdown options. Only rebuilt when something actually changed so open dropdowns don't flicker.
            var outSig = string.Join("|", outs.Select(o => o.Id + o.Name + o.IsDefault)) + "#" + string.Join("|", _settings.Rules.Values.Select(r => r.OutputDeviceId));
            var inSig = string.Join("|", ins.Select(o => o.Id + o.Name + o.IsDefault)) + "#" + string.Join("|", _settings.Rules.Values.Select(r => r.InputDeviceId));
            if (outSig != _outSignature)
            {
                _outSignature = outSig;
                Rebuild(OutputOptions, outs, "Default (follow Windows)", r => (r.OutputDeviceId, r.OutputDeviceName));
            }
            if (inSig != _inSignature)
            {
                _inSignature = inSig;
                Rebuild(InputOptions, ins, "Default mic", r => (r.InputDeviceId, r.InputDeviceName));
            }
        }

        private void Rebuild(ObservableCollection<DeviceOption> options, List<AudioDevice> devices, string defaultLabel,
                             Func<RouteRule, (string? Id, string? Name)> pick)
        {
            var wanted = new List<DeviceOption> { new("", defaultLabel, Glyphs.Default) };
            wanted.AddRange(devices.Select(d => new DeviceOption(d.Id, d.Name, Glyphs.For(d.Kind))));
            // Saved devices that are unplugged right now still need to show up in the dropdown.
            foreach (var r in _settings.Rules.Values)
            {
                var (id, name) = pick(r);
                if (!string.IsNullOrEmpty(id) && wanted.All(w => w.Id != id))
                    wanted.Add(new DeviceOption(id!, (name ?? "Saved device") + " (unplugged)", Glyphs.Other, true));
            }

            // Update in place: same ids keep the same objects so ComboBox selections survive.
            foreach (var old in options.Where(o => wanted.All(w => w.Id != o.Id)).ToList()) options.Remove(old);
            for (var i = 0; i < wanted.Count; i++)
            {
                var w = wanted[i];
                var existing = options.FirstOrDefault(o => o.Id == w.Id);
                if (existing == null) options.Insert(Math.Min(i, options.Count), w);
                else
                {
                    existing.Label = w.Label; existing.Glyph = w.Glyph; existing.IsMissing = w.IsMissing;
                    var at = options.IndexOf(existing);
                    if (at != i && i < options.Count) options.Move(at, i);
                }
            }
            foreach (var a in Apps) a.RefreshBindings();
        }

        // ------------------------------------------------------------------ user actions

        internal void OnRouteChanged(AppItem app, bool input, string deviceId)
        {
            if (!_settings.Rules.TryGetValue(app.Key, out var rule))
            {
                rule = new RouteRule { DisplayName = app.DisplayName, ExePath = app.ExePath };
                _settings.Rules[app.Key] = rule;
            }
            rule.DisplayName = app.DisplayName;
            rule.ExePath ??= app.ExePath;

            var options = input ? InputOptions : OutputOptions;
            var label = options.FirstOrDefault(o => o.Id == deviceId)?.Label ?? "";
            if (input) { rule.InputDeviceId = deviceId == "" ? null : deviceId; rule.InputDeviceName = deviceId == "" ? null : label; }
            else { rule.OutputDeviceId = deviceId == "" ? null : deviceId; rule.OutputDeviceName = deviceId == "" ? null : label; }

            if (!rule.HasRouting && !rule.Pinned) _settings.Rules.Remove(app.Key);
            _settings.Save();

            if (deviceId == "" && !_demo) _router.ResetApp(app.Key);
            Refresh();

            if (!IsEnabled) Toast("Saved. Turn SoundSplit on to make it happen.");
            else if (deviceId == "") Toast($"{app.DisplayName} follows Windows again.");
            else Toast($"{app.DisplayName} {(input ? "mic" : "sound")} → {label}. No change? Pause & play it once.");
        }

        internal void OnVolumeChanged(AppItem app, float? volume, bool? mute)
        {
            if (app.Pids.Count > 0 && !_demo) _audio.SetVolume(app.Pids, volume, mute);
        }

        private void OpenAddApp()
        {
            PickerApps.Clear();
            var listed = new HashSet<string>(Apps.Select(a => a.Key), StringComparer.OrdinalIgnoreCase);
            var found = new Dictionary<string, PickerItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in Process.GetProcesses())
            {
                using (p)
                {
                    try
                    {
                        if (p.Id == _myPid || p.MainWindowHandle == IntPtr.Zero || string.IsNullOrWhiteSpace(p.MainWindowTitle)) continue;
                        var path = ProcessInfo.GetPath(p.Id);
                        if (path == null) continue;
                        var key = ProcessInfo.KeyFromPath(path);
                        if (listed.Contains(key) || found.ContainsKey(key) || key == "explorer.exe" || key == "applicationframehost.exe") continue;
                        found[key] = new PickerItem { Key = key, ExePath = path, DisplayName = ProcessInfo.FriendlyName(path, key), Icon = ProcessInfo.Icon(path) };
                    }
                    catch { }
                }
            }
            foreach (var item in found.Values.OrderBy(i => i.DisplayName, StringComparer.CurrentCultureIgnoreCase)) PickerApps.Add(item);
            ShowSettings = false;
            ShowAddApp = true;
        }

        private void BrowseExe()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pick an app (.exe)",
                Filter = "Apps (*.exe)|*.exe",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            };
            if (dlg.ShowDialog() != true) return;
            var key = ProcessInfo.KeyFromPath(dlg.FileName);
            AddApp(key, ProcessInfo.FriendlyName(dlg.FileName, key), dlg.FileName);
        }

        private void AddApp(string key, string name, string? path)
        {
            if (!_settings.Rules.TryGetValue(key, out var rule))
                _settings.Rules[key] = rule = new RouteRule { DisplayName = name, ExePath = path };
            rule.Pinned = true;
            _settings.Save();
            ShowAddApp = false;
            Refresh();
            Toast($"{name} added. Now pick where it should play.");
        }

        private void RemoveApp(AppItem app)
        {
            _settings.Rules.Remove(app.Key);
            _settings.Save();
            _router.ResetApp(app.Key);
            app.LoadRoute("", "");
            Refresh();
            Toast($"{app.DisplayName} removed and back on the default device.");
        }

        private void ResetAll()
        {
            ShowResetConfirm = false;
            _settings.Rules.Clear();
            _settings.Save();
            var ok = _router.ResetEverything();
            foreach (var a in Apps) a.LoadRoute("", "");
            _outSignature = _inSignature = "";
            Refresh();
            Toast(ok ? "Everything reset. All apps follow Windows again." : "Couldn't reach Windows' audio settings.");
        }

        public void Dispose()
        {
            _timer.Stop();
            _audio.Dispose();
        }
    }
}
