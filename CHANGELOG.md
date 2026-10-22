# Changelog

## 0.3.0

- Add a dedicated portable ZIP with settings stored beside the executable in `data/`.
- Add a per-user Windows installer, uninstaller, Start menu shortcut, and optional desktop/sign-in shortcuts.
- Declare the .NET Framework 4.8 runtime requirement and check it during installation.
- Add release checksums and automated portable/install/uninstall verification.
- Add repository documentation and Windows build/package CI.
- Isolate test settings from the current user's configuration.

## 0.2.0 — local milestone

- Introduce a terminal-inspired dark interface and custom folder/prompt icon.
- Add category presets, JSON ruleset import/export, monitoring status, and unsaved-change feedback.
- Preserve unsaved edits when pausing monitoring.

## 0.1.0 — local milestone

- Add multiple watched folders, independent ordered rules, ruleset copying, preview, and tray controls.
- Add periodic top-level scans, file-age checks, and collision-safe moves.
