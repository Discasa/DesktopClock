# Changelog

## 1.0.6 - 2026-05-19

- Fixed a regression where the clock could disappear by attaching to `Progman`
  on Explorer configurations that do not expose a usable wallpaper `WorkerW`.
- The clock now only uses the wallpaper layer when a real `WorkerW` target is
  available; otherwise it falls back to the previous visible bottom-window mode.

## 1.0.5 - 2026-05-19

- Moved the normal clock window to the Explorer wallpaper layer when it is set
  to stay below other windows, keeping desktop icon selection rectangles above
  the clock.
- Kept the editor preview as a normal preview window so editing remains visible.

## 1.0.4 - 2026-05-19

- Removed global opacity and kept opacity as a per-item setting.
- Added editor language selection with English, Portuguese, and system-default modes.
- Localized the editor interface and kept installer/uninstaller language detection tied to the Windows UI language.
- Added an option to keep the editor in the tray when closed.
- Hide the regular clock window while the editor preview is active.

## 1.0.3 - 2026-05-19

- Made the editor dark theme more consistent across item labels, sliders, and themed controls.
- Replaced opacity text boxes with 0-100% sliders.
- Moved bold, italic, and animation duration to per-item settings.
- Changed the default `CENTER_Y` / `Centro Y` value to `500`.

## 1.0.2 - 2026-05-18

- Fixed editor checkbox rows so options such as "Start with Windows" can be selected reliably.
- Increased the checkbox hit area and made the row label clickable.
- Added a clearer theme-aware checkbox visual state for dark and light mode.
- Added retry handling for transient preview config read/write races while editing.

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
