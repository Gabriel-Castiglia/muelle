//
//  DockWindow.cs
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
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using static Muelle.Loc;

namespace Muelle;

/// <summary>
/// The dock window: registers as an AppBar to reserve its strip of screen,
/// positions itself on the chosen monitor and edge, and offers the right-click menu.
/// </summary>
internal sealed class DockWindow : Window
{
    public DockManager Manager { get; }
    public DockConfig Config => Manager.Config;
    public List<DockItem> Items => Manager.Items;
    public DockView View { get; }
    public bool MenuOpen { get; private set; }
    public bool KeepAnimating => screenshotPath != null;

    private readonly string screenshotPath;
    private IntPtr hwnd;
    private uint callbackMsg, taskbarCreatedMsg;
    private bool appBarRegistered, fullscreenAppActive;
    private string appBarKey = "";
    private Native.RECT lastSetRc;
    /// <summary>Monitor pinned to this window ("all screens" mode); null = whichever the configuration specifies.</summary>
    private readonly string monitorDevice;
    private readonly DispatcherTimer repositionTimer;
    private uint shellHookMsg;
    private int frames;
    private bool shotTaken, closingQuietly;

    public DockWindow(DockManager manager, string screenshotPath, string monitorDevice)
    {
        Manager = manager;
        this.screenshotPath = screenshotPath;
        this.monitorDevice = monitorDevice;

        Title = "Muelle";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = 0; Top = 0; Width = 80; Height = 400;
        UseLayoutRounding = true;

        View = new DockView(this);
        Content = View;
        if (screenshotPath != null) View.DemoHoverIndex = Math.Min(2, Math.Max(0, Items.Count - 1));

        repositionTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
        repositionTimer.Tick += (s, e) => { repositionTimer.Stop(); ApplyPosition(); };

        SourceInitialized += OnSourceInitialized;
        Loaded += (s, e) =>
        {
            View.Kick();
            if (screenshotPath != null)
            {
                // If frame-by-frame rendering never starts, the screenshot is taken anyway.
                var fallback = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
                fallback.Tick += (s2, e2) => { fallback.Stop(); TakeScreenshot(); };
                fallback.Start();
            }
        };
        // A close that does not come from the dock itself (the installer replacing Muelle, Alt+F4) quits the
        // whole program: with several monitors there are several windows, and a lone process without windows
        // would keep the executable in use.
        Closing += (s, e) =>
        {
            if (closingQuietly) return;
            e.Cancel = true;
            Dispatcher.BeginInvoke(new Action(Manager.Exit));
        };
        Closed += (s, e) => RemoveAppBar();
    }

    // ---------------- Window / AppBar ----------------
    private void OnSourceInitialized(object sender, EventArgs e)
    {
        hwnd = new WindowInteropHelper(this).Handle;
        long ex = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE).ToInt64();
        ex |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
        ex &= ~Native.WS_EX_APPWINDOW;
        Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE, new IntPtr(ex));

        callbackMsg = Native.RegisterWindowMessage("Muelle.AppBarMessage");
        taskbarCreatedMsg = Native.RegisterWindowMessage("TaskbarCreated");
        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
        shellHookMsg = Native.RegisterWindowMessage("SHELLHOOK");
        Native.RegisterShellHookWindow(hwnd);
        ApplyPosition();
    }

    /// <summary>Rectangle of the icon the window animates toward when minimized, if its app is in this dock.</summary>
    private Native.RECT? MinimizeTarget(IntPtr appWindow)
    {
        try
        {
            if (Config.AutoHide || appWindow == IntPtr.Zero) return null; // hidden, no visible icon
            // With one dock per screen, only the dock on the window's screen answers.
            if (Manager.WindowCount > 1)
            {
                var mon = Monitors.Pick(monitorDevice ?? Config.Monitor);
                if (mon == null || Native.MonitorFromWindow(appWindow, 2 /* MONITOR_DEFAULTTONEAREST */) != mon.Handle) return null;
            }
            var keys = WindowTracker.KeysFor(appWindow);
            var item = Items.FirstOrDefault(it => it.ExePath != null &&
                keys.Any(k => string.Equals(k, WindowTracker.Normalize(it.ExePath), StringComparison.OrdinalIgnoreCase)));
            return item == null ? null : View.IconScreenRect(item);
        }
        catch { return null; }
    }

    private IntPtr WndProc(IntPtr h, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (shellHookMsg != 0 && msg == (int)shellHookMsg)
        {
            // HSHELL_GETMINRECT: Windows asks where to animate a window when it is minimized.
            // We answer with the app's icon in the dock (instead of its taskbar button).
            if ((wParam.ToInt32() & 0x7FFF) == 5 && lParam != IntPtr.Zero && MinimizeTarget(Marshal.ReadIntPtr(lParam)) is Native.RECT rc)
            {
                Marshal.StructureToPtr(rc, lParam + IntPtr.Size, false);
                handled = true;
                return new IntPtr(1);
            }
            return IntPtr.Zero;
        }
        if (callbackMsg != 0 && msg == (int)callbackMsg)
        {
            switch (wParam.ToInt32())
            {
                case Native.ABN_POSCHANGED:
                    ScheduleReposition();
                    break;
                case Native.ABN_FULLSCREENAPP:
                    // Fullscreen app (video, game): the dock goes to the background.
                    fullscreenAppActive = lParam != IntPtr.Zero;
                    Native.SetWindowPos(hwnd, fullscreenAppActive ? Native.HWND_NOTOPMOST : Native.HWND_TOPMOST, 0, 0, 0, 0,
                        Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
                    if (fullscreenAppActive) Native.SetWindowPos(hwnd, new IntPtr(1) /* HWND_BOTTOM */, 0, 0, 0, 0,
                        Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
                    break;
            }
            handled = true;
        }
        else if (taskbarCreatedMsg != 0 && msg == (int)taskbarCreatedMsg)
        {
            // Windows Explorer restarted: reserve the space again.
            appBarRegistered = false;
            ScheduleReposition();
        }
        else if (msg == Native.WM_DISPLAYCHANGE || msg == Native.WM_DPICHANGED)
        {
            ScheduleReposition();
            if (msg == Native.WM_DISPLAYCHANGE) Manager.ScheduleRebuild();
        }
        else if (msg == Native.WM_ACTIVATE && appBarRegistered)
        {
            var abd = NewAbd();
            Native.SHAppBarMessage(Native.ABM_ACTIVATE, ref abd);
        }
        else if (msg == Native.WM_WINDOWPOSCHANGED && appBarRegistered)
        {
            var abd = NewAbd();
            Native.SHAppBarMessage(Native.ABM_WINDOWPOSCHANGED, ref abd);
        }
        return IntPtr.Zero;
    }

    private Native.APPBARDATA NewAbd() => new()
    {
        cbSize = Marshal.SizeOf<Native.APPBARDATA>(),
        hWnd = hwnd,
    };

    private void ScheduleReposition()
    {
        repositionTimer.Stop();
        repositionTimer.Start();
    }

    private void RemoveAppBar()
    {
        if (!appBarRegistered || hwnd == IntPtr.Zero) return;
        var abd = NewAbd();
        Native.SHAppBarMessage(Native.ABM_REMOVE, ref abd);
        appBarRegistered = false;
        appBarKey = "";
        lastSetRc = default;
    }

    /// <summary>Closes the window, first releasing the reserved strip.</summary>
    public void CloseQuietly()
    {
        closingQuietly = true;
        RemoveAppBar();
        if (hwnd != IntPtr.Zero) Native.DeregisterShellHookWindow(hwnd);
        Close();
    }

    /// <summary>Positions the dock on its monitor and edge, reserving the strip unless it auto-hides.</summary>
    public void ApplyPosition()
    {
        if (hwnd == IntPtr.Zero) return;
        var mon = Monitors.Pick(monitorDevice ?? Config.Monitor);
        if (mon == null) return;
        double s = mon.Scale;
        int thick = (int)Math.Ceiling(View.Thickness * s);
        int extent = (int)Math.Ceiling(View.CrossExtent * s);
        var b = mon.Bounds;
        bool vert = View.Vertical;

        // Along the edge: the whole monitor, minus the taskbar if it is on that monitor.
        int a0 = vert ? b.Top : b.Left, a1 = vert ? b.Bottom : b.Right;
        foreach (var tb in Monitors.Taskbars())
        {
            var i = Native.RECT.Intersect(tb, b);
            if (i.IsEmpty) continue;
            if (vert && i.Width > i.Height)
            {
                if (i.Top - b.Top > b.Bottom - i.Bottom) a1 = Math.Min(a1, i.Top); else a0 = Math.Max(a0, i.Bottom);
            }
            else if (!vert && i.Height > i.Width)
            {
                if (i.Left - b.Left > b.Right - i.Right) a1 = Math.Min(a1, i.Left); else a0 = Math.Max(a0, i.Right);
            }
        }
        if (a1 - a0 < 100) { a0 = vert ? b.Top : b.Left; a1 = vert ? b.Bottom : b.Right; }

        uint edge = Config.Edge switch
        {
            DockEdge.Left => Native.ABE_LEFT, DockEdge.Right => Native.ABE_RIGHT,
            DockEdge.Top => Native.ABE_TOP, _ => Native.ABE_BOTTOM,
        };
        var rc = Config.Edge switch
        {
            DockEdge.Left => new Native.RECT(b.Left, a0, b.Left + thick, a1),
            DockEdge.Right => new Native.RECT(b.Right - thick, a0, b.Right, a1),
            DockEdge.Top => new Native.RECT(a0, b.Top, a1, b.Top + thick),
            _ => new Native.RECT(a0, b.Bottom - thick, a1, b.Bottom),
        };

        bool reserve = Config.ReserveSpace && !Config.AutoHide && screenshotPath == null;
        string key = Config.Edge + "|" + mon.Device;
        if (reserve)
        {
            if (appBarRegistered && key != appBarKey) RemoveAppBar();
            if (!appBarRegistered)
            {
                var nb = NewAbd();
                nb.uCallbackMessage = callbackMsg;
                Native.SHAppBarMessage(Native.ABM_NEW, ref nb);
                appBarRegistered = true;
            }
            appBarKey = key;
            var abd = NewAbd();
            abd.uEdge = edge;
            abd.rc = rc;
            Native.SHAppBarMessage(Native.ABM_QUERYPOS, ref abd);
            switch (Config.Edge)
            {
                case DockEdge.Left: abd.rc.Right = abd.rc.Left + thick; break;
                case DockEdge.Right: abd.rc.Left = abd.rc.Right - thick; break;
                case DockEdge.Top: abd.rc.Bottom = abd.rc.Top + thick; break;
                default: abd.rc.Top = abd.rc.Bottom - thick; break;
            }
            // Only notify Windows if the strip changed: with several docks (one per monitor),
            // each ABM_SETPOS notifies the others and, without this check, they would reposition each other in a loop.
            if (!abd.rc.Equals(lastSetRc))
            {
                Native.SHAppBarMessage(Native.ABM_SETPOS, ref abd);
                lastSetRc = abd.rc;
            }
            rc = abd.rc;
        }
        else RemoveAppBar();

        int x, y, w, h;
        switch (Config.Edge)
        {
            case DockEdge.Left: x = rc.Left; y = rc.Top; w = extent; h = rc.Height; break;
            case DockEdge.Right: x = rc.Right - extent; y = rc.Top; w = extent; h = rc.Height; break;
            case DockEdge.Top: x = rc.Left; y = rc.Top; w = rc.Width; h = extent; break;
            default: x = rc.Left; y = rc.Bottom - extent; w = rc.Width; h = extent; break;
        }
        Native.SetWindowPos(hwnd, fullscreenAppActive ? Native.HWND_NOTOPMOST : Native.HWND_TOPMOST, x, y, w, h, Native.SWP_NOACTIVATE);
        View.InvalidateVisual();
        View.Kick();
    }

    // ---------------- Launching and ending apps ----------------
    /// <summary>Click on an icon: brings its window to the front (or minimizes it), or launches the app with a bounce.</summary>
    public void ActivateItem(DockItem it, bool newInstance)
    {
        if (!newInstance && it.ExePath != null)
        {
            var map = WindowTracker.Snapshot();
            if (map.TryGetValue(WindowTracker.Normalize(it.ExePath), out var wins) && wins.Count > 0)
            {
                IntPtr fg = Native.GetForegroundWindow();
                int at = wins.IndexOf(fg);
                if (at >= 0 && wins.Count == 1) { Native.ShowWindow(fg, Native.SW_MINIMIZE); return; }
                IntPtr target = at >= 0 ? wins[(at + 1) % wins.Count] : wins[0];
                if (Native.IsIconic(target)) Native.ShowWindow(target, Native.SW_RESTORE);
                Native.SetForegroundWindow(target);
                return;
            }
        }
        Launch(it, false);
    }

    private void Launch(DockItem it, bool asAdmin)
    {
        try
        {
            var psi = new ProcessStartInfo(it.Cfg.Path) { UseShellExecute = true };
            if (!string.IsNullOrEmpty(it.Cfg.Arguments)) psi.Arguments = it.Cfg.Arguments;
            if (!it.Cfg.Path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var dir = Path.GetDirectoryName(it.Cfg.Path);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) psi.WorkingDirectory = dir;
            }
            if (asAdmin) psi.Verb = "runas";
            Process.Start(psi);
            View.StartBounce(it);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // The user canceled the administrator permission prompt.
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, tr("No se pudo abrir «", "Could not open \"") + it.Name + tr("».", "\".") + "\n\n" + ex.Message, "Muelle",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>"End task" (as in the taskbar): forcibly terminates the app's processes.</summary>
    private void EndTask(DockItem it)
    {
        try
        {
            string key = WindowTracker.Normalize(it.ExePath);
            var map = WindowTracker.Snapshot();
            map.TryGetValue(key, out var wins);
            foreach (var pid in WindowTracker.ProcessesFor(key, wins ?? new List<IntPtr>()))
            {
                try { using var p = Process.GetProcessById((int)pid); p.Kill(); }
                catch (Exception ex) { Program.Log("End task: could not terminate process " + pid + ": " + ex.Message); }
            }
        }
        catch (Exception ex) { Program.Log("End task: " + ex.Message); }
        Manager.RefreshRunning();
    }

    /// <summary>True if "End task" would terminate something (windows hosted by Explorer, such as Control Panel, are excluded).</summary>
    private bool HasKillableProcess(DockItem it)
    {
        try
        {
            string key = WindowTracker.Normalize(it.ExePath);
            WindowTracker.Snapshot().TryGetValue(key, out var wins);
            return WindowTracker.ProcessesFor(key, wins ?? new List<IntPtr>()).Count > 0;
        }
        catch { return false; }
    }

    // ---------------- Editing the list ----------------
    public void AddPaths(IEnumerable<string> paths, int index)
    {
        index = Math.Clamp(index, 0, Manager.PinnedCount);
        foreach (var raw in paths)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            string p = raw.Trim();
            var existing = Items.FirstOrDefault(i => string.Equals(i.Cfg.Path, p, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                if (existing.Transient) { existing.Transient = false; Items.Remove(existing); Items.Insert(Math.Min(index++, Manager.PinnedCount), existing); }
                continue;
            }
            var cfg = new DockItemConfig { Name = DockItem.NiceName(p), Path = p };
            Items.Insert(index++, DockItem.From(cfg));
        }
        Manager.SaveItems();
        Manager.RefreshRunning();
        Manager.KickAll();
    }

    public void RemoveItem(DockItem it)
    {
        if (it.Transient) return; // an unpinned running app goes away by itself when it closes
        Items.Remove(it);
        Manager.SaveItems();
        Manager.KickAll();
    }

    public void MoveItem(DockItem it, int index)
    {
        // Dragging an unpinned running app into the pinned ones pins it (as in macOS).
        if (it.Transient && index <= Manager.PinnedCount) it.Transient = false;
        Items.Remove(it);
        Items.Insert(Math.Clamp(index, 0, Items.Count), it);
        Manager.SaveItems();
        Manager.KickAll();
    }

    public void AddWithDialog(int index)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = tr("Agregar al dock", "Add to the dock"),
            Filter = tr("Programas y accesos directos", "Programs and shortcuts") + "|*.exe;*.lnk;*.url;*.bat;*.cmd|"
                   + tr("Todos los archivos", "All files") + "|*.*",
            Multiselect = true,
            DereferenceLinks = false,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
        };
        Native.SetForegroundWindow(hwnd);
        if (dlg.ShowDialog(this) == true) AddPaths(dlg.FileNames, index);
    }

    // ---------------- Right-click menu ----------------
    /// <summary>Short menu: actions on the icon, adding apps, and access to the settings window.</summary>
    public void ShowMenu(DockItem item, int insertAt)
    {
        var menu = new ContextMenu { Placement = PlacementMode.MousePoint };

        if (item != null)
        {
            menu.Items.Add(new MenuItem { Header = item.Name, IsEnabled = false, FontWeight = FontWeights.SemiBold });
            menu.Items.Add(Cmd(item.Running ? tr("Mostrar", "Show") : tr("Abrir", "Open"), () => ActivateItem(item, false)));
            menu.Items.Add(Cmd(tr("Abrir otra ventana", "Open another window"), () => Launch(item, false)));
            menu.Items.Add(Cmd(tr("Ejecutar como administrador", "Run as administrator"), () => Launch(item, true)));
            menu.Items.Add(Cmd(tr("Abrir ubicación del archivo", "Open file location"), () => OpenLocation(item)));
            if (item.Running && item.ExePath != null && HasKillableProcess(item))
                menu.Items.Add(Cmd(tr("Finalizar tarea", "End task"), () => EndTask(item)));
            if (item.Transient) menu.Items.Add(Cmd(tr("Mantener en el dock", "Keep in dock"), () => Manager.Pin(item)));
            else menu.Items.Add(Cmd(tr("Quitar del dock", "Remove from dock"), () => RemoveItem(item)));
            menu.Items.Add(new Separator());
        }

        menu.Items.Add(Cmd(tr("Agregar aplicación…", "Add application…"), () => AddWithDialog(insertAt)));
        menu.Items.Add(new Separator());
        menu.Items.Add(Cmd(Config.Style == DockStyle.Panel
            ? tr("Cambiar a dock flotante (estilo Mac)", "Switch to floating dock (Mac style)")
            : tr("Cambiar a panel fijo (estilo Ubuntu)", "Switch to fixed panel (Ubuntu style)"), () =>
        {
            Config.Style = Config.Style == DockStyle.Panel ? DockStyle.Dock : DockStyle.Panel;
            Manager.Changed();
        }));
        var lockItem = Cmd(tr("Bloquear iconos", "Lock icons"), () => Manager.SetLocked(!Config.LockItems));
        lockItem.IsCheckable = true;
        lockItem.IsChecked = Config.LockItems;
        menu.Items.Add(lockItem);
        // The language menu is always labeled in both languages, so it can be found from either one.
        var language = new MenuItem { Header = "Idioma / Language" };
        foreach (var (lang, label) in new[] { (UiLanguage.En, "English"), (UiLanguage.Es, "Español") })
        {
            var mi = Cmd(label, () => Manager.SetLanguage(lang));
            mi.IsCheckable = true;
            mi.IsChecked = Loc.Current == lang;
            language.Items.Add(mi);
        }
        menu.Items.Add(language);
        menu.Items.Add(Cmd(tr("Configuración…", "Settings…"), () => Manager.OpenSettings()));
        menu.Items.Add(Cmd(tr("Acerca de Muelle…", "About Muelle…"), () => Manager.OpenAbout()));
        menu.Items.Add(new Separator());
        menu.Items.Add(Cmd(tr("Salir de Muelle", "Quit Muelle"), Manager.Exit));

        menu.Opened += (s, e) => { MenuOpen = true; View.Kick(); };
        menu.Closed += (s, e) => { MenuOpen = false; View.Kick(); };
        Native.SetForegroundWindow(hwnd);
        menu.PlacementTarget = View;
        menu.IsOpen = true;
    }

    private static MenuItem Cmd(string header, Action action)
    {
        var mi = new MenuItem { Header = header };
        mi.Click += (s, e) => action();
        return mi;
    }

    private static void OpenLocation(DockItem it)
    {
        try
        {
            string p = it.Cfg.Path;
            if (File.Exists(p) || Directory.Exists(p)) Process.Start("explorer.exe", "/select,\"" + p + "\"");
        }
        catch { }
    }

    // ---------------- Test screenshot (--screenshot) ----------------
    public void OnFrame()
    {
        if (screenshotPath == null || shotTaken) return;
        if (++frames >= 120)
            Dispatcher.BeginInvoke(new Action(TakeScreenshot), DispatcherPriority.ContextIdle);
    }

    private void TakeScreenshot()
    {
        if (shotTaken) return;
        shotTaken = true;
        DockManager.SaveScreenshot(this, screenshotPath, trim: true);
        Manager.Exit(); // with explicit shutdown, the application ends here
    }
}
