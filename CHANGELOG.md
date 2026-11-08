# Changelog

All notable changes to Muelle. Versions follow [semantic versioning](https://semver.org): major.minor.patch.

## Unreleased

### Added

- `README.txt` and `README.es.txt`, plain-text copies of the READMEs for reading without a Markdown
  viewer. `scripts/manual.sh` builds them with the PDF manuals.

## 2.0.0 - 2026-11-08

### Added

- English and Spanish interface. The language is chosen from the dock menu (Idioma / Language) or in
  Settings, and is remembered; the first start uses the language chosen during setup, or English.
- Setup program with its own window in English and Spanish, chosen from a menu at any moment. The same
  window opens to uninstall from Windows "Installed apps", and offers repair when Muelle is already
  installed. It closes a running Muelle before replacing or removing its files.
- The uninstaller deletes the "Start with Windows" entry of the user who uninstalls.
- README and PDF manual in English and Spanish, with images of every window.
- `scripts\screenshots.ps1`, which renders the windows to images for the manuals without showing them.

### Changed

- License: GNU GPL version 2 only (GPL-2.0-only); version 1.0 was GPL-3.0-or-later.
- Store apps show their sharpest icon: the dock reads the app package and picks the largest logo or
  the icon of its executable, instead of the small one Windows gives to the taskbar.
- Runs on .NET 10 (version 1.0 used .NET 8, which reaches the end of its support on November 10, 2026).
- The names of the applications added on the first start follow the interface language, and Windows
  Terminal is added when it is present (Windows PowerShell otherwise).
- One scan of the open windows per second, shared by every monitor, instead of one per dock window.
- Settings are written to disk shortly after the last change, not on every step of a slider.
- The settings window adapts its rows to its width and no longer cuts labels or values.
- The icon size limits in the settings file match the settings window (24 to 96 px).
- The log file is kept under 512 KB.
- The version is set only in `src/Muelle/Muelle.csproj`; the installers and the About window read it
  from there.
- New repository layout (`src`, `installer`, `scripts`, `docs`), UTF-8 files with LF line endings, and
  one license header format.

### Fixed

- Dragging an open application that is not pinned out of the dock showed the removal animation, but the
  icon stayed.
- Closing a dock window from outside (the installer, Alt+F4) left the program running without windows,
  so its executable stayed in use during an upgrade.
- Uninstalling left the "Start with Windows" entry pointing to a file that no longer existed.
- Executables are opened only as resource files when their icons are read.

## 1.0.0 - 2026-11-06

First version: floating dock (Mac style) and fixed panel (Ubuntu style) on any screen edge; one dock
per monitor or a chosen monitor; running-app indicators and unpinned running apps; Store apps and shell
objects; drag and drop to add, reorder and remove; icon lock; End task; auto-hide; start with Windows;
settings and About windows; MSI package and setup program built with WiX Toolset 5. Interface in
Spanish only.
