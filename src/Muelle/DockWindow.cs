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
    /// <summary>The icons of this dock: those of its screen (when each screen has its own) or the shared ones.</summary>
    public List<DockItem> Items => Manager.ListFor(Dock);
    /// <summary>The entry of this dock's screen in the configuration; null = it shows the shared icons.</summary>
    public MonitorDockConfig Dock { get; }
    public DockView View { get; }
    public bool MenuOpen { get; private set; }
    public bool KeepAnimating => screenshotPath != null;

    private readonly string screenshotPath;
    private IntPtr hwnd;
    private uint callbackMsg, taskbarCreatedMsg;
    private bool appBarRegistered;
    // Fullscreen app in front (game, video): Windows only tells an app bar (fullscreenFromAppBar), and the dock is not
    // one when it hides itself or does not reserve its strip, so it also looks for itself (fullscreenSeen).
    private bool fullscreenFromAppBar, fullscreenSeen, hiddenForFullscreen;
    private bool FullscreenActive => fullscreenFromAppBar || fullscreenSeen;
    private readonly DispatcherTimer fullscreenTimer;
    private string appBarKey = "";
    private Native.RECT lastSetRc;
    /// <summary>Monitor pinned to this window ("all screens" mode); null = whichever the configuration specifies.</summary>
    private readonly string monitorDevice;
    private readonly DispatcherTimer repositionTimer;
    private uint shellHookMsg;
    private int frames;
    private bool shotTaken, closingQuietly;

    public DockWindow(DockManager manager, string screenshotPath, string monitorDevice, MonitorDockConfig dock = null)
    {
        Manager = manager;
        Dock = dock;
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
        fullscreenTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        fullscreenTimer.Tick += (s, e) => CheckFullscreen();

        SourceInitialized += OnSourceInitialized;
        Loaded += (s, e) =>
        {
            View.Kick();
            if (screenshotPath == null) fullscreenTimer.Start();
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
        // Every timer stops and the handle is forgotten: a reposition pending on a closed dock (when another dock
        // releases its strip, Windows tells every dock) would register a strip again for a window that is gone.
        Closed += (s, e) => { fullscreenTimer.Stop(); repositionTimer.Stop(); RemoveAppBar(); hwnd = IntPtr.Zero; };
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
            // HSHELL_WINDOWACTIVATED / HSHELL_RUDEAPPACTIVATED: another window came to the front; look again at once.
            if ((wParam.ToInt32() & 0x7FFF) == 4 && screenshotPath == null)
                Dispatcher.BeginInvoke(new Action(CheckFullscreen), DispatcherPriority.Background);
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
                    // Fullscreen app (video, game): the dock steps out of the way.
                    fullscreenFromAppBar = lParam != IntPtr.Zero;
                    ApplyFullscreen();
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
            Monitors.Forget();
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

    /// <summary>Whether the window in front covers this dock's monitor in fullscreen.</summary>
    private void CheckFullscreen()
    {
        bool fs = false;
        try
        {
            IntPtr fg = Native.GetForegroundWindow();
            if (fg != IntPtr.Zero && fg != hwnd && Native.IsWindowVisible(fg) && !Native.IsIconic(fg))
            {
                Native.GetWindowThreadProcessId(fg, out uint pid);
                string cls = Native.ClassOf(fg);
                // The desktop and the taskbar also cover the screen, and they are not an app in fullscreen.
                bool shell = cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
                var mon = Monitors.Pick(monitorDevice ?? Config.Monitor);
                if (pid != (uint)Environment.ProcessId && !shell && mon != null &&
                    Native.MonitorFromWindow(fg, 2 /* MONITOR_DEFAULTTONEAREST */) == mon.Handle &&
                    Native.GetWindowRect(fg, out var wr))
                    fs = Native.LooksFullscreen(wr, Native.GetWindowLongPtr(fg, Native.GWL_STYLE).ToInt64(), mon.Bounds);
            }
        }
        catch { fs = false; }
        if (fs == fullscreenSeen) return;
        fullscreenSeen = fs;
        ApplyFullscreen();
    }

    /// <summary>
    /// With a fullscreen app in front the dock hides completely, so that reaching the edge with the pointer (games
    /// that scroll the view at the edges) does not bring it up over the app; it comes back when the app leaves.
    /// </summary>
    private void ApplyFullscreen()
    {
        bool fs = FullscreenActive;
        if (fs == hiddenForFullscreen || hwnd == IntPtr.Zero) return;
        hiddenForFullscreen = fs;
        if (fs)
        {
            Native.SetWindowPos(hwnd, Native.HWND_NOTOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            Visibility = Visibility.Hidden;
        }
        else
        {
            Visibility = Visibility.Visible;
            ApplyPosition();
        }
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
        Native.SetWindowPos(hwnd, FullscreenActive ? Native.HWND_NOTOPMOST : Native.HWND_TOPMOST, x, y, w, h, Native.SWP_NOACTIVATE);
        View.InvalidateVisual();
        View.Kick();
    }

    // ---------------- Launching and ending apps ----------------
    /// <summary>Click on an icon: brings its window to the front (or minimizes it), or launches the app with a bounce.</summary>
    public void ActivateItem(DockItem it, bool newInstance)
    {
        if (it.IsStack) { ShowStack(it); return; }
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

    private void Launch(DockItem it, bool asAdmin, DockItem bounce = null)
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
            Process.Start(psi)?.Dispose();   // the app runs by itself: its handle is not kept
            View.StartBounce(bounce ?? it);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // The user canceled the administrator permission prompt.
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, tr("No se pudo abrir «", "Could not open \"", "開けませんでした: \"") + it.Name + tr("».", "\".", "\"") + "\n\n" + ex.Message, "Muelle",
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
            return WindowTracker.AnyProcessFor(key, wins ?? new List<IntPtr>());
        }
        catch { return false; }
    }

    // ---------------- Editing the list ----------------
    public void AddPaths(IEnumerable<string> paths, int index) => Manager.AddPaths(Items, paths, index);

    public void RemoveItem(DockItem it)
    {
        if (it.Transient) return; // an unpinned running app goes away by itself when it closes
        Items.Remove(it);
        Manager.SaveItems(Items);
        Manager.KickAll();
    }

    public void MoveItem(DockItem it, int index)
    {
        // Dragging an unpinned running app into the pinned ones pins it (as in macOS).
        if (it.Transient && index <= DockManager.PinnedCountOf(Items)) it.Transient = false;
        Items.Remove(it);
        Items.Insert(Math.Clamp(index, 0, Items.Count), it);
        Manager.SaveItems(Items);
        Manager.KickAll();
    }

    /// <summary>The file dialog that adds apps to a list (this dock's by default).</summary>
    public void AddWithDialog(int index, List<DockItem> target = null)
    {
        var dlg = Ui.AppsDialog(tr("Agregar al dock", "Add to the dock", "ドックに追加"));
        Native.SetForegroundWindow(hwnd);
        if (dlg.ShowDialog(this) == true) Manager.AddPaths(target ?? Items, dlg.FileNames, index);
    }

    // ---------------- Stacks ----------------
    /// <summary>Unfolds a stack next to its icon: its shortcuts, and at the end the way to edit it.</summary>
    public void ShowStack(DockItem stack)
    {
        var menu = BuildStackMenu(stack);
        // It opens away from the screen edge, beside the icon.
        menu.Placement = Config.Edge switch
        {
            DockEdge.Left => PlacementMode.Right, DockEdge.Right => PlacementMode.Left,
            DockEdge.Top => PlacementMode.Bottom, _ => PlacementMode.Top,
        };
        var r = View.IconLocalRect(stack);
        if (r.HasValue) menu.PlacementRectangle = r.Value;
        OpenMenu(menu);
    }

    /// <summary>
    /// Opens a menu of this dock. While it is open the dock stays shown and magnified (<see cref="MenuOpen"/>); the
    /// dock window is brought to the front first, or the menu would not close when the user clicks elsewhere.
    /// </summary>
    private void OpenMenu(ContextMenu menu)
    {
        menu.Opened += (s, e) => { MenuOpen = true; View.Kick(); };
        menu.Closed += (s, e) => { MenuOpen = false; View.Kick(); };
        Native.SetForegroundWindow(hwnd);
        menu.PlacementTarget = View;
        menu.IsOpen = true;
    }

    /// <summary>The menu a stack unfolds: each shortcut with its icon (a click opens it), then "Edit stack…".</summary>
    public ContextMenu BuildStackMenu(DockItem stack)
    {
        var menu = new ContextMenu { Placement = PlacementMode.MousePoint };
        if (stack.Children.Count == 0)
            menu.Items.Add(new MenuItem { Header = tr("La pila está vacía", "The stack is empty", "スタックは空です"), IsEnabled = false });
        foreach (var child in stack.Children)
        {
            var c = child;
            // The menu style has no icon column: the icon goes in the header, beside the name.
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(-20, 3, 0, 3) };
            var image = new Image { Source = c.Icon, Width = 28, Height = 28, Margin = new Thickness(0, 0, 10, 0) };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            header.Children.Add(image);
            header.Children.Add(new TextBlock { Text = c.Name, VerticalAlignment = VerticalAlignment.Center });
            var mi = new MenuItem { Header = header };
            mi.Click += (s, e) => ActivateChild(c, stack);
            menu.Items.Add(mi);
        }
        menu.Items.Add(new Separator());
        menu.Items.Add(Enabled(Cmd(tr("Editar pila…", "Edit stack…", "スタックを編集…"), () => EditStack(stack)), !Config.LockItems));
        return menu;
    }

    /// <summary>A shortcut of a stack: shows its window if it is open, otherwise opens it; the stack icon bounces.</summary>
    private void ActivateChild(DockItem child, DockItem stack)
    {
        if (child.ExePath != null)
        {
            var map = WindowTracker.Snapshot();
            if (map.TryGetValue(WindowTracker.Normalize(child.ExePath), out var wins) && wins.Count > 0)
            {
                if (Native.IsIconic(wins[0])) Native.ShowWindow(wins[0], Native.SW_RESTORE);
                Native.SetForegroundWindow(wins[0]);
                return;
            }
        }
        Launch(child, false, stack);
    }

    /// <summary>Opens the stack editor for a stack of this dock, or for a new one (null) at a position.</summary>
    public void EditStack(DockItem stack, int index = -1) => StackWindow.Edit(Manager, Items, stack, index < 0 ? DockManager.PinnedCountOf(Items) : index);

    // ---------------- Right-click menu ----------------
    /// <summary>Short menu: actions on the icon, adding apps, and access to the settings window.</summary>
    public void ShowMenu(DockItem item, int insertAt) => OpenMenu(BuildMenu(item, insertAt));

    /// <summary>
    /// The right-click menu. On an icon: its own actions first, then the dock (lock, settings, quit), and "End task"
    /// last and apart when the app is running. On an empty part of the dock (item null): adding apps and stacks,
    /// then the same dock lines. Everything else (style, language, appearance, updates, about) is in Settings.
    /// </summary>
    public ContextMenu BuildMenu(DockItem item, int insertAt)
    {
        var menu = new ContextMenu { Placement = PlacementMode.MousePoint };
        bool locked = Config.LockItems;

        if (item != null)
        {
            menu.Items.Add(new MenuItem { Header = item.Name, IsEnabled = false, FontWeight = FontWeights.SemiBold });
            menu.Items.Add(new Separator());
        }

        if (item != null && item.IsStack)
        {
            menu.Items.Add(Cmd(tr("Abrir la pila", "Open the stack", "スタックを開く"), () => ShowStack(item)));
            menu.Items.Add(Enabled(Cmd(tr("Editar pila…", "Edit stack…", "スタックを編集…"), () => EditStack(item)), !locked));
            menu.Items.Add(Enabled(Cmd(tr("Liberar del dock", "Unpin from dock", "ドックから外す"), () => RemoveItem(item)), !locked));
            menu.Items.Add(new Separator());
        }
        else if (item != null)
        {
            menu.Items.Add(Cmd(item.Running ? tr("Abrir otra ventana", "Open another window", "新しいウィンドウを開く") : tr("Abrir", "Open", "開く"), () => Launch(item, false)));
            if (item.Transient)
                menu.Items.Add(Enabled(Cmd(tr("Fijar al dock", "Pin to dock", "ドックに固定"), () => Manager.Pin(item)), !locked));
            else
            {
                menu.Items.Add(Enabled(Cmd(tr("Renombrar…", "Rename…", "名前の変更…"), () => RenameItem(item)), !locked));
                menu.Items.Add(Enabled(Cmd(tr("Liberar del dock", "Unpin from dock", "ドックから外す"), () => RemoveItem(item)), !locked));
            }

            // Less frequent actions, one level down.
            var more = new MenuItem { Header = tr("Más", "More", "その他") };
            more.Items.Add(Cmd(tr("Ejecutar como administrador", "Run as administrator", "管理者として実行"), () => Launch(item, true)));
            more.Items.Add(Cmd(tr("Abrir ubicación del archivo", "Open file location", "ファイルの場所を開く"), () => OpenLocation(item)));
            // A pinned icon can go into one of the stacks of this dock.
            var stacks = Items.Where(i => i.IsStack).ToList();
            if (!item.Transient && stacks.Count > 0 && !locked)
            {
                var into = new MenuItem { Header = tr("Mover a la pila", "Move to stack", "スタックへ移動") };
                foreach (var st in stacks) { var target = st; into.Items.Add(Cmd(target.Name, () => Manager.MoveIntoStack(Items, item, target))); }
                more.Items.Add(into);
            }
            menu.Items.Add(more);
            menu.Items.Add(new Separator());
        }
        else
        {
            menu.Items.Add(Enabled(Cmd(tr("Agregar aplicación…", "Add application…", "アプリケーションを追加…"), () => AddWithDialog(insertAt)), !locked));
            menu.Items.Add(Enabled(Cmd(tr("Nueva pila…", "New stack…", "新しいスタック…"), () => EditStack(null, insertAt)), !locked));
            menu.Items.Add(new Separator());
        }

        var lockItem = Cmd(tr("Bloquear dock", "Lock dock", "ドックをロック"), () => Manager.SetLocked(!Config.LockItems));
        lockItem.IsCheckable = true;
        lockItem.IsChecked = locked;
        menu.Items.Add(lockItem);
        menu.Items.Add(Cmd(tr("Configuración…", "Settings…", "設定…"), () => Manager.OpenSettings()));
        menu.Items.Add(Cmd(tr("Salir de Muelle", "Quit Muelle", "Muelle を終了"), Manager.Exit));

        // End task: only while the app is running, last and apart so it is not clicked by mistake.
        if (item != null && !item.IsStack && item.Running && item.ExePath != null && HasKillableProcess(item))
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(Cmd(tr("Finalizar tarea", "End task", "タスクの終了"), () => EndTask(item)));
        }
        return menu;
    }

    /// <summary>Enables a line of the menu; a line the lock disables says why in its tooltip.</summary>
    private static MenuItem Enabled(MenuItem mi, bool enabled)
    {
        mi.IsEnabled = enabled;
        if (!enabled)
        {
            mi.ToolTip = Ui.LockedHint;
            ToolTipService.SetShowOnDisabled(mi, true);
        }
        return mi;
    }

    /// <summary>Asks for a new name for a pinned icon; an empty name goes back to the automatic one.</summary>
    private void RenameItem(DockItem item)
    {
        string name = RenameWindow.Ask(this, item.Name);
        if (name == null) return;
        // Accepting the name as it was changes nothing: an automatic name (a default app's) stays automatic, so it
        // still follows the interface language.
        if (name.Trim() == item.Name) return;
        item.Cfg.Name = name.Trim();
        Manager.SaveItems(Items);
        Manager.KickAll();
    }

    private static MenuItem Cmd(string header, Action action)
    {
        var mi = new MenuItem { Header = header };
        mi.Click += (s, e) => action();
        return mi;
    }

    private static void OpenLocation(DockItem it)
    {
        string p = it.Cfg.Path;
        if (!it.IsStack && (File.Exists(p) || Directory.Exists(p))) Shell.ShowInExplorer(p);
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
