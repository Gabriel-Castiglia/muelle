//
//  Config.cs
//  Muelle: a dock for Windows.
//
//  Copyright (C) 2026 Gabriel Castiglia. Licensed under the GNU GPL, version 3 or later.
//  Date: November 6, 2026
//  SPDX-License-Identifier: GPL-3.0-or-later
//
//  This program is free software: you can redistribute it and/or modify it under the terms of
//  the GNU General Public License as published by the Free Software Foundation, either version 3
//  of the License, or (at your option) any later version.
//  It is distributed WITHOUT ANY WARRANTY. See the LICENSE file for details.
//

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;
using Microsoft.Win32;

namespace Muelle;

public enum DockEdge { Left, Top, Right, Bottom }
public enum Backdrop { Glass, Clear }
/// <summary>Dock: floating with zoom (Mac style). Panel: fixed bar edge to edge (Ubuntu style).</summary>
public enum DockStyle { Dock, Panel }

/// <summary>A dock item as stored in the JSON.</summary>
public sealed class DockItemConfig
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Arguments { get; set; }
}

/// <summary>Settings stored in %APPDATA%\Muelle\config.json.</summary>
public sealed class DockConfig
{
    /// <summary>Icon size limits, shared by the settings slider and the validation of the file.</summary>
    public const int MinIconSize = 24, MaxIconSize = 96;

    /// <summary>
    /// Interface language: "en" or "es". A settings file of version 1.0 has none (that version was Spanish only);
    /// it then takes the language chosen in the setup window.
    /// </summary>
    public string Language { get; set; }
    public int IconSize { get; set; } = 40;
    public int ZoomSize { get; set; } = 80;
    public DockEdge Edge { get; set; } = DockEdge.Left;
    /// <summary>"" = Windows primary display; otherwise the monitor name (\\.\DISPLAY2…).</summary>
    public string Monitor { get; set; } = "";
    /// <summary>true = one copy of the dock on every monitor; false = only on the monitor given in <see cref="Monitor"/>.</summary>
    public bool AllMonitors { get; set; }
    public DockStyle Style { get; set; } = DockStyle.Dock;
    /// <summary>Background opacity in panel style, as a percentage (0 = fully transparent).</summary>
    public int PanelOpacity { get; set; } = 55;
    /// <summary>
    /// true = the dock reserves its strip (maximized windows do not cover it). Windows then rearranges
    /// the desktop icons that were underneath and may relocate them to another monitor.
    /// </summary>
    public bool ReserveSpace { get; set; } = true;
    public bool AutoHide { get; set; }
    public bool StartWithWindows { get; set; }
    /// <summary>true = running apps that are not pinned appear at the end of the dock until they close (as in macOS).</summary>
    public bool ShowRunningApps { get; set; } = true;
    /// <summary>true = icons cannot be added, deleted or reordered by dragging (prevents accidental changes).</summary>
    public bool LockItems { get; set; }
    public Backdrop Backdrop { get; set; } = Backdrop.Glass;
    public List<DockItemConfig> Items { get; set; } = new();

    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Muelle");
    public static string FilePath => Path.Combine(Folder, "config.json");

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static DockConfig Load(out bool firstRun)
    {
        firstRun = false;
        try
        {
            if (File.Exists(FilePath))
            {
                var cfg = JsonSerializer.Deserialize<DockConfig>(File.ReadAllText(FilePath), Json);
                if (cfg != null) { cfg.Sanitize(); return cfg; }
            }
        }
        catch (Exception ex) { Program.Log("Could not read the configuration: " + ex.Message); }
        firstRun = !File.Exists(FilePath);
        // On the first start, the dock speaks the language chosen in the setup window, if it was installed.
        var fresh = new DockConfig { Language = InstalledLanguage() };
        fresh.Items.AddRange(DefaultItems());
        return fresh;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
            File.Move(tmp, FilePath, true);
        }
        catch (Exception ex) { Program.Log("Could not save the configuration: " + ex.Message); }
    }

    private void Sanitize()
    {
        IconSize = Math.Clamp(IconSize, MinIconSize, MaxIconSize);
        // The magnification goes from none to twice the icon size, as in the settings slider.
        ZoomSize = Math.Clamp(ZoomSize, IconSize, IconSize * 2);
        Language = Loc.Code(Loc.FromCode(Language ?? InstalledLanguage()));
        Items ??= new();
        Items.RemoveAll(i => i == null || string.IsNullOrWhiteSpace(i.Path));
        Monitor ??= "";
        PanelOpacity = Math.Clamp(PanelOpacity, 0, 100);
    }

    /// <summary>
    /// Common applications present on the machine, so the dock does not start empty.
    /// </summary>
    private static IEnumerable<DockItemConfig> DefaultItems()
    {
        string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string sys = Environment.SystemDirectory;
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        // Windows Terminal is a Store app: it is pinned by its AppUserModelID, which also lets the dock
        // recognize its windows. Without it, Windows PowerShell takes its place.
        bool terminal = File.Exists(Path.Combine(local, @"Microsoft\WindowsApps\wt.exe"));
        var candidates = new[]
        {
            Path.Combine(win, "explorer.exe"),
            Path.Combine(pf86, @"Microsoft\Edge\Application\msedge.exe"),
            Path.Combine(pf, @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(pf, @"Mozilla Firefox\firefox.exe"),
            Path.Combine(local, @"Programs\Microsoft VS Code\Code.exe"),
            Path.Combine(sys, "notepad.exe"),
            Path.Combine(sys, "calc.exe"),
            Path.Combine(sys, "mspaint.exe"),
            terminal ? TerminalAumid : Path.Combine(sys, @"WindowsPowerShell\v1.0\powershell.exe"),
            Path.Combine(sys, "Taskmgr.exe"),
        };
        int n = 0;
        foreach (var path in candidates)
        {
            if (n >= 8) yield break;
            if (path != TerminalAumid && !File.Exists(path)) continue;
            n++;
            // No stored name: the dock shows a name in the current interface language (see DockItem.Name).
            yield return new DockItemConfig { Name = "", Path = path };
        }
    }

    private const string TerminalAumid = @"shell:AppsFolder\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App";

    /// <summary>Language stored by the installer (HKLM\Software\Muelle\Language), or English.</summary>
    private static string InstalledLanguage()
    {
        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"Software\Muelle");
            return Loc.Code(Loc.FromCode(key?.GetValue("Language") as string));
        }
        catch { return "en"; }
    }

    // ---- Start with Windows ----
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsStartupEnabled()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey);
            return k?.GetValue("Muelle") is string;
        }
        catch { return false; }
    }

    public static void SetStartup(bool on)
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(RunKey);
            if (on) k.SetValue("Muelle", "\"" + Environment.ProcessPath + "\"");
            else k.DeleteValue("Muelle", false);
        }
        catch (Exception ex) { Program.Log("Could not change the start with Windows setting: " + ex.Message); }
    }
}

/// <summary>A live dock item: its icon, whether it is running, and its bounce animation.</summary>
internal sealed class DockItem
{
    public DockItemConfig Cfg;
    public ImageSource Icon;
    public string ExePath;
    public bool Running;
    /// <summary>Running app that is not pinned: shown at the end of the dock and dropped when it closes; not saved.</summary>
    public bool Transient;
    public double BounceStart = double.NaN;
    public int BounceIdx;

    /// <summary>
    /// Name shown in the label and menus: the stored one or, if there is none (the default apps), the name of a
    /// well-known Windows program in the interface language, or else the program's own description.
    /// </summary>
    public string Name
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Cfg.Name)) return Cfg.Name;
            return KnownName(Cfg.Path) ?? (autoName ??= NiceName(Cfg.Path));
        }
    }

    private string autoName;

    /// <summary>Names of the Windows programs among the default apps, in both interface languages.</summary>
    private static string KnownName(string path)
    {
        if (path.EndsWith("!App", StringComparison.OrdinalIgnoreCase) && path.Contains("WindowsTerminal", StringComparison.OrdinalIgnoreCase))
            return "Terminal";
        return System.IO.Path.GetFileName(path).ToLowerInvariant() switch
        {
            "explorer.exe" => Loc.tr("Explorador de archivos", "File Explorer"),
            "notepad.exe" => Loc.tr("Bloc de notas", "Notepad"),
            "calc.exe" => Loc.tr("Calculadora", "Calculator"),
            "mspaint.exe" => "Paint",
            "taskmgr.exe" => Loc.tr("Administrador de tareas", "Task Manager"),
            "powershell.exe" => "Windows PowerShell",
            "msedge.exe" => "Microsoft Edge",
            "chrome.exe" => "Google Chrome",
            "firefox.exe" => "Firefox",
            "code.exe" => "Visual Studio Code",
            _ => null,
        };
    }

    public static DockItem From(DockItemConfig cfg)
    {
        var it = new DockItem { Cfg = cfg };
        it.ExePath = Shell.ResolveTarget(cfg.Path);
        it.Icon = Shell.LoadSmallNativeIcon(it.ExePath) ?? Shell.LoadIcon(cfg.Path, 256);
        if (it.Icon == null && it.ExePath != null && it.ExePath != cfg.Path) it.Icon = Shell.LoadIcon(it.ExePath, 256);
        return it;
    }

    public static string NiceName(string path)
    {
        try
        {
            if (path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
                return Shell.DisplayName(path) ?? path.Substring(path.LastIndexOf('!') + 1);
            // Shortcuts: the name Windows shows for them, which may be translated ("File Explorer.lnk"
            // is shown as "Explorador de archivos" on a Spanish Windows).
            if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var shown = Shell.DisplayName(path);
                if (!string.IsNullOrWhiteSpace(shown)) return System.IO.Path.GetFileNameWithoutExtension(shown.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? shown : shown + ".lnk");
            }
            if (Directory.Exists(path))
            {
                var dn = new DirectoryInfo(path).Name;
                return string.IsNullOrEmpty(dn) ? path : dn;
            }
            if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                var desc = FileVersionInfo.GetVersionInfo(path).FileDescription;
                if (!string.IsNullOrWhiteSpace(desc) && desc.Length <= 40) return desc.Trim();
            }
        }
        catch { }
        return System.IO.Path.GetFileNameWithoutExtension(path);
    }
}
