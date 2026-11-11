# SoundSplit (source)

WPF / .NET 8 source for SoundSplit. See the [main README](../README.md) for usage and build instructions.

| Path | Purpose |
| --- | --- |
| `Core/AudioPolicyConfig.cs` | Interop with Windows' per-app default endpoint API |
| `Core/EndpointIds.cs` | Device id ↔ endpoint path conversion |
| `Core/AudioDevices.cs` | Device enumeration, audio sessions, per-app volume |
| `Core/Router.cs` | Applies saved rules to running processes |
| `Core/ProcessInfo.cs` | Process paths, names, icons |
| `Core/Settings.cs` | `settings.json` |
| `Core/Startup.cs` | Start with Windows (per-user Run key) |
| `Core/TestTone.cs` | Device test chime |
| `Core/DemoData.cs` | Sample data for `--demo` |
| `ViewModels/` | UI state and commands |
| `Themes/` | `Light`, `Midnight`, `Aero`, `Classic` palettes, line icons (`Icons.xaml`) and shared control styles |
| `IconBox.cs` | Renders the vector line icons |
| `MainWindow.xaml` | Main UI |
| `App.xaml.cs` | Startup, tray icon, single instance |

Nunito is included under the SIL Open Font License (`Assets/Fonts/OFL.txt`).
