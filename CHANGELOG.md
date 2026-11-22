# Changelog

All notable changes to Muelle. Versions follow [semantic versioning](https://semver.org): major.minor.patch.

## 2.3.1 - 2026-11-23

### Fixed

- The dock no longer comes up over a game or a video in fullscreen. Windows only tells an app bar that a fullscreen
  app is in front, and the dock is not one when it hides itself or does not reserve its strip: it stayed on top of
  everything, and a game that scrolls the view when the pointer reaches the edge brought it up again and again.
  Now the dock looks for itself every second and whenever another window comes to the front: if that window covers
  its whole screen without a title bar, the dock hides completely, also from the pointer at the edge, and comes
  back when the app leaves fullscreen or another window comes to the front. A maximized window with a title bar
  does not count.

## 2.3.0 - 2026-11-21

### Added

- Icons follow their apps. Every 15 seconds the dock compares the dates its icons were read from, and reads again
  the icon of an app that was updated or that changed its own icon, without restarting. Before, a new icon showed
  only after Muelle restarted.
- For a program, the icon its own shortcuts (Start menu, desktop, taskbar) name, when that `.ico` lives in the
  program's folder. Vigía writes the icon of the colour chosen for it there and points its shortcuts at it; the
  dock, which points at `Vigia.exe`, kept the icon inside the program. The file is read directly, because the
  shell's icon cache keeps the old picture of a path whose file changed. An icon chosen by hand from elsewhere is
  left alone.

## 2.2.0 - 2026-11-20

### Added

- Updates that install themselves. Once a day (if the option is on) and with "Check for updates" (dock menu
  and About Muelle…), Muelle asks GitHub for its latest release and, if it is newer, asks one question:
  Update now, What is new or Later. Then everything happens alone: the setup of the release is downloaded
  and checked against the release's `SHA256SUMS.txt` (a file that does not match is deleted and never run),
  Muelle closes, the setup installs in silence keeping the language chosen at installation, and Muelle opens
  again with the same arguments. If anything fails, Muelle keeps going or comes back and says what happened.
  A copy that runs without installation only offers the release page.
- The setup takes `/muellewaitpid=<pid>` (waits for that Muelle to close before touching any file) and
  `/muellerelaunch=1` (starts Muelle again when done, saying whether the update went in), which the updates
  use together with `/quiet`.
- Stacks: one dock icon that unfolds several shortcuts beside it, like the stacks of the Mac dock. Created
  with "New stack…" (dock menu or Settings), with a name, an icon of its own or its first shortcuts in a
  grid, and shortcuts that are added, removed and reordered in the stack editor. "Move to stack" in the
  menu of an icon puts it in a stack.
- Each screen with its own icons (Settings → Location). A new screen starts with a copy of the common
  icons; screens are recognized by the device name of the monitor and its connector (or by display name and
  resolution), and the icons of a disconnected screen are kept and come back with it. Off by default: every
  screen shows the same dock, as before.
- `Muelle.exe --self-test <log>`: checks the settings file, stacks, icons per screen and the update
  functions on a throwaway configuration, without windows; the exit code is the number of failures.
  `--online` also checks the setup of the latest release against its hashes, without running it.
- `scripts\build.ps1` writes `SHA256SUMS.txt` next to the installers.

### Changed

- The images of the manuals show example apps and a stack, rendered from the default settings
  (`--sample`), never the apps of whoever renders them; there is a new image of an open stack.
- `scripts/manual.sh` also runs in Git Bash on Windows (Chrome is taken from `CHROME`).
- Text fields follow the light or dark appearance.

## 2.1.0 - 2026-11-11

### Added

- Light, dark or as Windows. The dock turns into frosted light glass or dark glass, and the settings, About
  and update windows follow, title bar included. "As Windows" changes with Windows while Muelle runs.
  Chosen in Settings → Appearance or from the dock menu. A settings file from an earlier version keeps the
  dark dock; a new installation follows Windows.
- Twelve colours. The one chosen colours the dot under open apps, the switches, sliders and selected
  items, and the icon of Muelle's windows.
- Notice of new versions: once a day Muelle asks GitHub for the latest version number and, if it is newer,
  offers to open the download page. It can be turned off in Settings → Behavior; About Muelle… asks on
  request.
- `SHA256SUMS.txt` and the PDF manuals in each release.

### Changed

- A new icon: the armed three-master of Frans Huys's engraving after Pieter Bruegel the Elder (The
  Metropolitan Museum of Art, public domain), drawn by `scripts/make_icon.py` in the twelve colours.
- The settings window: rounded cards, switches instead of check boxes, and its own drop-down lists,
  sliders and scroll bars. The dock menu has the same rounded look.
- The README and the manuals say x64 only, and link to the latest release.

### Fixed

- The setup window showed a blurred icon: it took the first frame of the icon file instead of the largest.
- An error while Muelle starts is now written to `muelle.log`; before, it only reached the Windows event
  log.

## 2.0.2 - 2026-11-09

### Fixed

- The debug data embedded in the setup window named the folder where it was built; source files are now
  named from the repository root.

## 2.0.1 - 2026-11-08

### Added

- `README.txt` and `README.es.txt`, plain-text copies of the READMEs for reading without a Markdown
  viewer. `scripts/manual.sh` builds them with the PDF manuals, and setup installs them next to
  `Muelle.exe`.

### Fixed

- The properties of `muelle-setup.exe` said "All rights reserved"; they now give the GPL-2.0-only
  license, like the rest of Muelle.

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
