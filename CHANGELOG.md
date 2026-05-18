# Changelog

## 1.0.1 - 2026-05-18

- Updated the embedded executable, installer, uninstaller, and Windows installed-app icon.
- Set the installed-app registry icon to the main app executable so Windows Settings does not fall back to a generic icon.

## 1.0.0 - 2026-05-18

- Rewrote the clock from Python/Tkinter to C#/.NET WPF.
- Added a native transparent desktop overlay clock.
- Added a WPF live editor.
- Added per-item render mode with `filled`, `outline`, and `filled_outline`.
- Added per-item opacity.
- Removed adaptive wallpaper color and background inversion.
- Set Segoe UI as the default font family.
- Added Windows light/dark theme-aware editor colors and app icons.
- Added per-user installer and uninstaller modeled after Mute MIC.
- Added automatic updates through GitHub Releases.
- Added release build script and GitHub-ready package output.
