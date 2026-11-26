<p align="center"><img src="docs/images/icon.png" width="140" alt="Muelle"></p>

<h1 align="center">Muelle</h1>
<p align="center"><b>A dock for Windows, floating like the Mac's or fixed like Ubuntu's.</b></p>

<p align="center"><img src="docs/images/dock-en.png" width="720" alt="The dock at the bottom of the screen"></p>

Gabriel Castiglia · November 26, 2026 · version 2.5.1 · GPL-2.0-only

English · [Español](README.es.md) · [日本語](README.ja.md)

PDF manual: [`docs/Muelle-manual-en.pdf`](docs/Muelle-manual-en.pdf)

> **Windows 10/11 on x64 (64-bit Intel or AMD) only.** There is no build for ARM processors.

Muelle is an application dock for Windows 10 and 11, written in C# with .NET 10 and WPF. It can be a
floating dock with magnification under the pointer, in the style of macOS, or a fixed panel from edge
to edge, in the style of Ubuntu. Its interface is available in English, Spanish and Japanese; it starts in the
language chosen during setup (English when it runs without installation) and can be changed at any
time. It can be light, dark or follow Windows, in one of twelve colours. Several shortcuts can share one icon
as a stack, each screen can have its own icons, and new versions install themselves. "Muelle" is the Spanish
word for a dock, the place where ships moor.

## Requirements

- Windows 11, or Windows 10 version 1803 or later, on an x64 processor (64-bit Intel or AMD).

Nothing else has to be installed. Muelle carries its own .NET runtime, and the setup window runs on
the .NET Framework that Windows already includes.

## Installation

Download the files from the [latest release](https://github.com/Gabriel-Castiglia/muelle/releases/latest). There
are three ways to get Muelle, all built by `scripts\build.ps1` into the `dist` folder:

| File | What it is |
|---|---|
| `muelle-setup.exe` | Setup program, in English, Spanish or Japanese. The usual way to install. |
| `muelle.msi` | The same installation as a Windows Installer package, for silent installs: `msiexec /i muelle.msi /qn`. |
| `Muelle.exe` | The program on its own; it runs without installation from any folder. |

The `SHA256SUMS.txt` file of each release lets you check a download first, in PowerShell:
`Get-FileHash muelle-setup.exe`.

The setup program opens a window with a language menu in its top right corner (English first, then
Spanish and Japanese), and every page changes language at once. It installs Muelle for all users in `C:\Program Files\Muelle`, which takes
administrator rights (Windows asks for them when installation starts), and adds Muelle to the Start
menu. The language chosen in the setup window is the one the dock uses when it starts for the first
time.

![The setup window](docs/images/setup-install-en.png)

Running a newer `muelle-setup.exe` updates an installed Muelle and keeps each user's settings. If
Muelle is running, setup closes it before replacing its files and opens it again when done. An older
version cannot be installed over a newer one. An installed Muelle can also update itself: see [New versions](#new-versions).

For an installation without windows: `muelle-setup.exe /quiet` (Windows still asks for administrator rights).

To start Muelle with Windows, turn on Settings → Behavior → "Start Muelle when Windows starts". If you made a
Task Scheduler task that starts Muelle (`\Muelle (inicio de sesion)` or `\Muelle`), the check box follows that task:
on enables it, off disables it, and the Run key of the registry is not used too, so Muelle never starts twice.

## Uninstallation

Open Windows Settings → Apps → Installed apps, find Muelle and choose Uninstall. The same setup
window opens, with its language menu, in the language chosen when Muelle was installed. It closes
Muelle if it is running, removes the program and the Start menu shortcut, and deletes the "Start with
Windows" entry of the user who uninstalls.

Each user's settings, in `%APPDATA%\Muelle`, are kept, so a later installation finds them. To remove
them as well, delete that folder.

For an uninstall without windows: `muelle-setup.exe /uninstall /quiet`.

![The uninstall page](docs/images/setup-uninstall-en.png)

## Using the dock

- A click on an icon opens the application or, if it is already open, brings its window to the
  front; with several windows, each click brings the next one. A click on the only window that is
  already in front minimizes it. Shift+click or a middle click opens a new window.
- The icon bounces while an application starts, and a dot under an icon marks an open application.
- An icon follows its app: when the app is updated (Store apps too), changes the colour of its own icon (as
  Vigía does with its theme colour), or its shortcut or chosen icon file changes, the dock shows the new one
  within 15 seconds, without restarting. For a program, the dock
  takes the icon that the program's own shortcuts name when that file lives in the program's folder; an icon
  chosen by hand for one shortcut, from anywhere else, is left alone.
- Open applications that are not in the dock appear at its end and leave when they close. "Pin to
  dock" in their menu, or dragging them among the others, keeps them.
- Applications are added by dropping a shortcut or an `.exe` on the dock, with "Add application…" in
  the dock menu, or from Settings. Store apps (Teams, Terminal…) and shell objects such as Control
  Panel can be added too.
- Icons are reordered by dragging them along the dock and removed by dragging them out of it. "Lock
  dock" prevents both, and pinning, unpinning and renaming from the menu, to avoid accidental changes.
- A stack is one icon that holds several shortcuts: a click unfolds them beside it. See [Stacks](#stacks).
- When the icons do not fit on a screen, they shrink on that screen only.
- With a full-screen application in front (a video, a game) the dock hides completely, also from the pointer at
  the screen edge, and comes back when the application leaves full screen.

Right-clicking an icon offers a short menu: Open another window (or Open, when the app is closed),
Rename…, Pin to dock or Unpin from dock, and More (Run as administrator, Open file location, Move to
stack), then Lock dock, Settings… and Quit Muelle. End task comes last and apart, only while the app is
running. Right-clicking an empty part of the dock offers Add application…, New stack…, Lock dock,
Settings… and Quit Muelle. Everything else (style, language, appearance, updates, About) is in Settings.
Renaming with an empty name goes back to the program's own name.

## Stacks

A stack keeps several shortcuts under one icon, like the stacks of the Mac dock. A click on it unfolds its
shortcuts beside the icon, each with its own icon; a click on one opens it, or brings its window to the
front if it is already open. "Edit stack…" at the end of the list opens the stack editor.

![An open stack](docs/images/stack-en.png)

- "New stack…" in the dock menu, or in Settings → Dock applications, creates one.
- In the editor: the name of the stack; its icon, either one of its own ("Choose icon…": an `.ico`, a
  program or a picture) or "Automatic icon", its first four shortcuts in a grid; and its shortcuts, with
  Add…, Remove, Move up and Move down. Nothing changes in the dock until "Save". If the stack left the dock
  while it was being edited (it was removed, or "Each screen has its own icons" was switched), "Save" says so
  and changes nothing.
- "Move to stack" in the menu of an icon puts that icon at the end of a stack.
- A stack is moved and removed like any other icon. An open app that is in a stack does not appear again
  at the end of the dock.

## Each screen with its own icons

With the dock on every screen, by default each copy shows the same icons. Settings → Location → "Each
screen has its own icons" gives every screen its own list. A screen seen for the first time starts with a
copy of the common icons, and from then on its icons are added, removed and reordered on its own dock, or
in Settings → Dock applications, which then has an "Icons of" list to choose the screen.

Muelle recognizes a screen by the name Windows gives the physical monitor and the connector it is plugged
into, so it keeps its icons after a restart or when the screens are reordered; if Windows gives no such
name, by its display name and resolution. When a screen is disconnected its icons are kept in the settings
and come back when it is connected again. Turning the option off goes back to the common icons, and the
icons of each screen are kept for the next time.

## Settings

Right-click the dock → Settings…. Every change applies at once. `Muelle.exe --settings` starts Muelle with
the settings window open (useful for a shortcut).

![The settings window](docs/images/settings-en.png)

| Group | Options |
|---|---|
| Style | Floating dock (Mac style) or fixed panel (Ubuntu style); panel opacity. |
| Location | Screen edge; the primary screen, every screen (one copy on each) or a specific screen; each screen with its own icons. |
| Behavior | Reserve the screen strip, hide automatically, show open apps that are not pinned, lock the dock, start with Windows, look for new versions. |
| Appearance | As Windows, light or dark; one of twelve colours. |
| Icons | Size (24 to 96 px), magnification under the pointer (up to twice the size), background of the floating dock. |
| Language | English, Spanish or Japanese. |
| Dock applications | The list of pinned applications and stacks, with Add, New stack, Edit stack (or a double click), Remove, Move up and Move down; with its own icons per screen, the screen whose icons are listed. |

## Appearance

Muelle can be light, dark or follow the setting of Windows for apps (Settings → Personalization →
Colors), and changes at once when Windows changes. The dock turns into frosted light glass or dark glass,
and the settings, About and update windows follow, title bar included. The appearance is chosen in
Settings → Appearance.

![The settings window, dark](docs/images/settings-dark-en.png)

There are twelve colours. The one chosen colours the dot under open apps, the switches, sliders and
selected items, and the icon of Muelle's windows: the ship sails on a tile of that colour. The icon of
the Start menu stays blue, because Muelle is installed for all users and that shortcut belongs to all of
them.

A settings file from an earlier version keeps the dark dock it had; a new installation follows Windows.

## New versions

Once a day, Muelle asks GitHub for its latest release; "Check for updates", in About Muelle… (Settings →
About…), asks at any time. If there is a newer version, a small window asks one question: Update now, What
is new (the release page) or Later. After "Update now" everything happens by itself:

1. The `SHA256SUMS.txt` of the release must carry the author's signature (`release.sig`, checked against two
   keys built into Muelle); a release without a valid one is refused and can only be downloaded by hand from its
   page. Then the setup, `muelle-setup.exe`, is downloaded to `%TEMP%\MuelleUpdate` and checked against that
   `SHA256SUMS.txt`. A file that does not match is deleted and never run.
2. Muelle notes how it was started and closes.
3. The setup installs in silence, with the language chosen when Muelle was installed; Windows asks for
   administrator rights, as for any installation. It waits for Muelle to be gone, and closes any other copy
   that runs from the install folder.
4. Muelle opens again, with the same arguments, and says that it was updated.

If anything fails, nothing is installed: Muelle keeps going, or opens again, and says what happened (for
example, when the administrator permission is refused). A copy of `Muelle.exe` that runs without
installation cannot replace itself; for it the window only offers the release page.

The requests carry nothing but the program name and its version. The daily check is turned off in Settings
→ Behavior.

## Language

The interface is in English, Spanish and Japanese. The language is changed in Settings (Language /
Idioma / 言語, labeled in the three languages so it can be found from any of them), and it is
remembered. The setup window starts in English unless Muelle is already installed in another language. The names of the applications that Muelle adds on its first start (File Explorer, Notepad,
Calculator…) follow the interface language.

## Files

| What | Where |
|---|---|
| Installed program | `C:\Program Files\Muelle`, with `LICENSE.txt`, `README.txt`, `README.es.txt` and `README.ja.txt` |
| Settings | `%APPDATA%\Muelle\config.json` (it can be deleted to start over); the version before the last save in `config.previous.json` |
| Settings that could not be read | An icon or setting this version cannot read is dropped alone, never the whole list: what was dropped goes to `config.dropped-<date>.json` and the whole file as it was to `config.partial-<date>.json` |
| Error log, if anything fails | `%APPDATA%\Muelle\muelle.log`, kept under 512 KB (the previous part in `muelle.old.log`) |
| Update being installed | `%TEMP%\MuelleUpdate` (the setup and how Muelle was started); deleted a minute after the update installed |

## Known limitations

- When Muelle reserves its strip of the screen, Windows rearranges the desktop icons that were
  underneath and may move them to another monitor. This is how Windows treats any application bar. If
  it is a problem, turn off "Reserve its screen strip"; maximized windows will then go under the dock.

## Building from source

With the [.NET 10 SDK](https://dotnet.microsoft.com/download) installed:

```
dotnet build src\Muelle\Muelle.csproj -c Release
powershell -ExecutionPolicy Bypass -File scripts\build.ps1
```

The first command builds the program. The second builds everything in `dist`: the stand-alone
`Muelle.exe`, `muelle.msi` and `muelle-setup.exe`, and prints their SHA-256 hashes; the WiX Toolset 5 is restored
automatically as a NuGet package. A release must carry `muelle-setup.exe`, `SHA256SUMS.txt` and `release.sig` with these
names, since the updates look for them.

`Muelle.exe --self-test <log>` checks the settings file, the stacks, the icons per screen, the icons that follow
their app, the detection of full-screen apps and the update functions (version comparison, hashes, the signature,
the setup's command line), and writes one line per check to the log; its exit code is the number of failures. It works on a throwaway settings folder in `%TEMP%` that it
deletes at the end, shows no window and leaves a running Muelle alone. With `--online` it also downloads
the setup of the latest release and checks it against its `SHA256SUMS.txt` and signature, without running it. The version is set in one place, `src\Muelle\Muelle.csproj`, and the installers and the
About window read it from there.

`scripts\screenshots.ps1` renders the images in `docs\images` with example apps (from a desktop session:
over SSH the dock image comes out empty), and `scripts/manual.sh` (with pandoc and Google Chrome, on macOS
or in Git Bash on Windows) builds the PDF manuals and `README.txt`, `README.es.txt` and `README.ja.txt` from this file, `README.es.md` and `README.ja.md`. `scripts/make_icon.py`
draws the icon in its twelve colours from the engraving in `assets/` (Python 3 with numpy, opencv-python
and Pillow).

| Folder | Contents |
|---|---|
| `src/Muelle` | The program. |
| `installer/Msi` | The MSI package (WiX). |
| `installer/SetupUI` | The setup window, in English, Spanish and Japanese (.NET Framework 4.7.2, WPF). |
| `installer/Setup` | The setup program that carries the setup window and the MSI (WiX bundle). |
| `scripts` | Build, images, icon and manuals. |
| `assets` | The engraving the icon comes from. |
| `docs` | PDF manuals, images and the manual stylesheet. |

## Version history

See [`CHANGELOG.md`](CHANGELOG.md).

## License

Copyright (C) 2026 Gabriel Castiglia.

Muelle is free software: you can redistribute it and/or modify it under the terms of the GNU General
Public License, version 2 only (GPL-2.0-only), as published by the Free Software Foundation. It is
distributed WITHOUT ANY WARRANTY. See [`LICENSE`](LICENSE) for the full text.

The icon is the armed three-master of Frans Huys's engraving after Pieter Bruegel the Elder (The Sailing
Vessels, c. 1561-1565), The Metropolitan Museum of Art, public domain (CC0).
