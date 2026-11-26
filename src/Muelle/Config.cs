//
//  Config.cs
//  Muelle: a dock for Windows.
//
//  Copyright (C) 2026 Gabriel Castiglia. Licensed under the GNU GPL, version 2 only.
//  Date: November 6, 2026
//  SPDX-License-Identifier: GPL-2.0-only
//
//  This program is free software: you can redistribute it and/or modify it under the terms of
//  the GNU General Public License, version 2, as published by the Free Software Foundation.
//  It is distributed WITHOUT ANY WARRANTY. See the LICENSE file for details.
//

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    /// <summary>
    /// A stack (a dock icon that unfolds several shortcuts when clicked, like the stacks of the Mac dock): its
    /// shortcuts, in order. null = an ordinary icon. A stack has no <see cref="Path"/> and holds no other stacks.
    /// </summary>
    public List<DockItemConfig> Stack { get; set; }
    /// <summary>
    /// Icon chosen for this item: an .ico, .exe, .dll, .png or any file whose icon is shown. It wins over every other
    /// icon. null = for a stack, its first shortcuts in a grid; for a shortcut, its own icon or its program's.
    /// </summary>
    public string Icon { get; set; }

    /// <summary>Fields this version does not know (written by a newer one): kept and written back as they were.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement> Unknown { get; set; }

    [JsonIgnore] public bool IsStack => Stack != null;

    /// <summary>A copy that shares nothing with this one (stacks included).</summary>
    public DockItemConfig Clone() => new()
    {
        Name = Name, Path = Path, Arguments = Arguments, Icon = Icon,
        Stack = Stack?.Select(c => c.Clone()).ToList(),
        Unknown = Unknown == null ? null : new(Unknown),
    };
}

/// <summary>
/// The icons of one monitor, when every screen has its own dock (<see cref="DockConfig.PerMonitor"/>). A monitor is
/// recognized by <see cref="Id"/>, the device path Windows gives the physical screen and the connector it is plugged
/// into; if Windows gives none, or it changed, by the display name and the resolution. The entry of a monitor that is
/// disconnected stays in the file, so its icons come back when it does.
/// </summary>
public sealed class MonitorDockConfig
{
    /// <summary>Fields this version does not know (written by a newer one): kept and written back as they were.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement> Unknown { get; set; }
    public string Id { get; set; } = "";
    /// <summary>Display name when it was last seen (\\.\DISPLAY2…).</summary>
    public string Device { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public List<DockItemConfig> Items { get; set; } = new();

    /// <summary>
    /// The entry of a monitor: the one with the same <paramref name="id"/>, or else one with the same display name and
    /// resolution that no connected monitor claims by its id; null if there is none.
    /// </summary>
    public static MonitorDockConfig Find(IList<MonitorDockConfig> list, string id, string device, int width, int height,
                                         ICollection<string> connectedIds = null)
    {
        if (list == null) return null;
        if (!string.IsNullOrEmpty(id))
        {
            var byId = list.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
            if (byId != null) return byId;
        }
        return list.FirstOrDefault(m =>
            string.Equals(m.Device, device ?? "", StringComparison.OrdinalIgnoreCase) && m.Width == width && m.Height == height
            && (string.IsNullOrEmpty(m.Id) || connectedIds == null || !connectedIds.Contains(m.Id, StringComparer.OrdinalIgnoreCase)));
    }
}

/// <summary>Settings stored in %APPDATA%\Muelle\config.json.</summary>
public sealed class DockConfig
{
    /// <summary>Icon size limits, shared by the settings slider and the validation of the file.</summary>
    public const int MinIconSize = 24, MaxIconSize = 96;

    /// <summary>
    /// Interface language: "en", "es" or "ja". A settings file of version 1.0 has none (that version was Spanish only);
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
    /// <summary>
    /// "auto" (as Windows), "light" or "dark". A settings file from before version 2.1 has none and keeps the
    /// dark dock it had; a new one follows Windows.
    /// </summary>
    public string Appearance { get; set; }
    /// <summary>Accent colour: the key of one of <see cref="Theme.Accents"/>.</summary>
    public string Accent { get; set; }
    /// <summary>true = once a day Muelle asks GitHub whether there is a newer version, and offers to install it if there is.</summary>
    public bool CheckUpdates { get; set; } = true;
    public DateTime LastUpdateCheck { get; set; }
    public List<DockItemConfig> Items { get; set; } = new();
    /// <summary>
    /// true = each screen has its own icons (<see cref="MonitorDocks"/>); false = every dock shows <see cref="Items"/>.
    /// A screen that gets its own dock for the first time starts with a copy of <see cref="Items"/>.
    /// </summary>
    public bool PerMonitor { get; set; }
    public List<MonitorDockConfig> MonitorDocks { get; set; } = new();
    /// <summary>Version of Muelle that last wrote the file: when another one reads it, a copy is kept first.</summary>
    public string SavedBy { get; set; }
    /// <summary>Settings this version does not know (written by a newer one): kept and written back as they were.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement> Unknown { get; set; }

    /// <summary>Folder used instead of %APPDATA%\Muelle (the self-test works on a throwaway copy); null = the real one.</summary>
    internal static string FolderOverride;

    public static string Folder => FolderOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Muelle");
    public static string FilePath => Path.Combine(Folder, "config.json");

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The previous file, written before every save: what is read when config.json cannot be.</summary>
    public static string PreviousPath => Path.Combine(Folder, "config.previous.json");

    private static string RunningVersion =>
        typeof(DockConfig).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion ?? "";

    /// <summary>
    /// The settings are the user's: nothing here may put them back to the defaults. A setting this version cannot read
    /// (a value a newer version added, a damaged entry) is dropped alone and the rest is kept; in the lists of icons,
    /// that is one icon (or one shortcut of a stack), never the whole list. Whatever was dropped is written to
    /// config.dropped-&lt;date&gt;.json and the whole file is kept as config.partial-&lt;date&gt;.json, both before
    /// anything is saved. A file that cannot be read at all is kept aside with the date, and the previous one is read
    /// instead. When another version of Muelle reads the file for the first time (it was updated or went back), a copy
    /// is kept before anything is written.
    /// </summary>
    public static DockConfig Load(out bool firstRun)
    {
        firstRun = false;
        droppedOnLoad = previousKept = false;
        bool exists = File.Exists(FilePath);
        if (exists)
        {
            var cfg = Read(FilePath, out var dropped);
            string from = FilePath;
            if (cfg == null)
            {
                Program.Log("Could not read the configuration: kept aside, and the previous one is read.");
                Keep(FilePath, "config.unreadable-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
                if (File.Exists(PreviousPath)) { cfg = Read(PreviousPath, out dropped); from = PreviousPath; }
            }
            if (cfg != null && dropped != null)
            {
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                Keep(from, "config.partial-" + stamp + ".json");
                try
                {
                    Directory.CreateDirectory(Folder);
                    File.WriteAllText(Path.Combine(Folder, "config.dropped-" + stamp + ".json"), dropped.ToJsonString(Json));
                }
                catch (Exception ex) { Program.Log("Could not keep what was dropped from the configuration: " + ex.Message); }
                droppedOnLoad = true;
            }
            if (cfg != null)
            {
                if (cfg.SavedBy != RunningVersion)
                    Keep(FilePath, "config.before-" + (string.IsNullOrEmpty(RunningVersion) ? "update" : RunningVersion) + ".json");
                cfg.Sanitize();
                return cfg;
            }
        }
        firstRun = !exists;
        // On the first start, the dock speaks the language chosen in the setup window, if it was installed.
        var fresh = new DockConfig { Language = InstalledLanguage(), Appearance = Theme.Auto, Accent = Theme.DefaultAccent };
        fresh.Items.AddRange(DefaultItems());
        return fresh;
    }

    /// <summary>The settings of a file, without the ones that cannot be read; null if it is not a settings file.</summary>
    private static DockConfig Read(string file) => Read(file, out _);

    /// <summary>
    /// The same; <paramref name="dropped"/> gets what could not be read (each piece as it was in the file), or null
    /// when everything was read.
    /// </summary>
    internal static DockConfig Read(string file, out JsonArray dropped)
    {
        dropped = null;
        string text;
        try { text = File.ReadAllText(file); } catch (Exception ex) { Program.Log("Could not read " + file + ": " + ex.Message); return null; }
        try { return JsonSerializer.Deserialize<DockConfig>(text, Json); }
        catch (Exception ex) { Program.Log("Part of the configuration could not be read: " + ex.Message); }
        // One setting by itself, so a value that cannot be read drops that setting only; the lists of icons, one icon
        // by itself, so a damaged icon drops that icon only and not the whole dock.
        try
        {
            if (JsonNode.Parse(text) is not JsonObject all) return null;
            var lost = new JsonArray();
            var kept = new JsonObject();
            foreach (var (name, value) in all.ToList())
            {
                if (Fits<DockConfig>(new JsonObject { [name] = value?.DeepClone() })) { kept[name] = value?.DeepClone(); continue; }
                if (value is JsonArray list && name is nameof(Items) or nameof(MonitorDocks))
                {
                    var good = new JsonArray();
                    foreach (var entry in list)
                    {
                        var one = name == nameof(Items) ? KeepItem(entry, lost) : KeepMonitor(entry, lost);
                        if (one != null) good.Add(one);
                    }
                    kept[name] = good;
                    continue;
                }
                Program.Log("Setting dropped, it could not be read: " + name);
                lost.Add(new JsonObject { [name] = value?.DeepClone() });
            }
            if (lost.Count > 0) dropped = lost;
            return JsonSerializer.Deserialize<DockConfig>(kept.ToJsonString(), Json);
        }
        catch { return null; }
    }

    private static bool Fits<T>(JsonNode node)
    {
        try { JsonSerializer.Deserialize<T>(node?.ToJsonString() ?? "null", Json); return true; }
        catch { return false; }
    }

    /// <summary>An icon that can be read, as it was; a stack without the shortcuts that cannot be read; else null (and it goes to <paramref name="lost"/>).</summary>
    private static JsonNode KeepItem(JsonNode entry, JsonArray lost)
    {
        if (Fits<DockItemConfig>(entry)) return entry?.DeepClone();
        if (entry is JsonObject o && o[nameof(DockItemConfig.Stack)] is JsonArray children)
        {
            var copy = (JsonObject)o.DeepClone();
            var good = new JsonArray();
            foreach (var c in children)
            {
                if (Fits<DockItemConfig>(c)) good.Add(c?.DeepClone());
                else { Program.Log("Shortcut of a stack dropped, it could not be read."); lost.Add(c?.DeepClone()); }
            }
            copy[nameof(DockItemConfig.Stack)] = good;
            if (Fits<DockItemConfig>(copy)) return copy;
        }
        Program.Log("Icon dropped, it could not be read.");
        lost.Add(entry?.DeepClone());
        return null;
    }

    /// <summary>The icons of a screen that can be read, as <see cref="KeepItem"/> does for each of its icons; else null.</summary>
    private static JsonNode KeepMonitor(JsonNode entry, JsonArray lost)
    {
        if (Fits<MonitorDockConfig>(entry)) return entry?.DeepClone();
        if (entry is JsonObject o && o[nameof(MonitorDockConfig.Items)] is JsonArray items)
        {
            var copy = (JsonObject)o.DeepClone();
            var good = new JsonArray();
            foreach (var i in items) if (KeepItem(i, lost) is JsonNode one) good.Add(one);
            copy[nameof(MonitorDockConfig.Items)] = good;
            if (Fits<MonitorDockConfig>(copy)) return copy;
        }
        Program.Log("The icons of a screen were dropped, they could not be read.");
        lost.Add(entry?.DeepClone());
        return null;
    }

    /// <summary>
    /// Part of the file could not be read when it was loaded: the previous file then keeps the version that still had
    /// it (the first save of the session puts the read file there, and no later save replaces it).
    /// </summary>
    private static bool droppedOnLoad, previousKept;

    /// <summary>A copy of a settings file next to it, if there is none of that name yet.</summary>
    private static void Keep(string file, string name)
    {
        try
        {
            string to = Path.Combine(Folder, name);
            if (!File.Exists(to)) File.Copy(file, to);
        }
        catch (Exception ex) { Program.Log("Could not keep a copy of the configuration: " + ex.Message); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            SavedBy = RunningVersion;
            var tmp = FilePath + ".tmp";
            // Written through to the disk before it replaces the file: a power cut right after leaves the new file
            // or the old one, never one that exists but is empty.
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                var bytes = new System.Text.UTF8Encoding(false).GetBytes(JsonSerializer.Serialize(this, Json));
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true);
            }
            // The file being replaced stays as the previous one, but only if it can be read (never a damaged one), and
            // never a file this session wrote without the part it could not read (see droppedOnLoad).
            if (File.Exists(FilePath) && !(droppedOnLoad && previousKept) && Read(FilePath) != null)
            {
                File.Copy(FilePath, PreviousPath, true);
                previousKept = true;
            }
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
        SanitizeItems(Items ??= new());
        MonitorDocks ??= new();
        MonitorDocks.RemoveAll(m => m == null);
        foreach (var m in MonitorDocks) { m.Id ??= ""; m.Device ??= ""; SanitizeItems(m.Items ??= new()); }
        Monitor ??= "";
        PanelOpacity = Math.Clamp(PanelOpacity, 0, 100);
        Appearance = Appearance is Theme.Auto or Theme.Light or Theme.Dark ? Appearance : Theme.Dark;
        Accent = Theme.Get(Accent).Key;
    }

    /// <summary>Drops empty entries; a stack keeps only its valid shortcuts (never another stack) and always has a name.</summary>
    internal static void SanitizeItems(List<DockItemConfig> items)
    {
        items.RemoveAll(i => i == null || (!i.IsStack && string.IsNullOrWhiteSpace(i.Path)));
        foreach (var i in items.Where(i => i.IsStack))
        {
            i.Stack.RemoveAll(c => c == null || c.IsStack || string.IsNullOrWhiteSpace(c.Path));
            i.Path = "";
            if (string.IsNullOrWhiteSpace(i.Name)) i.Name = Loc.tr("Pila", "Stack", "スタック");
        }
    }

    /// <summary>
    /// Common applications present on the machine, so the dock does not start empty.
    /// </summary>
    internal static IEnumerable<DockItemConfig> DefaultItems()
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
    /// <summary>
    /// A scheduled task that starts Muelle at sign-in, made by hand instead of the Run key (it starts earlier and with a
    /// higher priority). When it exists, the check box follows it: on enables it, off disables it, and the Run key is
    /// not used too, so Muelle never starts twice.
    /// </summary>
    internal static readonly string[] StartupTasks = { @"\Muelle (inicio de sesion)", @"\Muelle" };

    private static bool RunValue()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey);
            return k?.GetValue("Muelle") is string;
        }
        catch { return false; }
    }

    /// <summary>The startup task of <see cref="StartupTasks"/> that exists and runs Muelle.exe, with whether it is enabled; null if none.</summary>
    internal static (string Name, bool Enabled)? StartupTask()
    {
        foreach (var name in StartupTasks)
        {
            var (code, output) = Schtasks($"/query /tn \"{name}\" /xml");
            if (code != 0 || !output.Contains("Muelle.exe", StringComparison.OrdinalIgnoreCase)) continue;
            var m = System.Text.RegularExpressions.Regex.Match(output, @"<Settings>.*?<Enabled>(\w+)</Enabled>", System.Text.RegularExpressions.RegexOptions.Singleline);
            return (name, !m.Success || !m.Groups[1].Value.Equals("false", StringComparison.OrdinalIgnoreCase));
        }
        return null;
    }

    private static (int Code, string Output) Schtasks(string arguments)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("schtasks.exe", arguments)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
            });
            var output = p.StandardOutput.ReadToEndAsync();
            p.StandardError.ReadToEnd();
            if (!p.WaitForExit(10000)) { try { p.Kill(); } catch { } return (-1, ""); }
            return (p.ExitCode, output.Result);
        }
        catch { return (-1, ""); }
    }

    public static bool IsStartupEnabled() => RunValue() || StartupTask() is { Enabled: true };

    /// <summary>Refreshes the path in the Run key if Muelle starts from there (the executable may have moved).</summary>
    public static void RefreshStartup()
    {
        if (RunValue() && StartupTask() == null) SetRun(true);
    }

    public static void SetStartup(bool on)
    {
        if (StartupTask() is { } task)
        {
            var (code, _) = Schtasks($"/change /tn \"{task.Name}\" {(on ? "/enable" : "/disable")}");
            if (code != 0) Program.Log($"Could not {(on ? "enable" : "disable")} the startup task {task.Name} (schtasks {code}).");
            SetRun(false);
        }
        else SetRun(on);
    }

    private static void SetRun(bool on)
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
    /// <summary>What the icon was read from (<see cref="Shell.IconSignature"/>): when it changes, the icon is read again.</summary>
    public string IconSig;

    /// <summary>The icon signature of this item right now (see <see cref="Shell.IconSignature"/>).</summary>
    public string CurrentIconSig() => Shell.IconSignature(Cfg.Path, Cfg.Icon, ExePath);
    public bool Running;
    /// <summary>Running app that is not pinned: shown at the end of the dock and dropped when it closes; not saved.</summary>
    public bool Transient;
    public double BounceStart = double.NaN;
    public int BounceIdx;
    /// <summary>The shortcuts of a stack, ready to show; null for an ordinary icon.</summary>
    public List<DockItem> Children;

    public bool IsStack => Cfg.IsStack;

    /// <summary>
    /// Name shown in the label and menus: the stored one or, if there is none (the default apps), the name of a
    /// well-known Windows program in the interface language, or else the program's own description.
    /// </summary>
    public string Name
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Cfg.Name) || IsStack) return Cfg.Name;
            return KnownName(Cfg.Path) ?? (autoName ??= NiceName(Cfg.Path));
        }
    }

    private string autoName;

    /// <summary>Names of well-known Windows programs and shortcuts, in the interface language.</summary>
    private static string KnownName(string path)
    {
        if (path.EndsWith("!App", StringComparison.OrdinalIgnoreCase) && path.Contains("WindowsTerminal", StringComparison.OrdinalIgnoreCase))
            return "Terminal";
        return System.IO.Path.GetFileName(path).ToLowerInvariant() switch
        {
            "explorer.exe" or "file explorer.lnk" => Loc.tr("Explorador de archivos", "File Explorer", "エクスプローラー"),
            // Shortcuts that Windows creates with English file names on every installation.
            "control panel.lnk" => Loc.tr("Panel de control", "Control Panel", "コントロール パネル"),
            "remote desktop connection.lnk" => Loc.tr("Conexión a Escritorio remoto", "Remote Desktop Connection", "リモート デスクトップ接続"),
            "notepad.exe" => Loc.tr("Bloc de notas", "Notepad", "メモ帳"),
            "calc.exe" => Loc.tr("Calculadora", "Calculator", "電卓"),
            "mspaint.exe" => "Paint",
            "taskmgr.exe" => Loc.tr("Administrador de tareas", "Task Manager", "タスク マネージャー"),
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
        if (cfg.IsStack)
        {
            it.Children = cfg.Stack.Select(From).ToList();
            if (!string.IsNullOrWhiteSpace(cfg.Icon)) it.Icon = Shell.LoadChosenIcon(cfg.Icon);
            it.Icon ??= StackIcon(it.Children.Select(c => c.Icon));
            return it;
        }
        it.ExePath = Shell.ResolveTarget(cfg.Path);
        // An icon chosen by hand wins: the one of this item in the settings, or the one set on this very shortcut.
        if (!string.IsNullOrWhiteSpace(cfg.Icon)) it.Icon = Shell.LoadChosenIcon(cfg.Icon);
        if (it.Icon == null && Shell.ChosenIconPath(cfg.Path) is string chosen)
            it.Icon = (chosen.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) ? Shell.LoadIconFile(chosen) : null) ?? Shell.LoadIcon(cfg.Path, 256);
        // Packaged apps: their own sharpest logo (Windows may give the dock a 24 px one).
        if (it.Icon == null && it.ExePath != null && it.ExePath.StartsWith(WindowTracker.AumidPrefix, StringComparison.Ordinal))
            it.Icon = Shell.LoadPackagedIcon(it.ExePath.Substring(WindowTracker.AumidPrefix.Length));
        // The icon the program gives its own shortcuts (for example the colour chosen in Vigía), read from its file.
        if (it.Icon == null && Shell.ThemedIconPath(it.ExePath) is string themed) it.Icon = Shell.LoadIconFile(themed);
        it.Icon ??= Shell.LoadSmallNativeIcon(it.ExePath) ?? Shell.LoadIcon(cfg.Path, 256);
        if (it.Icon == null && it.ExePath != null && it.ExePath != cfg.Path) it.Icon = Shell.LoadIcon(it.ExePath, 256);
        it.IconSig = it.CurrentIconSig();
        return it;
    }

    /// <summary>
    /// The icon of a stack without an icon of its own: its first four shortcuts in a grid, on a soft rounded tile
    /// (grey, so it reads on light and dark docks alike).
    /// </summary>
    public static ImageSource StackIcon(IEnumerable<ImageSource> icons)
    {
        const double size = 256, pad = 26, gap = 14, cell = (size - 2 * pad - gap) / 2;
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            var tile = new SolidColorBrush(Color.FromArgb(0x8C, 0x80, 0x86, 0x90));
            var edge = new Pen(new SolidColorBrush(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF)), 4);
            dc.DrawRoundedRectangle(tile, edge, new System.Windows.Rect(2, 2, size - 4, size - 4), 56, 56);
            int n = 0;
            foreach (var icon in icons)
            {
                if (n >= 4) break;
                if (icon == null) continue;
                double x = pad + (n % 2) * (cell + gap), y = pad + (n / 2) * (cell + gap);
                dc.DrawImage(icon, new System.Windows.Rect(x, y, cell, cell));
                n++;
            }
        }
        var image = new DrawingImage(group);
        image.Freeze();
        return image;
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
