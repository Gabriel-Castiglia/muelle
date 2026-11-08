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

using System.Windows;
using System.Windows.Threading;

namespace Muelle;

/// <summary>
/// Coordinates the dock windows: a single one (primary monitor or a specific one) or one per monitor.
/// All share the same configuration and the same list of applications.
/// </summary>
internal sealed class DockManager
{
    public DockConfig Config { get; }
    public List<DockItem> Items { get; } = new();
    public bool IsScreenshot => screenshotPath != null;

    private readonly string screenshotPath;
    private readonly List<DockWindow> windows = new();
    private readonly DispatcherTimer rebuildTimer;
    private string monitorKey;
    private SettingsWindow settings;

    public DockManager(string screenshotPath)
    {
        this.screenshotPath = screenshotPath;
        Config = DockConfig.Load(out bool firstRun);
        if (screenshotPath == null)
        {
            Config.StartWithWindows = DockConfig.IsStartupEnabled();
            if (Config.StartWithWindows) DockConfig.SetStartup(true); // refreshes the path if the executable moved
            if (firstRun) Config.Save();
        }
        foreach (var c in Config.Items) Items.Add(DockItem.From(c));

        rebuildTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(600) };
        rebuildTimer.Tick += (s, e) => { rebuildTimer.Stop(); Rebuild(); };
    }

    /// <summary>Creates or closes windows according to the monitor option and the connected monitors.</summary>
    public void Rebuild()
    {
        var targets = Config.AllMonitors && !IsScreenshot
            ? Monitors.All().Select(m => m.Device).ToList()
            : new List<string> { null };
        if (targets.Count == 0) targets.Add(null);
        string key = string.Join("|", targets.Select(t => t ?? "*"));

        if (key == monitorKey && windows.Count == targets.Count)
        {
            ApplyAll();
            return;
        }
        monitorKey = key;

        foreach (var w in windows) w.CloseQuietly();
        windows.Clear();
        foreach (var device in targets)
        {
            var w = new DockWindow(this, screenshotPath, device);
            windows.Add(w);
            w.Show();
        }
        Application.Current.MainWindow = windows[0];
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

    public void SaveConfig()
    {
        if (!IsScreenshot) Config.Save();
    }

    public void SaveItems()
    {
        // Pinned apps always come before unpinned running apps.
        var ordered = Items.Where(i => !i.Transient).Concat(Items.Where(i => i.Transient)).ToList();
        Items.Clear();
        Items.AddRange(ordered);
        Config.Items = Items.Where(i => !i.Transient).Select(i => i.Cfg).ToList();
        SaveConfig();
    }

    /// <summary>Number of pinned apps (they are at the start of <see cref="Items"/>).</summary>
    public int PinnedCount => Items.Count(i => !i.Transient);

    /// <summary>
    /// Appends to the end of the dock the running apps that are not pinned, and removes those that have since closed.
    /// Uses the windows from the last <see cref="WindowTracker.Snapshot"/>. Returns true if the list changed.
    /// </summary>
    public bool SyncOpenApps()
    {
        bool changed = false;
        var open = Config.ShowRunningApps && !IsScreenshot ? WindowTracker.LastApps : new List<WindowTracker.OpenApp>();

        bool Covers(DockItem it, WindowTracker.OpenApp app)
        {
            if (it.ExePath == null) return false;
            string key = it.ExePath.StartsWith(WindowTracker.AumidPrefix, StringComparison.Ordinal) ? it.ExePath : Normalize(it.ExePath);
            return string.Equals(key, app.Launch, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, app.Path, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, app.Aumid, StringComparison.OrdinalIgnoreCase);
        }

        // Remove those that no longer have windows.
        for (int i = Items.Count - 1; i >= 0; i--)
            if (Items[i].Transient && !open.Any(a => Covers(Items[i], a))) { Items.RemoveAt(i); changed = true; }

        // Add the new ones, in the order they appear.
        foreach (var app in open)
        {
            if (Items.Any(it => Covers(it, app))) continue;
            string path = app.Launch.StartsWith(WindowTracker.AumidPrefix, StringComparison.Ordinal)
                ? @"shell:AppsFolder\" + app.Launch.Substring(WindowTracker.AumidPrefix.Length)
                : app.Launch;
            var it = DockItem.From(new DockItemConfig { Name = DockItem.NiceName(path), Path = path });
            it.Transient = true;
            it.Running = true;
            Items.Add(it);
            changed = true;
        }
        return changed;
    }

    private static string Normalize(string p)
    {
        try { return System.IO.Path.GetFullPath(p); } catch { return p; }
    }

    /// <summary>Pins an unpinned running app to the dock, after the last pinned one.</summary>
    public void Pin(DockItem it)
    {
        it.Transient = false;
        Items.Remove(it);
        Items.Insert(PinnedCount, it);
        SaveItems();
        KickAll();
    }

    /// <summary>Called after an option is modified: saves the configuration and repositions the docks.</summary>
    public void Changed()
    {
        SaveConfig();
        Rebuild();
        KickAll();
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

    private AboutWindow about;

    /// <summary>Opens the "About Muelle" window (or brings it to the front if already open).</summary>
    public void OpenAbout()
    {
        if (about != null) { about.Activate(); return; }
        about = new AboutWindow();
        about.Closed += (s, e) => about = null;
        about.Show();
        about.Activate();
    }

    /// <summary>Adds applications via the file dialog, at the end of the list.</summary>
    public void AddWithDialog()
    {
        if (windows.Count > 0) windows[0].AddWithDialog(Items.Count);
    }

    public void Exit()
    {
        settings?.Close();
        foreach (var w in windows) w.CloseQuietly();
        windows.Clear();
        Application.Current.Shutdown();
    }
}
