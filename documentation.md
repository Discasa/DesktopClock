# Desktop Clock Documentation

## Project Layout

- `DesktopClock.csproj`: main WPF clock/editor application.
- `Clock/`: transparent overlay clock window.
- `Editor/`: live configuration editor.
- `Models/`: JSON configuration model.
- `Services/`: config, startup, theme, and update services.
- `Native/`: small Win32 interop layer for window flags and z-order.
- `src/DesktopClock.Installer`: per-user installer.
- `src/DesktopClock.Uninstaller`: per-user uninstaller.
- `src/SetupUi`: shared installer/uninstaller controls and localized strings.
- `tools/build.ps1`: release build script.

## Configuration

The app uses `desktop-image-clock.json`.

The default preset is a vertical three-row layout matching the current desktop
format:

- 12-hour clock.
- Seconds enabled.
- Separators hidden.
- Segoe UI family defaults.
- Segoe UI Black per digit.
- Light gray digit color.
- Per-item render mode and opacity.

The old adaptive wallpaper color and background inversion settings were removed.

## Editor

The editor has two tabs:

- `Geral`: position, time layout, spacing, animation, and window behavior.
- `Item`: per-slot font, size, color, render mode, opacity, width, height, text
  offset, and position offset.

The `Todos` checkbox beside each item setting applies the selected item's value
to every slot.

Removed editor clutter:

- Console mode, because the app is a WPF `WinExe`.
- Global font family, color, size, and render mode controls, because every item
  already has explicit per-slot controls.
- Adaptive/invert color controls, because that feature was intentionally removed.

## Installer

The installer is per-user and writes to:

```text
%LOCALAPPDATA%\Desktop Clock
```

It does not require administrator privileges. It installs the app, uninstaller,
default config, native WPF runtime sidecar files, Start Menu shortcuts, Windows
Installed Apps registration, and the startup entry.

The installer supports silent update mode:

```text
--silent --from-update
```

For test runs, these environment variables are honored:

- `DESKTOPCLOCK_SKIP_STARTUP=1`
- `DESKTOPCLOCK_SKIP_LAUNCH=1`

## Updates

The app checks GitHub Releases for `Discasa/DesktopClock` once shortly after
startup. If the latest stable release tag is newer than the running version, it
downloads the release package, verifies the GitHub SHA256 digest when available,
extracts the installer, starts it with:

```text
--silent --from-update
```

Then the running app exits. The installer replaces the installed files while
preserving the existing `desktop-image-clock.json`.

Expected release package name:

```text
DesktopClock-<version>-win-x64.zip
```

The package contains `Desktop.Clock.Installer.exe`.
