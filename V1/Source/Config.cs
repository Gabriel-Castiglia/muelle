// Muelle - Dock for Windows
// Copyright (C) 2026 M. Gabriel Castiglia
// Created by M. Gabriel Castiglia, 2026-11-06.
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

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
        catch (Exception ex) { Program.Log("No se pudo leer la configuración: " + ex.Message); }
        firstRun = !File.Exists(FilePath);
        var fresh = new DockConfig();
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
        catch (Exception ex) { Program.Log("No se pudo guardar la configuración: " + ex.Message); }
    }

    private void Sanitize()
    {
        IconSize = Math.Clamp(IconSize, 16, 128);
        ZoomSize = Math.Clamp(ZoomSize, IconSize, 256);
        Items ??= new();
        Items.RemoveAll(i => i == null || string.IsNullOrWhiteSpace(i.Path));
        Monitor ??= "";
        PanelOpacity = Math.Clamp(PanelOpacity, 0, 100);
    }

    /// <summary>Common applications present on the machine, so the dock does not start empty.</summary>
    private static IEnumerable<DockItemConfig> DefaultItems()
    {
        string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string sys = Environment.SystemDirectory;
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new (string name, string path)[]
        {
            ("Explorador de archivos", Path.Combine(win, "explorer.exe")),
            ("Microsoft Edge", Path.Combine(pf86, @"Microsoft\Edge\Application\msedge.exe")),
            ("Google Chrome", Path.Combine(pf, @"Google\Chrome\Application\chrome.exe")),
            ("Firefox", Path.Combine(pf, @"Mozilla Firefox\firefox.exe")),
            ("Visual Studio Code", Path.Combine(local, @"Programs\Microsoft VS Code\Code.exe")),
            ("Bloc de notas", Path.Combine(sys, "notepad.exe")),
            ("Calculadora", Path.Combine(sys, "calc.exe")),
            ("Paint", Path.Combine(sys, "mspaint.exe")),
            ("Terminal", Path.Combine(sys, @"WindowsPowerShell\v1.0\powershell.exe")),
            ("Administrador de tareas", Path.Combine(sys, "Taskmgr.exe")),
        };
        int n = 0;
        foreach (var (name, path) in candidates)
        {
            if (n >= 8) yield break;
            if (!File.Exists(path)) continue;
            n++;
            yield return new DockItemConfig { Name = name, Path = path };
        }
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
        catch (Exception ex) { Program.Log("No se pudo cambiar el arranque con Windows: " + ex.Message); }
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

    public string Name => string.IsNullOrWhiteSpace(Cfg.Name) ? System.IO.Path.GetFileNameWithoutExtension(Cfg.Path) : Cfg.Name;

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
