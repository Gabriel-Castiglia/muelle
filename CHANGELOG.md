# Changelog

All notable changes to Muelle. Versions follow [semantic versioning](https://semver.org): major.minor.patch.

English · [Español](CHANGELOG.es.md) · [日本語](CHANGELOG.ja.md)

## 2.5.1 - 2026-11-26

### Fixed

- With "Lock dock" on, a click on the "+" slot of an empty dock still opened the file dialog and added apps; it
  does nothing now, like the other ways of adding.
- An `.ico` chosen as the icon of a stack is read straight from its file, as for any other icon: a file that
  changed showed the old picture kept in the shell's icon cache.
- A settings window opened at startup (`Muelle.exe --settings`) could show "Start Muelle when Windows starts" as it
  was last saved, not as the startup task or the Run key say; it is updated as soon as Muelle has looked.
- The update window changes the icon in its header when the colour is changed, like the About window.
- The READMEs and manuals said that the dock moves behind a full-screen app (it hides completely since 2.3.1) and
  that "Check for updates" and the appearance are in the dock menu (they are in About Muelle… and in Settings
  since 2.4.0).

### Changed

- When setup closes a running Muelle to update it, it opens it again when done (also with `/quiet`), as the
  signed-in user even if setup was run as administrator; before, only an update started from Muelle did so.
- The PDF manuals use Muelle's own light colours and its blue accent, with a larger icon on the cover.
- The version history is also in Spanish and Japanese (`CHANGELOG.es.md`, `CHANGELOG.ja.md`).
- The READMEs mention `Muelle.exe --settings` and everything the self-test checks; the Spanish texts write "icono"
  everywhere and use "vos" in the hint of the empty dock, like the rest of the program.
- Code cleanup: the add-apps file dialog, the buttons and muted texts of the windows, opening a file or web page,
  showing a file in Explorer, reading a chosen icon and the names of the screens are each written once and
  shared; unused code is removed, including `scripts/make-icon.ps1`, which drew the icon used before 2.1.0 and
  would have overwritten the current one.
- The self-test checks the icon of a stack and the add-apps dialog too.

## 2.5.0 - 2026-11-26

### Added

- "Start Muelle when Windows starts" recognizes a Task Scheduler task made by hand that starts Muelle
  (`\Muelle (inicio de sesion)` or `\Muelle`): the check box shows it, turning it off disables the task and turning it
  on enables it again, and the Run key is not added on top, so Muelle never starts twice. The task is looked for off
  the UI thread, so the dock does not start later.
- README and PDF manual in Japanese (`README.ja.md`, `docs/Muelle-manual-ja.pdf`), with images of the windows in
  Japanese; setup installs `README.ja.txt` next to the other READMEs. Building the manuals checks the version under
  the title of the Japanese README too.
- The self-test checks that the startup task can be looked for.

### Changed

- Lighter drawing: the shadow under the icons is no longer applied to the pointer area, which spans the whole
  length of the screen, but only to what is drawn; the name label is laid out once per name instead of on
  every frame, and the tiles of icons without a picture are made once.
- The pictures of the windows in the manuals are taken again, so Settings shows "Lock dock".

## 2.4.2 - 2026-11-25

### Changed

- **Updates signed by the author**: the `SHA256SUMS.txt` of a release must now carry the author's signature
  (`release.sig`, ECDSA P-256, checked against two keys built into Muelle) before the setup is downloaded. Before,
  the setup was checked only against hashes that came from the same page, so a release put there by someone else
  would have installed itself. A release without a valid signature is offered as a page, to download by hand.
  Anyone can still download, install and share Muelle as before. Muelle 2.4.1 and older install this one as usual.

### Fixed

- Settings said "Lock the icons" while the right-click menu says "Lock dock", and the option locks more than the
  icons' order: it is "Lock dock" in both places now, and its text names what it stops (adding, removing,
  reordering, pinning and renaming icons).
- The manuals (PDF) and the `README.txt` installed next to `Muelle.exe` described version 2.3.1 since 2.3.2: they
  are made again from the READMEs of this version. Building the manuals now stops when the version written under
  the title of a README is not the program's.

## 2.4.1 - 2026-11-25

### Fixed

- One icon that cannot be read no longer empties the dock. Before, a single damaged icon in `config.json` (a
  field of another type after editing it by hand, or going back to an older version) dropped the whole list,
  and the next saves left `config.previous.json` empty too. Now that icon (or that shortcut of a stack) is
  dropped alone; what was dropped is kept in `config.dropped-<date>.json` and the whole file in
  `config.partial-<date>.json`, and `config.previous.json` keeps the version that still had it.
- The settings are written through to the disk before they replace the file: a power cut right after a save
  leaves the new file or the old one.
- The daily check for updates counts only when it reached GitHub. A laptop that started without network lost
  that day's check; now it tries again an hour later.
- Accepting "Rename…" without changing the name no longer fixes it: a default app's name still follows the
  interface language.
- A stack removed (or left on a list that is no longer shown) while its editor was open no longer comes back
  when "Save" is pressed: the editor says so and changes nothing.
- A dock that closes (when screens are connected or disconnected, or with a dock per screen) no longer
  reserves its strip of the screen again a moment later.
- An app installed with ClickOnce (`.appref-ms`) keeps its dot of "open" after it updates itself.
- The downloaded setup in `%TEMP%\MuelleUpdate` is deleted after an update installs.

### Changed

- The dock no longer walks the Start menu, the desktop and the taskbar shortcuts every 15 seconds: it watches
  those folders and reads them again only when something changes (and every 5 minutes, just in case). The
  dates of the icons' files are read off the window's thread, so an icon on a network folder that does not
  answer no longer freezes the dock.
- The list of screens is kept until Windows says they changed, instead of being asked for every second.
- The right-click menu of a Store app (Teams, Terminal…) opens faster: it no longer opens every process of the
  system to know whether "End task" applies.

## 2.4.0 - 2026-11-24

### Added

- Japanese. The interface, the setup window and the names of the colours and of the default apps are in English,
  Spanish and Japanese, listed in that order in Settings and in the setup window.
- "Rename…" in the menu of a pinned icon. An empty name goes back to the program's own name.

### Changed

- The right-click menu of an icon is short: Open another window (or Open, when the app is closed), Rename…, Pin
  to dock or Unpin from dock, and More (Run as administrator, Open file location, Move to stack); then Lock dock,
  Settings… and Quit Muelle. End task comes last and apart, only while the app is running. Right-clicking an
  empty part of the dock offers Add application…, New stack…, Lock dock, Settings… and Quit Muelle. The style,
  language, appearance, updates and About are in Settings only.
- "Lock icons" is now "Lock dock", and it also turns off pinning, unpinning, renaming and adding from the menu.
- The setup window starts in English, unless Muelle is already installed in another language; before, it took
  the language of Windows when that was Spanish.

### Fixed

- An icon follows more changes without restarting the dock: Store apps that update themselves (Teams,
  Terminal…), a shortcut whose icon is changed in its properties, and an icon chosen in the settings whose file
  changes. Before, only a change of the program file or of its theme icon was noticed.

## 2.3.2 - 2026-11-24

### Fixed

- A shortcut with an icon chosen by hand shows it again. Since 2.3.0 the dock took the icon of the program the
  shortcut opens, from the program's own shortcuts, and dropped the one chosen for that shortcut: a shortcut that
  opens a document with a red icon showed the program's icon instead. Now the icon chosen for a shortcut (in its
  properties, from outside the program's folder) wins, and the program's own icon is followed only when there is none.
- An icon chosen in the settings (`Icon` of an item in `config.json`) now applies to any icon, not only to stacks,
  and wins over every other.

### Changed

- The settings are never put back to the defaults. A setting that a version cannot read (a value added by a newer
  version, a damaged entry) is dropped alone and all the others are kept; before, one such value made Muelle start
  with every setting at its default, and the next save wrote them over the user's. A file that cannot be read at
  all is kept aside as `config.unreadable-<date>.json` and the previous file, `config.previous.json` (written before
  every save, never from a damaged file), is read instead.
- Settings written by a newer version are kept and written back as they were, so going back to an older version
  and forward again loses nothing.
- When a different version of Muelle reads the settings for the first time (after an update), it first keeps a
  copy of them as `config.before-<version>.json`.

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
