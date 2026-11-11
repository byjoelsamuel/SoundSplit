<img src="SoundSplit/Assets/logo.png" width="72" align="right" alt="" />

# SoundSplit

Per-app audio output for Windows 10/11. Route any app to its own speaker or headphones and leave everything else on the default device.

**[Download the latest release](https://github.com/byjoelsamuel/md-player/releases/latest)**

## Features

- Pick an output device (and optionally a mic) for each app
- Choices persist and re-apply when the app starts
- Per-app volume and mute
- Test chime to identify devices
- Tray icon, start with Windows
- Themes: Aero, Classic 98, Midnight

Routing is handled by Windows itself (the same mechanism as *Settings → Sound → Volume mixer*), so no drivers or virtual cables are needed and no latency is added.

## Usage

1. Run `SoundSplit.exe`. It's unsigned, so SmartScreen may ask: *More info → Run anyway*.
2. Make sure **Turn on now** is enabled.
3. Play audio in an app, or add it with **Add an app**.
4. Choose a device from the app's **Play on** dropdown.

If an app doesn't switch immediately, pause and resume playback. Some apps need a restart.

Settings live in `%AppData%\SoundSplit\`. Run with `--demo` to preview the UI with sample data.

## Requirements

Windows 10 1803 or later. The release is a self-contained single-file exe, so no .NET install is needed.

## Building

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
cd SoundSplit
dotnet run
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

Releases are built by GitHub Actions. Run the *SoundSplit (Windows app)* workflow with a version (e.g. `v1.0.2`) or push a `v*` tag.

## Project structure

```
SoundSplit/
  Core/         audio routing, device/session access, settings
  ViewModels/   UI state
  Themes/       theme palettes and control styles
  Assets/       icon, logo, font
```

## Credits

[NAudio](https://github.com/naudio/NAudio) (MIT) · [Nunito](https://fonts.google.com/specimen/Nunito) (OFL) · per-app endpoint research from [EarTrumpet](https://github.com/File-New-Project/EarTrumpet)
