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
/// All share the same configuration, the same list of applications and a single scan of the open windows.
/// </summary>
internal sealed class DockManager
{
    public DockConfig Config { get; }
    public List<DockItem> Items { get; } = new();
    public bool IsScreenshot => screenshotPath != null;

    private readonly string screenshotPath;
    /// <summary>Window rendered by a test screenshot: "dock", "settings" or "about".</summary>
    private readonly string screenshotWindow;
    private readonly List<DockWindow> windows = new();
    private readonly DispatcherTimer rebuildTimer, runningTimer, saveTimer;
    private string monitorKey;
    private SettingsWindow settings;
    private AboutWindow about;

    public DockManager(string screenshotPath, string screenshotWindow)
    {
        this.screenshotPath = screenshotPath;
        this.screenshotWindow = screenshotWindow;
        Config = DockConfig.Load(out bool firstRun);
        Loc.Current = Loc.FromCode(Config.Language);
        if (screenshotPath == null)
        {
            Config.StartWithWindows = DockConfig.IsStartupEnabled();
            if (Config.StartWithWindows) DockConfig.SetStartup(true); // refreshes the path if the executable moved
            if (firstRun) Config.Save();
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
            Window w = screenshotWindow == "about" ? new AboutWindow() : new SettingsWindow(this);
            w.ContentRendered += (s, e) =>
                w.Dispatcher.BeginInvoke(new Action(() => { SaveScreenshot(w, screenshotPath); Exit(); }), DispatcherPriority.ContextIdle);
            w.Show();
            return;
        }
        Rebuild();
        RefreshRunning();
        runningTimer.Start();
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
    /// Scans the open windows once and updates every dock: unpinned running apps, running indicators.
    /// </summary>
    public void RefreshRunning()
    {
        Dictionary<string, List<IntPtr>> map;
        try { map = WindowTracker.Snapshot(); } catch { return; }
        bool listChanged = SyncOpenApps();
        bool dotsChanged = false;
        foreach (var it in Items)
        {
            bool run = it.ExePath != null && map.ContainsKey(WindowTracker.Normalize(it.ExePath));
            if (run != it.Running) { it.Running = run; dotsChanged = true; }
        }
        if (listChanged) KickAll();
        else if (dotsChanged) foreach (var w in windows) w.View.InvalidateVisual();
    }

    /// <summary>
    /// Appends to the end of the dock the running apps that are not pinned, and removes those that have since closed.
    /// Uses the windows from the last <see cref="WindowTracker.Snapshot"/>. Returns true if the list changed.
    /// </summary>
    private bool SyncOpenApps()
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

    /// <summary>Turns the icon lock on or off (from the dock menu or the settings window).</summary>
    public void SetLocked(bool locked)
    {
        if (Config.LockItems == locked) return;
        Config.LockItems = locked;
        SaveConfig();
        // Deferred: the change may come from a control of the settings window that is about to be replaced.
        var open = settings;
        open?.Dispatcher.BeginInvoke(new Action(open.Rebuild));
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
        about = new AboutWindow();
        about.Closed += (s, e) => about = null;
        about.Show();
        about.Activate();
    }

    /// <summary>Adds applications via the file dialog, at the end of the pinned ones.</summary>
    public void AddWithDialog()
    {
        if (windows.Count > 0) windows[0].AddWithDialog(PinnedCount);
    }

    private bool exiting;

    public void Exit()
    {
        if (exiting) return;
        exiting = true;
        runningTimer.Stop();
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
