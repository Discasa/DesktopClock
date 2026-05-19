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
- Per-item render mode, opacity, bold, italic, and animation duration.
- Default `Centro Y` / `CENTER_Y` value is `500`.

The old adaptive wallpaper color and background inversion settings were removed.

## Editor

The editor has two tabs:

- `Geral`: language, position, time layout, shared spacing, shared animation
  curve, update interval, shared outline/padding/offset values, and window
  behavior.
- `Item`: per-slot font, size, bold, italic, color, render mode, opacity,
  animation duration, width, height, text offset, and position offset.

Per-item opacity controls are shown as 0-100% sliders. Internally they are
stored as normalized `0.0` to `1.0` values in the JSON. There is no separate
global opacity control; checking `Todos` beside item opacity applies one opacity
value to every item.

The editor supports `system`, `en`, and `pt-BR` language modes. `system` uses
the current Windows UI language.

The `Todos` checkbox beside each item setting applies the selected item's value
to every slot.

When `Sempre abaixo` / `Always on bottom` is enabled, the normal clock is
attached to the Explorer wallpaper layer. This keeps the clock above the
wallpaper but below desktop icons and their selection rectangle. The editor
preview is not attached to that layer so it remains visible while editing.

Removed editor clutter:

- Console mode, because the app is a WPF `WinExe`.
- Global font family, color, size, and render mode controls, because every item
  already has explicit per-slot controls.
- Global bold, italic, and animation duration controls, because these are now
  configured per item.
- Global opacity, because the per-item opacity control with `Todos` covers the
  same workflow without duplicating state.
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
