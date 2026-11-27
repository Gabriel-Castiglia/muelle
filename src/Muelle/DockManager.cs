//
//  DockManager.cs
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

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Muelle;

/// <summary>
/// Coordinates the dock windows: a single one (primary monitor or a specific one) or one per monitor.
/// All share the same configuration and a single scan of the open windows; they show the same list of
/// applications or, with <see cref="DockConfig.PerMonitor"/>, each screen its own.
/// </summary>
internal sealed class DockManager
{
    public DockConfig Config { get; }
    /// <summary>The icons shared by every dock (<see cref="DockConfig.Items"/>).</summary>
    public List<DockItem> Items { get; } = new();
    public bool IsScreenshot => screenshotPath != null;

    private readonly string screenshotPath;
    /// <summary>
    /// Window rendered by a test screenshot: "dock", "settings", "settings-locked" (only the "Dock applications" card,
    /// with the dock locked), "about", "update", "menu", "stack" or "stack-editor".
    /// </summary>
    private readonly string screenshotWindow;
    private readonly List<DockWindow> windows = new();
    private readonly DispatcherTimer rebuildTimer, runningTimer, saveTimer;
    private string monitorKey;
    private SettingsWindow settings;
    /// <summary>The live icons of each screen that has its own dock, created when first needed.</summary>
    private readonly Dictionary<MonitorDockConfig, List<DockItem>> monitorLists = new();
    private AboutWindow about;

    /// <param name="selfTest">The self-test: the configuration is the throwaway one and Windows settings (start with Windows) are not touched.</param>
    public DockManager(string screenshotPath, string screenshotWindow, bool selfTest = false)
    {
        this.screenshotPath = screenshotPath;
        this.screenshotWindow = screenshotWindow;
        Config = DockConfig.Load(out bool firstRun);
        Loc.Current = Loc.FromCode(Config.Language);
        // A test screenshot shows the dock even if it hides itself (the configuration is never saved in that mode).
        if (screenshotPath != null) Config.AutoHide = false;
        Theme.Set(Config.Appearance, Config.Accent);
        Theme.Changed += () => { foreach (var w in windows) w.View.ThemeChanged(); };
        if (screenshotPath == null && !selfTest)
        {
            if (firstRun) Config.Save();
            // Looking for a startup task runs schtasks, so it is done off the UI thread and never delays the dock.
            var ui = Dispatcher.CurrentDispatcher;
            Task.Run(() =>
            {
                bool on = DockConfig.IsStartupEnabled();
                if (on) DockConfig.RefreshStartup(); // the path, if the executable moved
                ui.BeginInvoke(() =>
                {
                    if (Config.StartWithWindows == on) return;
                    Config.StartWithWindows = on;
                    // A settings window opened meanwhile (--settings) would show the stored value instead.
                    settings?.Rebuild();
                });
            });
        }
        foreach (var c in Config.Items) Items.Add(DockItem.From(c));

        rebuildTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(600) };
        rebuildTimer.Tick += (s, e) => { rebuildTimer.Stop(); Rebuild(); };
        // One scan of the open windows per second, shared by every dock window.
        runningTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        runningTimer.Tick += (s, e) => RefreshRunning();
        // Settings are written to disk shortly after the last change, not on every slider step.
        saveTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(500) };
        saveTimer.Tick += (s, e) => FlushConfig();
    }

    /// <summary>Shows the dock (or, for a test screenshot, the requested window).</summary>
    public void Start()
    {
        if (IsScreenshot && screenshotWindow != "dock")
        {
            Window w = screenshotWindow switch
            {
                "about" => new AboutWindow(this),
                "update" => new UpdateWindow(this, new Release
                {
                    Tag = new Version(Updates.Current.Major, Updates.Current.Minor + 1, 0).ToString(),
                    SetupUrl = Updates.SetupName, SumsUrl = Updates.SumsName, SigUrl = ReleaseSignature.AssetName,
                }),
                "menu" => MenuPreview(),
                "stack" => MenuPreview(Items.FirstOrDefault(i => i.IsStack)),
                "stack-editor" => new StackWindow(this, Items, Items.FirstOrDefault(i => i.IsStack), 0),
                // Only in memory, like every option of a screenshot: the configuration is never saved in this mode.
                "settings-locked" => LockedForScreenshot(),
                _ => new SettingsWindow(this),
            };
            w.ContentRendered += (s, e) =>
            {
                // The settings show the keyboard focus on the chosen colour, as after a Tab into that group.
                if (w is SettingsWindow sw && screenshotWindow == "settings") sw.AccentTile?.Focus();
                w.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (w is SettingsWindow locked && screenshotWindow == "settings-locked") SaveScreenshot(locked.AppsCard, locked.Background, screenshotPath);
                    else SaveScreenshot(w, screenshotPath);
                    Exit();
                }), DispatcherPriority.ContextIdle);
            };
            w.Show();
            return;
        }
        Rebuild();
        RefreshRunning();
        runningTimer.Start();
        foreach (var w in windows) w.View.ThemeChanged();
        // Once a day, if the option is on, ask GitHub whether there is a newer version (the first time a little
        // after starting, so it does not slow down the start with Windows).
        updateTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(30) };
        updateTimer.Tick += async (s, e) =>
        {
            updateTimer.Interval = TimeSpan.FromHours(1);
            if (IsScreenshot || exiting || checkingUpdates || !Config.CheckUpdates || DateTime.UtcNow - Config.LastUpdateCheck < TimeSpan.FromHours(24)) return;
            checkingUpdates = true;
            try { await Updates.CheckAsync(this); }
            finally { checkingUpdates = false; }
            // Only a check that reached GitHub counts for the day: without network yet (a laptop just started) it is
            // tried again the next hour, instead of the day being lost.
            if (Updates.LastCheckReached && !exiting)
            {
                Config.LastUpdateCheck = DateTime.UtcNow;
                SaveConfig();
            }
        };
        updateTimer.Start();
        iconTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(15) };
        iconTimer.Tick += async (s, e) =>
        {
            if (exiting || refreshingIcons) return;
            refreshingIcons = true;
            try { await RefreshIconsAsync(); }
            catch (Exception ex) { Program.Log("Could not refresh the icons: " + ex.Message); }
            finally { refreshingIcons = false; }
        };
        iconTimer.Start();
    }

    private bool refreshingIcons;
    private DateTime lastClickOnceCheck = DateTime.UtcNow;

    private DispatcherTimer updateTimer;
    private DispatcherTimer iconTimer;
    private bool checkingUpdates;

    /// <summary>
    /// An app that was updated (also Store apps), that changed the colour of its icon (as Vigía does), or whose shortcut or
    /// chosen icon changed, shows its new icon without restarting the dock: every 15 s the dates its icons were read
    /// from are compared (a few file dates, no icon is read unless one changed). Returns how many icons were read again.
    /// </summary>
    public int RefreshIcons()
    {
        var sigs = new Dictionary<DockItem, string>();
        foreach (var it in IconItems()) sigs[it] = it.CurrentIconSig();
        return ApplyIcons(sigs);
    }

    /// <summary>
    /// The same, with the file dates read off the window's thread: a dock item on a network folder that does not
    /// answer, or the walk of the shortcut folders, never freezes the dock. Every 5 minutes it also asks again where a
    /// ClickOnce app (.appref-ms) is installed, since each of its updates installs it in a new folder.
    /// </summary>
    public async Task<int> RefreshIconsAsync()
    {
        var items = IconItems().Select(it => (Item: it, it.Cfg.Path, it.Cfg.Icon, it.ExePath)).ToList();
        bool clickOnce = DateTime.UtcNow - lastClickOnceCheck > TimeSpan.FromMinutes(5);
        if (clickOnce) lastClickOnceCheck = DateTime.UtcNow;
        var read = await Task.Run(() => items.Select(x =>
        {
            string exe = x.ExePath;
            if (clickOnce && x.Path != null && x.Path.EndsWith(".appref-ms", StringComparison.OrdinalIgnoreCase)) exe = Shell.ResolveTarget(x.Path);
            return (x.Item, Exe: exe, Sig: Shell.IconSignature(x.Path, x.Icon, exe));
        }).ToList());
        if (exiting) return 0;
        var sigs = new Dictionary<DockItem, string>();
        foreach (var r in read)
        {
            // Moved to another folder: its windows are recognized by the new executable, and its icon read again.
            if (!string.Equals(r.Exe, r.Item.ExePath, StringComparison.OrdinalIgnoreCase)) { r.Item.ExePath = r.Exe; r.Item.IconSig = null; }
            sigs[r.Item] = r.Sig;
        }
        int n = ApplyIcons(sigs);
        if (clickOnce) RefreshRunning();
        return n;
    }

    /// <summary>Every icon that is read from a file: the ordinary icons of every list and the shortcuts of their stacks.</summary>
    private List<DockItem> IconItems()
    {
        var all = new List<DockItem>();
        void Add(IEnumerable<DockItem> items)
        {
            foreach (var it in items)
                if (it.Children != null) Add(it.Children); else all.Add(it);
        }
        Add(Items.ToList());
        foreach (var list in monitorLists.Values) Add(list.ToList());
        return all;
    }

    /// <summary>Reads again the icons whose signature changed (items that left the dock meanwhile are skipped).</summary>
    private int ApplyIcons(Dictionary<DockItem, string> sigs)
    {
        int n = 0;
        bool Walk(IEnumerable<DockItem> items)
        {
            bool any = false;
            foreach (var it in items)
            {
                if (it.Children != null)
                {
                    if (Walk(it.Children) && string.IsNullOrWhiteSpace(it.Cfg.Icon)) it.Icon = DockItem.StackIcon(it.Children.Select(c => c.Icon));
                    continue;
                }
                if (!sigs.TryGetValue(it, out var sig) || sig == it.IconSig) continue;
                var fresh = DockItem.From(it.Cfg);
                it.Icon = fresh.Icon; it.IconSig = fresh.IconSig; it.ExePath = fresh.ExePath;
                n++; any = true;
            }
            return any;
        }
        Walk(Items.ToList());
        foreach (var list in monitorLists.Values) Walk(list.ToList());
        if (n > 0) foreach (var w in windows) w.View.InvalidateVisual();
        return n;
    }

    /// <summary>
    /// Test screenshots: example apps and a stack, instead of the user's own; never saved. Only programs that come
    /// with every Windows, so the picture says nothing about what the machine that takes it has installed.
    /// </summary>
    public void UseSample()
    {
        string sys = Environment.SystemDirectory, win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        List<DockItemConfig> Existing(params string[] paths) =>
            paths.Where(File.Exists).Select(p => new DockItemConfig { Name = "", Path = p }).ToList();
        Config.Edge = DockEdge.Bottom;
        Config.Items = Existing(Path.Combine(win, "explorer.exe"), Path.Combine(pf86, @"Microsoft\Edge\Application\msedge.exe"),
                                Path.Combine(sys, "notepad.exe"), Path.Combine(sys, "calc.exe"), Path.Combine(sys, "Taskmgr.exe"),
                                Path.Combine(sys, @"WindowsPowerShell\v1.0\powershell.exe"));
        var tools = Existing(Path.Combine(sys, "notepad.exe"), Path.Combine(sys, "calc.exe"), Path.Combine(sys, "Taskmgr.exe"),
                             Path.Combine(sys, @"WindowsPowerShell\v1.0\powershell.exe"));
        Config.Items.Insert(Math.Min(2, Config.Items.Count), new DockItemConfig { Name = Loc.tr("Utilidades", "Utilities", "ユーティリティ"), Stack = tools });
        Items.Clear();
        foreach (var c in Config.Items) Items.Add(DockItem.From(c));
        // Fixed "open" dots, not those of the apps that happen to be open on the machine that takes the picture.
        sample = true;
        // The settings window lists two example screens, not those of the machine.
        Monitors.Sample = new List<MonitorInfo>
        {
            new() { Device = @"\\.\DISPLAY1", Bounds = new Native.RECT(0, 0, 2560, 1440), Work = new Native.RECT(0, 0, 2560, 1400), Primary = true, Dpi = 96 },
            new() { Device = @"\\.\DISPLAY2", Bounds = new Native.RECT(2560, 0, 4480, 1080), Work = new Native.RECT(2560, 0, 4480, 1040), Dpi = 96 },
        };
        for (int i = 0; i < Items.Count; i++) Items[i].Running = i is 0 or 2;
    }

    private bool sample;

    /// <summary>Changes the appearance ("auto", "light" or "dark") and the accent colour, and saves them.</summary>
    public void SetTheme(string appearance, string accent)
    {
        Config.Appearance = appearance;
        Config.Accent = accent;
        SaveConfig();
        Theme.Set(appearance, accent);
    }

    /// <summary>
    /// Test screenshot of the right-click menu of the first icon. A menu only opens as a popup, so its items
    /// are laid out in a window inside the same rounded frame; submenus show no arrow there.
    /// </summary>
    private Window MenuPreview(DockItem stack = null)
    {
        var dock = new DockWindow(this, null, null);
        var menu = stack != null ? dock.BuildStackMenu(stack) : dock.BuildMenu(Items.FirstOrDefault(), 0);
        var items = menu.Items.Cast<object>().ToList();
        menu.Items.Clear();
        var list = new System.Windows.Controls.StackPanel();
        foreach (var it in items)
        {
            if (it is System.Windows.Controls.Separator sep) sep.SetResourceReference(FrameworkElement.StyleProperty, System.Windows.Controls.MenuItem.SeparatorStyleKey);
            list.Children.Add((UIElement)it);
        }
        var frame = new System.Windows.Controls.Border { Child = list };
        frame.SetResourceReference(FrameworkElement.StyleProperty, "Mu.PopupFrame");
        var w = new Window { SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize, Content = frame };
        Theme.Attach(w);
        return w;
    }

    /// <summary>Creates or closes windows according to the monitor option and the connected monitors.</summary>
    public void Rebuild()
    {
        var all = Monitors.All();
        var targets = Config.AllMonitors && !IsScreenshot
            ? all.Select(m => m.Device).ToList()
            : new List<string> { null };
        if (targets.Count == 0) targets.Add(null);
        // With its own icons per screen, each window shows the list of the screen it is on.
        var docks = targets.Select(t => DockFor(Monitors.Pick(t ?? Config.Monitor), all)).ToList();
        string key = string.Join("|", targets.Select((t, i) => (t ?? "*") + (docks[i] == null ? "" : "@" + docks[i].Id + docks[i].Device)));

        if (key == monitorKey && windows.Count == targets.Count)
        {
            ApplyAll();
            return;
        }
        monitorKey = key;

        foreach (var w in windows) w.CloseQuietly();
        windows.Clear();
        for (int i = 0; i < targets.Count; i++)
        {
            var w = new DockWindow(this, screenshotPath, targets[i], docks[i]);
            windows.Add(w);
            w.Show();
        }
        Application.Current.MainWindow = windows[0];
    }

    /// <summary>
    /// The entry of the icons of a screen when every screen has its own (null otherwise, or without a screen). A
    /// screen seen for the first time gets a copy of the shared icons; a known one is updated with its current name
    /// and resolution.
    /// </summary>
    public MonitorDockConfig DockFor(MonitorInfo mon, IEnumerable<MonitorInfo> connected = null)
    {
        if (!Config.PerMonitor || mon == null) return null;
        return DockFor(mon.Id, mon.Device, mon.Bounds.Width, mon.Bounds.Height,
                       (connected ?? Monitors.All()).Select(m => m.Id).Where(id => !string.IsNullOrEmpty(id)).ToList());
    }

    /// <summary>The same, from what identifies a screen (the self-test uses it with made-up screens).</summary>
    public MonitorDockConfig DockFor(string id, string device, int width, int height, ICollection<string> connectedIds)
    {
        if (!Config.PerMonitor) return null;
        var dock = MonitorDockConfig.Find(Config.MonitorDocks, id, device, width, height, connectedIds);
        bool changed = false;
        if (dock == null)
        {
            dock = new MonitorDockConfig { Items = Config.Items.Select(c => c.Clone()).ToList() };
            Config.MonitorDocks.Add(dock);
            changed = true;
        }
        if ((dock.Id != (id ?? "") && !string.IsNullOrEmpty(id)) || dock.Device != (device ?? "") || dock.Width != width || dock.Height != height)
        {
            if (!string.IsNullOrEmpty(id)) dock.Id = id;
            dock.Device = device ?? ""; dock.Width = width; dock.Height = height;
            changed = true;
        }
        if (changed) SaveConfig();
        return dock;
    }

    /// <summary>The live icons of a dock: those of its screen, or the shared ones (null).</summary>
    public List<DockItem> ListFor(MonitorDockConfig dock)
    {
        if (dock == null) return Items;
        if (!monitorLists.TryGetValue(dock, out var list))
        {
            list = dock.Items.Select(DockItem.From).ToList();
            monitorLists[dock] = list;
        }
        return list;
    }

    /// <summary>The lists shown right now (each one once), or the shared one if no dock is open.</summary>
    private List<List<DockItem>> ActiveLists()
    {
        var lists = windows.Select(w => w.Items).Distinct().ToList();
        if (lists.Count == 0) lists.Add(Items);
        return lists;
    }

    /// <summary>
    /// The lists the settings window edits, with the screen each one belongs to: one per connected screen when each
    /// has its own icons, otherwise only the shared one (screen null).
    /// </summary>
    public List<(MonitorInfo Monitor, List<DockItem> Items)> EditableLists()
    {
        var result = new List<(MonitorInfo, List<DockItem>)>();
        if (Config.PerMonitor)
        {
            var all = Monitors.All();
            foreach (var m in all) result.Add((m, ListFor(DockFor(m, all))));
        }
        if (result.Count == 0) result.Add((null, Items));
        return result;
    }

    /// <summary>Deferred rebuild (e.g., when a monitor is connected or disconnected).</summary>
    public void ScheduleRebuild()
    {
        rebuildTimer.Stop();
        rebuildTimer.Start();
    }

    public int WindowCount => windows.Count;

    public void ApplyAll()
    {
        foreach (var w in windows) w.ApplyPosition();
    }

    public void KickAll()
    {
        // The thickness depends on the effective icon size, which changes with the number of apps.
        foreach (var w in windows) { w.ApplyPosition(); w.View.Kick(); }
    }

    /// <summary>Saves the configuration shortly (changes in quick succession are written once).</summary>
    public void SaveConfig()
    {
        if (IsScreenshot) return;
        saveTimer.Stop();
        saveTimer.Start();
    }

    /// <summary>Writes a pending configuration change right away.</summary>
    public void FlushConfig()
    {
        if (!saveTimer.IsEnabled) return;
        saveTimer.Stop();
        Config.Save();
    }

    /// <summary>Stores the order and content of a list of icons (the shared one by default).</summary>
    public void SaveItems(List<DockItem> list = null)
    {
        list ??= Items;
        // Pinned apps always come before unpinned running apps.
        var ordered = list.Where(i => !i.Transient).Concat(list.Where(i => i.Transient)).ToList();
        list.Clear();
        list.AddRange(ordered);
        var stored = list.Where(i => !i.Transient).Select(i => i.Cfg).ToList();
        if (list == Items) Config.Items = stored;
        else
        {
            var dock = monitorLists.FirstOrDefault(p => p.Value == list).Key;
            if (dock != null) dock.Items = stored;
            else { Program.Log("A list of icons that belongs to no dock was not saved."); return; }
        }
        SaveConfig();
    }

    /// <summary>
    /// Whether a list of icons is one the docks use with the current option: a screen's own list when each screen has
    /// its own icons, the shared one otherwise. With no dock open (the self-test) every list is.
    /// </summary>
    public bool IsShown(List<DockItem> list) =>
        windows.Count == 0 || (Config.PerMonitor ? monitorLists.ContainsValue(list) : list == Items);

    /// <summary>Number of pinned apps of a list (they are at its start).</summary>
    public static int PinnedCountOf(List<DockItem> list) => list.Count(i => !i.Transient);

    /// <summary>
    /// Adds files, programs or shortcuts to a list at a position among its pinned icons. One already there is not
    /// repeated; if it was an unpinned running app, it becomes pinned.
    /// </summary>
    public void AddPaths(List<DockItem> list, IEnumerable<string> paths, int index)
    {
        index = Math.Clamp(index, 0, PinnedCountOf(list));
        foreach (var raw in paths)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            string p = raw.Trim();
            var existing = list.FirstOrDefault(i => !i.IsStack && string.Equals(i.Cfg.Path, p, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                if (existing.Transient) { existing.Transient = false; list.Remove(existing); list.Insert(Math.Min(index++, PinnedCountOf(list)), existing); }
                continue;
            }
            var cfg = new DockItemConfig { Name = DockItem.NiceName(p), Path = p };
            list.Insert(index++, DockItem.From(cfg));
        }
        SaveItems(list);
        RefreshRunning();
        KickAll();
    }

    /// <summary>Puts a stack (new or edited) in a list: it replaces <paramref name="old"/>, or goes at <paramref name="index"/>.</summary>
    public DockItem PutStack(List<DockItem> list, DockItemConfig stack, DockItem old, int index)
    {
        var item = DockItem.From(stack);
        int at = old != null ? list.IndexOf(old) : -1;
        if (at >= 0) list[at] = item;
        else list.Insert(Math.Clamp(index, 0, PinnedCountOf(list)), item);
        SaveItems(list);
        KickAll();
        return item;
    }

    /// <summary>Moves a pinned icon into a stack of the same list (at its end).</summary>
    public void MoveIntoStack(List<DockItem> list, DockItem item, DockItem stack)
    {
        if (item.IsStack || !stack.IsStack || item.Transient) return;
        var cfg = stack.Cfg.Clone();
        cfg.Stack.Add(item.Cfg.Clone());
        list.Remove(item);
        PutStack(list, cfg, stack, 0);
    }

    /// <summary>
    /// Scans the open windows once and updates every dock: unpinned running apps, running indicators.
    /// </summary>
    public void RefreshRunning()
    {
        if (sample) return;
        Dictionary<string, List<IntPtr>> map;
        try { map = WindowTracker.Snapshot(); } catch { return; }
        bool listChanged = false, dotsChanged = false;
        foreach (var list in ActiveLists())
        {
            listChanged |= SyncOpenApps(list);
            foreach (var it in list.Concat(list.Where(i => i.Children != null).SelectMany(i => i.Children)))
            {
                bool run = it.ExePath != null && map.ContainsKey(WindowTracker.Normalize(it.ExePath));
                if (run != it.Running) { it.Running = run; dotsChanged = true; }
            }
        }
        if (listChanged) KickAll();
        else if (dotsChanged) foreach (var w in windows) w.View.InvalidateVisual();
    }

    /// <summary>
    /// Appends to the end of a dock the running apps that are not pinned, and removes those that have since closed.
    /// Uses the windows from the last <see cref="WindowTracker.Snapshot"/>. Returns true if the list changed. An app
    /// kept in a stack counts as pinned.
    /// </summary>
    private bool SyncOpenApps(List<DockItem> list)
    {
        bool changed = false;
        var open = Config.ShowRunningApps && !IsScreenshot ? WindowTracker.LastApps : new List<WindowTracker.OpenApp>();

        bool Covers(DockItem it, WindowTracker.OpenApp app)
        {
            if (it.ExePath == null) return false;
            string key = WindowTracker.Normalize(it.ExePath);
            return string.Equals(key, app.Launch, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, app.Path, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, app.Aumid, StringComparison.OrdinalIgnoreCase);
        }

        bool Pinned(WindowTracker.OpenApp app) =>
            list.Any(it => Covers(it, app) || (it.Children != null && it.Children.Any(c => Covers(c, app))));

        // Remove those that no longer have windows.
        for (int i = list.Count - 1; i >= 0; i--)
            if (list[i].Transient && !open.Any(a => Covers(list[i], a))) { list.RemoveAt(i); changed = true; }

        // Add the new ones, in the order they appear.
        foreach (var app in open)
        {
            if (Pinned(app)) continue;
            string path = app.Launch.StartsWith(WindowTracker.AumidPrefix, StringComparison.Ordinal)
                ? @"shell:AppsFolder\" + app.Launch.Substring(WindowTracker.AumidPrefix.Length)
                : app.Launch;
            var it = DockItem.From(new DockItemConfig { Name = DockItem.NiceName(path), Path = path });
            it.Transient = true;
            it.Running = true;
            list.Add(it);
            changed = true;
        }
        return changed;
    }

    /// <summary>Pins an unpinned running app to the dock, after the last pinned one.</summary>
    public void Pin(DockItem it)
    {
        var list = ActiveLists().FirstOrDefault(l => l.Contains(it)) ?? Items;
        it.Transient = false;
        list.Remove(it);
        list.Insert(PinnedCountOf(list), it);
        SaveItems(list);
        KickAll();
    }

    /// <summary>Called after an option is modified: saves the configuration and repositions the docks.</summary>
    public void Changed()
    {
        SaveConfig();
        Rebuild();
        KickAll();
    }

    /// <summary>Turns the icon lock on or off (from the dock menu or the settings window).</summary>
    public void SetLocked(bool locked)
    {
        if (Config.LockItems == locked) return;
        Config.LockItems = locked;
        SaveConfig();
        // An open settings window follows at once: its switch and the buttons that change the icons.
        settings?.LockChanged();
    }

    /// <summary>Changes the interface language; open windows rebuild their texts.</summary>
    public void SetLanguage(UiLanguage language)
    {
        Config.Language = Loc.Code(language);
        SaveConfig();
        Loc.Current = language;
        foreach (var w in windows) w.View.InvalidateVisual();
    }

    /// <summary>Opens the settings window (or brings it to the front if already open).</summary>
    public void OpenSettings()
    {
        if (settings != null)
        {
            settings.Activate();
            return;
        }
        settings = new SettingsWindow(this);
        settings.Closed += (s, e) => settings = null;
        settings.Show();
        settings.Activate();
    }

    /// <summary>Opens the "About Muelle" window (or brings it to the front if already open).</summary>
    public void OpenAbout()
    {
        if (about != null) { about.Activate(); return; }
        about = new AboutWindow(this);
        about.Closed += (s, e) => about = null;
        about.Show();
        about.Activate();
    }

    /// <summary>Adds applications via the file dialog to a list (the shared one by default), at the end of its pinned ones.</summary>
    public void AddWithDialog(List<DockItem> list = null)
    {
        list ??= Items;
        if (windows.Count > 0) windows[0].AddWithDialog(PinnedCountOf(list), list);
    }

    private bool exiting;

    public void Exit()
    {
        if (exiting) return;
        exiting = true;
        runningTimer.Stop();
        updateTimer?.Stop();
        iconTimer?.Stop();
        FlushConfig();
        settings?.Close();
        about?.Close();
        foreach (var w in windows) w.CloseQuietly();
        windows.Clear();
        Application.Current.Shutdown();
    }

    /// <summary>
    /// Test screenshot: renders a window's content to a PNG file. With <paramref name="trim"/>, the fully
    /// transparent margins are cut off (the dock window is much larger than the dock it shows).
    /// </summary>
    public static void SaveScreenshot(Window window, string path, bool trim = false)
    {
        try
        {
            var content = (FrameworkElement)window.Content;
            var dpi = VisualTreeHelper.GetDpi(content);
            // The content is drawn at its margin offset, so the image includes the margins.
            double width = content.ActualWidth + content.Margin.Left + content.Margin.Right;
            double height = content.ActualHeight + content.Margin.Top + content.Margin.Bottom;
            int w = Math.Max(1, (int)Math.Ceiling(width * dpi.DpiScaleX));
            int h = Math.Max(1, (int)Math.Ceiling(height * dpi.DpiScaleY));
            var rtb = new RenderTargetBitmap(w, h, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            // Windows draw their background on the window itself, not on the content: paint it first.
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
                dc.DrawRectangle(window.Background ?? Brushes.Transparent, null, new Rect(0, 0, width, height));
            rtb.Render(visual);
            rtb.Render(content);
            BitmapSource image = rtb;
            if (trim) image = TrimTransparent(rtb, 12);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(image));
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using var fs = File.Create(path);
            enc.Save(fs);
        }
        catch (Exception ex) { Program.Log("Could not save the screenshot: " + ex); }
    }

    /// <summary>Test screenshot of one part of a window, on the window's background, with a small margin around it.</summary>
    private static void SaveScreenshot(FrameworkElement part, Brush background, string path)
    {
        try
        {
            const double margin = 10;
            var dpi = VisualTreeHelper.GetDpi(part);
            double width = part.ActualWidth + 2 * margin, height = part.ActualHeight + 2 * margin;
            var rtb = new RenderTargetBitmap((int)Math.Ceiling(width * dpi.DpiScaleX), (int)Math.Ceiling(height * dpi.DpiScaleY),
                                             dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            // Drawn through a brush of the part itself: rendering the part directly would place it at its offset in the window.
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(background ?? Brushes.Transparent, null, new Rect(0, 0, width, height));
                dc.DrawRectangle(new VisualBrush(part) { Stretch = Stretch.None }, null, new Rect(margin, margin, part.ActualWidth, part.ActualHeight));
            }
            rtb.Render(visual);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using var fs = File.Create(path);
            enc.Save(fs);
        }
        catch (Exception ex) { Program.Log("Could not save the screenshot: " + ex); }
    }

    private SettingsWindow LockedForScreenshot()
    {
        Config.LockItems = true;
        return new SettingsWindow(this);
    }

    /// <summary>The smallest part of the image that holds every visible pixel, plus a margin.</summary>
    private static BitmapSource TrimTransparent(BitmapSource src, int margin)
    {
        int w = src.PixelWidth, h = src.PixelHeight, stride = w * 4;
        var px = new byte[stride * h];
        src.CopyPixels(px, stride, 0);
        int left = w, top = h, right = -1, bottom = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[y * stride + x * 4 + 3] > 8)
                {
                    if (x < left) left = x;
                    if (x > right) right = x;
                    if (y < top) top = y;
                    if (y > bottom) bottom = y;
                }
        if (right < 0) return src;
        left = Math.Max(0, left - margin); top = Math.Max(0, top - margin);
        right = Math.Min(w - 1, right + margin); bottom = Math.Min(h - 1, bottom + margin);
        return new CroppedBitmap(src, new Int32Rect(left, top, right - left + 1, bottom - top + 1));
    }
}
