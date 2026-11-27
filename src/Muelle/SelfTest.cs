//
//  SelfTest.cs
//  Muelle: a dock for Windows.
//
//  Copyright (C) 2026 Gabriel Castiglia. Licensed under the GNU GPL, version 2 only.
//  Date: November 20, 2026
//  SPDX-License-Identifier: GPL-2.0-only
//
//  This program is free software: you can redistribute it and/or modify it under the terms of
//  the GNU General Public License, version 2, as published by the Free Software Foundation.
//  It is distributed WITHOUT ANY WARRANTY. See the LICENSE file for details.
//

using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Muelle;

/// <summary>
/// "Muelle.exe --self-test &lt;log&gt;": checks the configuration, the stacks, the docks per screen and the pure
/// functions of the updates, and writes one line per check to the log. It works on a throwaway configuration in a
/// new temporary folder, deleted at the end; the user's own (%APPDATA%\Muelle) is never read or written, and it
/// checks that too. No window is shown, nothing is activated, and the running dock is not touched (no single-instance
/// lock is taken). The exit code is the number of failures. With "--online" it also asks GitHub for the latest
/// release and downloads its setup into the throwaway folder to check it against its SHA256SUMS.txt (never run).
/// </summary>
internal static class SelfTest
{
    private static readonly StringBuilder log = new();
    private static int failures, passes;

    public static int Run(string logPath)
    {
        string realFile = DockConfig.FilePath;   // before the override: only to prove it is left alone
        string realBefore = Fingerprint(realFile);
        string sandbox = Path.Combine(Path.GetTempPath(), "MuelleSelfTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);
        DockConfig.FolderOverride = sandbox;
        Line($"Muelle {Updates.Current} self-test, {DateTime.Now:s}");
        try
        {
            Check("the configuration is the throwaway one", DockConfig.FilePath.StartsWith(sandbox, StringComparison.OrdinalIgnoreCase));
            // The windows and the theme need an application object; it is never run, so nothing appears.
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            Section("Configuration", Configuration);
            Section("Screens", Screens);
            Section("Docks per screen", PerMonitor);
            Section("Stacks", Stacks);
            Section("Lock and accessibility", LockAndAccessibility);
            Section("Icons that follow the app", Icons);
            Section("Fullscreen apps", Fullscreen);
            Section("Updates", UpdateFunctions);
            if (Environment.GetCommandLineArgs().Contains("--online")) Section("Updates, online", Online);
            GC.KeepAlive(app);
        }
        catch (Exception ex) { Fail("unexpected error", ex.ToString()); }
        finally
        {
            DockConfig.FolderOverride = null;
            try { Directory.Delete(sandbox, true); } catch (Exception ex) { Line("Note: could not delete the throwaway folder: " + ex.Message); }
            Check("the throwaway configuration was deleted", !Directory.Exists(sandbox));
            Check("the user's configuration was not touched", Fingerprint(realFile) == realBefore);
            Line($"Result: {passes} passed, {failures} failed.");
            try { Directory.CreateDirectory(Path.GetDirectoryName(logPath)!); File.WriteAllText(logPath, log.ToString()); } catch { }
        }
        return failures;
    }

    // ---------------- Configuration ----------------
    private static void Configuration()
    {
        var first = DockConfig.Load(out bool firstRun);
        Check("first start: no file yet", firstRun && !File.Exists(DockConfig.FilePath));
        Check("first start: default apps", first.Items.Count > 0 && first.Items.All(i => !i.IsStack));
        Check("first start: one screen for every dock", !first.PerMonitor && first.MonitorDocks.Count == 0);

        first.IconSize = 52;
        first.Edge = DockEdge.Bottom;
        first.PerMonitor = true;
        first.Items.Add(new DockItemConfig { Name = "Tools", Icon = @"C:\Example\tools.ico", Stack = new() { Item("notepad.exe"), Item("calc.exe") } });
        first.MonitorDocks.Add(new MonitorDockConfig { Id = "id-A", Device = @"\\.\DISPLAY1", Width = 1920, Height = 1080, Items = new() { Item("mspaint.exe") } });
        first.Save();
        Check("saved to the throwaway folder", File.Exists(DockConfig.FilePath));
        var again = DockConfig.Load(out firstRun);
        Check("second start: the file is read", !firstRun);
        Check("round trip: icon size and edge", again.IconSize == 52 && again.Edge == DockEdge.Bottom);
        var stack = again.Items.LastOrDefault();
        Check("round trip: the stack, its name, icon and shortcuts in order",
              stack != null && stack.IsStack && stack.Name == "Tools" && stack.Icon == @"C:\Example\tools.ico"
              && stack.Stack.Count == 2 && stack.Stack[0].Path.EndsWith("notepad.exe") && stack.Stack[1].Path.EndsWith("calc.exe"));
        Check("round trip: each screen with its own icons", again.PerMonitor && again.MonitorDocks.Count == 1
              && again.MonitorDocks[0].Id == "id-A" && again.MonitorDocks[0].Items.Count == 1);
        Check("an ordinary icon is not written as a stack", !File.ReadAllText(DockConfig.FilePath).Contains("\"Stack\": null"));

        // Damaged entries are dropped; a stack keeps only real shortcuts and always has a name.
        File.WriteAllText(DockConfig.FilePath, """
            { "IconSize": 500, "Items": [
                { "Name": "No path", "Path": "" },
                { "Name": "", "Stack": [ { "Name": "x", "Path": "" }, { "Name": "inner", "Stack": [] }, { "Name": "ok", "Path": "C:\\Windows\\notepad.exe" } ] }
              ],
              "MonitorDocks": [ null, { "Id": null, "Items": null } ] }
            """);
        var bad = DockConfig.Load(out _);
        Check("sanitize: icon size kept within its limits", bad.IconSize == DockConfig.MaxIconSize);
        Check("sanitize: an icon without a path is dropped", bad.Items.Count == 1);
        Check("sanitize: a stack keeps only real shortcuts, and gets a name",
              bad.Items[0].IsStack && bad.Items[0].Stack.Count == 1 && bad.Items[0].Stack[0].Name == "ok" && !string.IsNullOrWhiteSpace(bad.Items[0].Name));
        Check("sanitize: empty screen entries are dropped or repaired",
              bad.MonitorDocks.Count == 1 && bad.MonitorDocks[0].Id == "" && bad.MonitorDocks[0].Items != null);

        var copy = again.Items.Last().Clone();
        copy.Stack.Clear();
        Check("a copy of a stack shares nothing with it", again.Items.Last().Stack.Count == 2);

        // The user's settings survive a new version: a value this one cannot read drops that setting alone.
        File.WriteAllText(DockConfig.FilePath, """
            { "AutoHide": true, "IconSize": 44, "Edge": "Diagonal", "FutureSetting": { "a": 1 },
              "Items": [ { "Name": "Notes", "Path": "C:\\Windows\\notepad.exe", "FutureItemField": 7 } ] }
            """);
        var newer = DockConfig.Load(out firstRun);
        Check("a value it cannot read drops that setting only, not all of them",
              !firstRun && newer.AutoHide && newer.IconSize == 44 && newer.Edge == DockEdge.Left && newer.Items.Count == 1);
        newer.Save();
        string written = File.ReadAllText(DockConfig.FilePath);
        Check("settings of a newer version are written back as they were",
              written.Contains("FutureSetting") && written.Contains("FutureItemField"));
        Check("another version reading the file keeps a copy of it first",
              Directory.GetFiles(DockConfig.Folder, "config.before-*.json").Length == 1);

        // A file that cannot be read at all is kept aside, and the previous one is read: never the defaults.
        newer.IconSize = 60;
        newer.Save();
        File.WriteAllText(DockConfig.FilePath, "{ \"AutoHide\": tr");
        var rescued = DockConfig.Load(out firstRun);
        Check("an unreadable file: the previous settings, not the defaults", !firstRun && rescued.AutoHide && rescued.IconSize == 44);
        Check("an unreadable file is kept aside", Directory.GetFiles(DockConfig.Folder, "config.unreadable-*.json").Length == 1);
        rescued.Save();
        Check("a damaged file never becomes the previous one", DockConfig.Load(out _).IconSize == 44 && File.ReadAllText(DockConfig.PreviousPath).Contains("\"AutoHide\": true"));
        foreach (var f in Directory.GetFiles(DockConfig.Folder, "config*.json")) File.Delete(f);

        // One icon that cannot be read (a field of another type) drops that icon only, never the whole dock; what was
        // dropped is kept, and no save of the session makes the previous file lose it.
        File.WriteAllText(DockConfig.FilePath, """
            { "IconSize": 48, "Items": [
                { "Name": "Notes", "Path": "C:\\Windows\\notepad.exe" },
                { "Name": "Broken", "Path": 123 },
                { "Name": "Box", "Stack": [ { "Name": "Calc", "Path": "C:\\Windows\\System32\\calc.exe" }, { "Name": "Bad", "Path": [] } ] }
              ] }
            """);
        var partial = DockConfig.Load(out firstRun);
        Check("an icon that cannot be read drops that icon only",
              !firstRun && partial.IconSize == 48 && partial.Items.Count == 2 && partial.Items[0].Name == "Notes"
              && partial.Items[1].IsStack && partial.Items[1].Stack.Count == 1 && partial.Items[1].Stack[0].Name == "Calc");
        Check("what was dropped is kept, with a copy of the whole file",
              Directory.GetFiles(DockConfig.Folder, "config.dropped-*.json").Length == 1
              && File.ReadAllText(Directory.GetFiles(DockConfig.Folder, "config.dropped-*.json")[0]).Contains("Broken")
              && Directory.GetFiles(DockConfig.Folder, "config.partial-*.json").Length == 1);
        partial.Save();
        partial.IconSize = 50;
        partial.Save();
        Check("no save of the session makes the previous file lose the icon that could not be read",
              File.ReadAllText(DockConfig.PreviousPath).Contains("Broken"));
        foreach (var f in Directory.GetFiles(DockConfig.Folder, "config*.json")) File.Delete(f);
    }

    // ---------------- Fullscreen apps ----------------
    private static void Fullscreen()
    {
        var mon = new Native.RECT(0, 0, 1920, 1080);
        const long popup = 0x80000000, normal = 0x00CF0000; // WS_POPUP; WS_OVERLAPPEDWINDOW (has WS_CAPTION)
        Check("a borderless window the size of the screen is fullscreen", Native.LooksFullscreen(new(0, 0, 1920, 1080), popup, mon));
        Check("a window larger than the screen is fullscreen", Native.LooksFullscreen(new(-1, -1, 1921, 1081), popup, mon));
        Check("a maximized window with a title bar is not", !Native.LooksFullscreen(new(-8, -8, 1928, 1088), normal, mon));
        Check("a smaller borderless window is not", !Native.LooksFullscreen(new(0, 0, 1920, 1040), popup, mon));
        Check("a window on another screen is not", !Native.LooksFullscreen(new(1920, 0, 3840, 1080), popup, mon));
        Check("without a screen, nothing is fullscreen", !Native.LooksFullscreen(new(0, 0, 1920, 1080), popup, default));
    }

    // ---------------- Screens ----------------
    private static void Screens()
    {
        var list = new List<MonitorDockConfig>
        {
            new() { Id = "id-A", Device = @"\\.\DISPLAY1", Width = 1920, Height = 1080 },
            new() { Id = "", Device = @"\\.\DISPLAY2", Width = 2560, Height = 1440 },
            new() { Id = "id-C", Device = @"\\.\DISPLAY3", Width = 1280, Height = 1024 },
        };
        Check("a screen is found by its id, even under another display name",
              MonitorDockConfig.Find(list, "id-A", @"\\.\DISPLAY9", 800, 600) == list[0]);
        Check("without an id, by display name and resolution",
              MonitorDockConfig.Find(list, "", @"\\.\DISPLAY2", 2560, 1440) == list[1]);
        Check("another resolution is another screen", MonitorDockConfig.Find(list, "", @"\\.\DISPLAY2", 1920, 1080) == null);
        Check("a new id falls back to name and resolution",
              MonitorDockConfig.Find(list, "id-new", @"\\.\DISPLAY3", 1280, 1024) == list[2]);
        Check("but not to the entry of a screen that is connected under its own id",
              MonitorDockConfig.Find(list, "id-new", @"\\.\DISPLAY3", 1280, 1024, new[] { "id-C", "id-new" }) == null);
        var real = Monitors.All();
        Check("the connected screens are listed", real.Count >= 0);
        Line($"  ({real.Count} screen(s) connected, {real.Count(m => m.Id.Length > 0)} with a device path)");
    }

    // ---------------- Docks per screen ----------------
    private static void PerMonitor()
    {
        WriteConfig(c => { c.Items = new() { Item("notepad.exe"), Item("calc.exe") }; c.ShowRunningApps = false; });
        var m = new DockManager(null, null, selfTest: true);
        Check("off: no screen gets its own entry", m.DockFor("id-A", @"\\.\DISPLAY1", 1920, 1080, new[] { "id-A" }) == null);
        Check("off: every dock shows the shared icons", m.ListFor(null) == m.Items && m.Items.Count == 2);

        m.Config.PerMonitor = true;
        var a = m.DockFor("id-A", @"\\.\DISPLAY1", 1920, 1080, new[] { "id-A", "id-B" });
        var b = m.DockFor("id-B", @"\\.\DISPLAY2", 2560, 1440, new[] { "id-A", "id-B" });
        Check("on: each screen gets its own entry", a != null && b != null && a != b && m.Config.MonitorDocks.Count == 2);
        Check("on: a new screen starts with a copy of the shared icons",
              a.Items.Count == 2 && a.Items[0] != m.Config.Items[0] && a.Items[0].Path == m.Config.Items[0].Path);
        var listA = m.ListFor(a);
        Check("on: the same list every time", m.ListFor(a) == listA && listA != m.Items && m.ListFor(b) != listA);

        m.AddPaths(listA, new[] { Sys("mspaint.exe") }, 0);
        m.FlushConfig();
        Check("adding to one screen: only that screen changes",
              a.Items.Count == 3 && b.Items.Count == 2 && m.Config.Items.Count == 2 && a.Items[0].Path.EndsWith("mspaint.exe"));
        m.ListFor(b).RemoveAt(0);
        m.SaveItems(m.ListFor(b));
        m.FlushConfig();
        Check("removing from another screen: only that one changes", b.Items.Count == 1 && a.Items.Count == 3);
        Check("a repeated shortcut is not added twice", AddCount(m, listA, Sys("mspaint.exe")) == 3);

        var same = m.DockFor("id-A", @"\\.\DISPLAY3", 1920, 1080, new[] { "id-A" });
        Check("a screen that changes display name keeps its icons and the new name is noted", same == a && a.Device == @"\\.\DISPLAY3");

        // Screen B is disconnected and Muelle restarts: its icons stay in the file and come back with it.
        var m2 = new DockManager(null, null, selfTest: true);
        Check("a disconnected screen keeps its entry", m2.Config.MonitorDocks.Count == 2);
        var b2 = m2.DockFor("id-B", @"\\.\DISPLAY2", 2560, 1440, new[] { "id-B" });
        Check("its icons come back when it does", b2 != null && b2.Items.Count == 1 && m2.ListFor(b2).Count == 1);
        m2.Config.PerMonitor = false;
        Check("turned off again: the shared icons, the screens' entries kept",
              m2.DockFor("id-B", @"\\.\DISPLAY2", 2560, 1440, new[] { "id-B" }) == null && m2.Config.MonitorDocks.Count == 2);
    }

    private static int AddCount(DockManager m, List<DockItem> list, string path)
    {
        m.AddPaths(list, new[] { path }, 0);
        return list.Count;
    }

    // ---------------- Icons that follow the app ----------------
    /// <summary>
    /// A program whose shortcuts name an icon of its own folder (as Vigía does with the colour chosen for it): the
    /// dock shows that icon, follows it when it changes, and leaves alone an icon chosen by hand from elsewhere.
    /// A copy of Notepad stands in for the program, with shortcuts in a folder of the test's own.
    /// </summary>
    private static void Icons()
    {
        string root = Path.Combine(Path.GetTempPath(), "MuelleIcons-" + Guid.NewGuid().ToString("N"));
        string app = Path.Combine(root, "App"), start = Path.Combine(root, "Start"), lnk = Path.Combine(start, "App.lnk");
        Directory.CreateDirectory(Path.Combine(app, "theme"));
        Directory.CreateDirectory(start);
        try
        {
            string exe = Path.Combine(app, "App.exe");
            File.Copy(Path.Combine(Environment.SystemDirectory, "notepad.exe"), exe);
            string blue = Path.Combine(app, "theme", "app_blue.ico"), red = Path.Combine(app, "theme", "app_red.ico");
            string elsewhere = Path.Combine(root, "chosen.ico");
            WriteIco(blue, 48, Colors.SteelBlue);
            WriteIco(red, 40, Colors.IndianRed);
            WriteIco(elsewhere, 36, Colors.Gray);
            Shell.ShortcutFoldersOverride = new[] { start };
            Shell.WriteLink(lnk, exe, blue);
            Shell.ForgetThemedIcons();
            Check("a program's own shortcut names its icon", string.Equals(Shell.ThemedIconPath(exe), blue, StringComparison.OrdinalIgnoreCase));

            WriteConfig(c => { c.Items = new() { new DockItemConfig { Name = "", Path = exe } }; c.ShowRunningApps = false; });
            var m = new DockManager(null, null, selfTest: true);
            var it = m.Items.First();
            Check("the dock shows the icon of the program's shortcut, read from its file", (it.Icon as BitmapSource)?.PixelWidth == 48);
            Check("with nothing changed, no icon is read again", m.RefreshIcons() == 0);

            Shell.WriteLink(lnk, exe, red);
            File.Delete(blue);
            Shell.ForgetThemedIcons();
            int read = m.RefreshIcons();
            Check("the program changed the colour of its icon: the dock follows without restarting",
                  read == 1 && (it.Icon as BitmapSource)?.PixelWidth == 40);

            Shell.WriteLink(lnk, exe, elsewhere);
            Shell.ForgetThemedIcons();
            Check("an icon chosen by hand from outside the program's folder is not taken for the program's", Shell.ThemedIconPath(exe) == null);

            // A shortcut of its own with an icon chosen by hand keeps it, though the program's shortcuts name another.
            Shell.WriteLink(lnk, exe, red);
            string mine = Path.Combine(root, "Mine.lnk");
            Shell.WriteLink(mine, exe, elsewhere);
            Shell.ForgetThemedIcons();
            Check("an icon chosen for a shortcut is found", string.Equals(Shell.ChosenIconPath(mine), elsewhere, StringComparison.OrdinalIgnoreCase)
                  && Shell.ChosenIconPath(lnk) == null);
            Check("the dock shows the icon chosen for its shortcut, not the program's", (DockItem.From(new DockItemConfig { Path = mine }).Icon as BitmapSource)?.PixelWidth == 36);
            Check("an icon chosen in the settings wins over every other",
                  (DockItem.From(new DockItemConfig { Path = exe, Icon = elsewhere }).Icon as BitmapSource)?.PixelWidth == 36);
            Check("the .ico chosen for a stack is read from its file too, not from the shell's icon cache",
                  (DockItem.From(new DockItemConfig { Name = "S", Stack = new() { Item("notepad.exe") }, Icon = elsewhere }).Icon as BitmapSource)?.PixelWidth == 36);
        }
        finally
        {
            Shell.ShortcutFoldersOverride = null;
            Shell.ForgetThemedIcons();
            try { Directory.Delete(root, true); } catch { }
        }
    }

    /// <summary>An .ico of one square of colour, <paramref name="px"/> wide (a PNG inside, as Windows allows).</summary>
    private static void WriteIco(string path, int px, Color c)
    {
        var pixels = new byte[px * px * 4];
        for (int i = 0; i < pixels.Length; i += 4) { pixels[i] = c.B; pixels[i + 1] = c.G; pixels[i + 2] = c.R; pixels[i + 3] = 255; }
        var src = BitmapSource.Create(px, px, 96, 96, PixelFormats.Bgra32, null, pixels, px * 4);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(src));
        using var ms = new MemoryStream();
        enc.Save(ms);
        byte[] png = ms.ToArray();
        using var w = new BinaryWriter(File.Create(path));
        w.Write((short)0); w.Write((short)1); w.Write((short)1);                     // ICONDIR: an icon, one image
        w.Write((byte)px); w.Write((byte)px); w.Write((byte)0); w.Write((byte)0);   // ICONDIRENTRY
        w.Write((short)1); w.Write((short)32); w.Write(png.Length); w.Write(22);
        w.Write(png);
    }

    // ---------------- Stacks ----------------
    private static void Stacks()
    {
        WriteConfig(c => { c.Items = new() { Item("notepad.exe"), Item("calc.exe"), Item("mspaint.exe") }; c.ShowRunningApps = false; });
        var m = new DockManager(null, null, selfTest: true);
        var stack = m.PutStack(m.Items, new DockItemConfig { Name = "Tools", Stack = new() { Item("notepad.exe") } }, null, 1);
        Check("a new stack goes where it was asked", m.Items.IndexOf(stack) == 1 && m.Items.Count == 4);
        Check("a stack has its shortcuts ready and an icon", stack.IsStack && stack.Children.Count == 1 && stack.Icon != null && stack.ExePath == null);
        Check("a stack's name is its own", stack.Name == "Tools");

        var calc = m.Items.First(i => !i.IsStack && i.Cfg.Path.EndsWith("calc.exe"));
        m.MoveIntoStack(m.Items, calc, stack);
        stack = m.Items.First(i => i.IsStack);
        Check("an icon moved into a stack leaves the dock and goes to the end of the stack",
              !m.Items.Contains(calc) && stack.Children.Count == 2 && stack.Cfg.Stack[1].Path.EndsWith("calc.exe"));

        var editor = new StackWindow(m, m.Items, stack, 0);
        editor.AddPaths(new[] { Sys("mspaint.exe"), Sys("mspaint.exe") });
        Check("the editor adds a shortcut once", editor.Stack.Stack.Count == 3);
        editor.Move(2, 0);
        Check("the editor reorders", editor.Stack.Stack[0].Path.EndsWith("mspaint.exe"));
        editor.RemoveAt(1);
        Check("the editor removes", editor.Stack.Stack.Count == 2 && editor.Stack.Stack[1].Path.EndsWith("calc.exe"));
        Check("the dock does not change before Save", m.Items.First(i => i.IsStack).Children.Count == 2
              && m.Items.First(i => i.IsStack).Cfg.Stack[0].Path.EndsWith("notepad.exe"));
        editor.Stack.Name = "  Graphics  ";
        editor.Stack.Icon = Sys("shell32.dll");
        editor.Save();
        var saved = m.Items.First(i => i.IsStack);
        Check("Save replaces the stack in place", m.Items.Count(i => i.IsStack) == 1 && saved.Name == "Graphics"
              && saved.Cfg.Stack.Select(c => Path.GetFileName(c.Path)).SequenceEqual(new[] { "mspaint.exe", "calc.exe" }));
        m.FlushConfig();
        var reread = DockConfig.Load(out _).Items.FirstOrDefault(i => i.IsStack);
        Check("the stack is saved in the configuration", reread != null && reread.Name == "Graphics" && reread.Stack.Count == 2
              && reread.Icon == Sys("shell32.dll"));

        var dock = new DockWindow(m, null, null);
        var menu = dock.BuildStackMenu(saved);
        Check("the stack unfolds its shortcuts and \"Edit stack…\"", menu.Items.Count == saved.Children.Count + 2);
        var empty = m.PutStack(m.Items, new DockItemConfig { Name = "Empty", Stack = new() }, null, 0);
        Check("an empty stack says so", dock.BuildStackMenu(empty).Items.Count == 3);
        var right = dock.BuildMenu(saved, 0);
        Check("the menu of a stack offers to edit it", right.Items.OfType<System.Windows.Controls.MenuItem>()
              .Any(i => (i.Header as string) == Loc.tr("Editar pila…", "Edit stack…", "スタックを編集…")));
        var plain = dock.BuildMenu(m.Items.First(i => !i.IsStack && !i.Transient), 0);
        static IEnumerable<System.Windows.Controls.MenuItem> All(System.Windows.Controls.ItemsControl c) =>
            c.Items.OfType<System.Windows.Controls.MenuItem>().SelectMany(i => All(i).Prepend(i));
        bool Has(System.Windows.Controls.ItemsControl c, string es, string en, string ja) => All(c).Any(i => (i.Header as string) == Loc.tr(es, en, ja));
        Check("the menu of an icon offers to move it to a stack, under \"More\"", Has(plain, "Mover a la pila", "Move to stack", "スタックへ移動"));
        Check("the menu of an icon offers to rename and unpin it", Has(plain, "Renombrar…", "Rename…", "名前の変更…")
              && Has(plain, "Liberar del dock", "Unpin from dock", "ドックから外す"));
        Check("the menu of an icon is short (dock options live in Settings)", plain.Items.Count <= 12
              && !Has(plain, "Apariencia", "Appearance", "外観"));
        var bare = dock.BuildMenu(null, 0);
        Check("the menu of the empty dock adds apps and opens Settings", Has(bare, "Agregar aplicación…", "Add application…", "アプリケーションを追加…")
              && Has(bare, "Configuración…", "Settings…", "設定…"));
        var before = Loc.Current;
        foreach (var (lang, _) in Loc.All) { Loc.Current = lang; Check("menu in " + Loc.Code(lang), dock.BuildMenu(null, 0).Items.Count > 0); }
        Loc.Current = before;
        Check("the three languages round-trip through their codes", Loc.All.All(l => Loc.FromCode(Loc.Code(l.Lang)) == l.Lang)
              && Loc.FromCode("xx") == UiLanguage.En);
        Check("a stack never counts as running", !saved.Running);
        var add = Ui.AppsDialog("x");
        Check("the add dialog takes several files and keeps a shortcut as itself", add.Multiselect && !add.DereferenceLinks
              && add.Filter.Contains("*.lnk") && add.Filter.Contains("*.exe"));
    }

    // ---------------- Lock and accessibility ----------------
    private static void LockAndAccessibility()
    {
        WriteConfig(c => { c.Items = new() { Item("notepad.exe"), Item("calc.exe") }; c.ShowRunningApps = false; c.LockItems = true; });
        var m = new DockManager(null, null, selfTest: true);
        var settings = new SettingsWindow(m);
        Check("locked: the settings disable the buttons that change the icons, and say why",
              settings.EditButtons.Count == 6 && settings.EditButtons.All(b => !b.IsEnabled && b.ToolTip is string t && t.Length > 0));
        m.SetLocked(false);
        settings.LockChanged();
        Check("unlocked: the same buttons are enabled again at once", settings.EditButtons.All(b => b.IsEnabled && b.ToolTip == null));
        m.SetLocked(true);
        settings.LockChanged();
        Check("locked again: they follow", settings.EditButtons.All(b => !b.IsEnabled));
        var dock = new DockWindow(m, null, null);
        var menu = dock.BuildMenu(null, 0);
        Check("locked: the dock menu disables adding, with the same explanation", menu.Items.OfType<System.Windows.Controls.MenuItem>()
              .Where(i => !i.IsEnabled && i.Header is string).All(i => (i.ToolTip as string) == Ui.LockedHint)
              && menu.Items.OfType<System.Windows.Controls.MenuItem>().Count(i => !i.IsEnabled) >= 2);
        var lockedStack = m.PutStack(m.Items, new DockItemConfig { Name = "Locked", Stack = new() { Item("notepad.exe") } }, null, 0);
        bool EditDisabled(System.Windows.Controls.ItemsControl c) => c.Items.OfType<System.Windows.Controls.MenuItem>()
            .Any(i => (i.Header as string) == Loc.tr("Editar pila…", "Edit stack…", "スタックを編集…") && !i.IsEnabled && (i.ToolTip as string) == Ui.LockedHint);
        Check("locked: \"Edit stack…\" is disabled in the stack and in its right-click menu", EditDisabled(dock.BuildStackMenu(lockedStack)) && EditDisabled(dock.BuildMenu(lockedStack, 0)));
        m.SetLocked(false);

        // The colours are radio buttons of one group, each with its name, exactly one chosen.
        static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
        {
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            {
                if (child is T t) yield return t;
                foreach (var d in Find<T>(child)) yield return d;
            }
        }
        var tiles = Find<System.Windows.Controls.RadioButton>(settings).Where(r => r.GroupName == "accent").ToList();
        Check("the colours can be chosen with the keyboard and are named for screen readers",
              tiles.Count == Theme.Accents.Length && tiles.All(t => t.Focusable && System.Windows.Automation.AutomationProperties.GetName(t).Length > 0)
              && tiles.Count(t => t.IsChecked == true) == 1);
        settings.Close();

        // The dock for screen readers: one button per icon, with its name, that can be invoked.
        var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(dock.View);
        var children = peer?.GetChildren() ?? new();
        Check("the dock is a tool bar for UI Automation", peer?.GetAutomationControlType() == System.Windows.Automation.Peers.AutomationControlType.ToolBar);
        Check("one element per icon, named after it, that can be invoked", children.Count == m.Items.Count
              && children.Select(c => c.GetName()).SequenceEqual(m.Items.Select(i => i.Name))
              && children.All(c => c.GetAutomationControlType() == System.Windows.Automation.Peers.AutomationControlType.Button
                                   && c.GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke) != null));

        // A stack editor knows whether it holds changes, so opening another one never drops them in silence.
        var stack = m.PutStack(m.Items, new DockItemConfig { Name = "Tools", Stack = new() { Item("notepad.exe") } }, null, 0);
        var editor = new StackWindow(m, m.Items, stack, 0);
        Check("a stack editor just opened has no changes", !editor.Dirty);
        editor.AddPaths(new[] { Sys("calc.exe") });
        Check("after a change it has", editor.Dirty);
        editor.Close();
    }

    // ---------------- Updates ----------------
    private static void UpdateFunctions()
    {
        Check("IsNewer: 2.2.0 > 2.1.0", Updates.IsNewer("v2.2.0", "2.1.0"));
        Check("IsNewer: 2.10.0 > 2.9.9", Updates.IsNewer("2.10.0", "2.9.9"));
        Check("IsNewer: same version is not newer", !Updates.IsNewer("v2.1.0", "2.1.0"));
        Check("IsNewer: older is not newer", !Updates.IsNewer("V1.9", "2.1.0"));
        Check("IsNewer: garbage is not newer", !Updates.IsNewer("latest", "2.1.0") && !Updates.IsNewer("", "2.1.0"));

        string h1 = new string('a', 64), h2 = "EB80163DED5988FA987B7E38165C5E918C73FD2DA482C1C8BA19A10416503142";
        string sums = $"{h1}  Muelle.exe\n{h2} *muelle-setup.exe\r\n{new string('b', 63)}  muelle.msi\nnot a line\n";
        Check("HashFor: \"hash  name\"", Updates.HashFor(sums, "Muelle.exe") == h1);
        Check("HashFor: \"hash *name\", lower case, any case of the name", Updates.HashFor(sums, "MUELLE-SETUP.EXE") == h2.ToLowerInvariant());
        Check("HashFor: a hash of the wrong length is ignored", Updates.HashFor(sums, "muelle.msi") == null);
        Check("HashFor: a missing file", Updates.HashFor(sums, "other.exe") == null && Updates.HashFor("", "Muelle.exe") == null);
        Check("HashFor: a name is not matched by its start", Updates.HashFor($"{h1}  muelle-setup.exe.old\n", "muelle-setup.exe") == null);

        var release = Updates.Parse("""
            { "tag_name": "v9.8.7", "html_url": "https://example.com/r",
              "assets": [ { "name": "muelle.msi", "browser_download_url": "https://example.com/msi" },
                          { "name": "muelle-setup.exe", "browser_download_url": "https://example.com/setup" },
                          { "name": "SHA256SUMS.txt", "browser_download_url": "https://example.com/sums" },
                          { "name": "release.sig", "browser_download_url": "https://example.com/sig" } ] }
            """);
        Check("a release: its version, page, setup, hashes and signature", release.Tag == "9.8.7" && release.Page == "https://example.com/r"
              && release.SetupUrl == "https://example.com/setup" && release.SumsUrl == "https://example.com/sums"
              && release.SigUrl == "https://example.com/sig" && release.CanInstall);
        Check("a release without the author's signature cannot install itself", !Updates.Parse("""
            { "tag_name": "v9", "assets": [ { "name": "muelle-setup.exe", "browser_download_url": "https://example.com/setup" },
                                            { "name": "SHA256SUMS.txt", "browser_download_url": "https://example.com/sums" } ] }
            """).CanInstall);
        // release.sig: real signatures by the author's two keys (the PC's, the Mac's) of the same bytes; any change is refused
        var signed = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef0123456789abcdef0123456789ABCDEF  Vigia-Setup-9.9.9.exe\n"
                                            + "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff *Vigia_Manual_EN.pdf\n");
        const string pcSig = "rEEKYRSXLO7VKbID/El7ZkFBh1uKIPNS38KopNSZ6DEm7knfIUjxch9kmv8xfREc6bLKka+bePFZ1BKzO+15ww==";
        const string macSig = "K8+3h6FgPjn2ZyH3HsLWof4obfKfyW00p3YKE9WQFoEF+umfN6/OMxMQNrQbc3XqkKrZSz3QexCBIZEMrdrvKg==";
        var changed = (byte[])signed.Clone(); changed[0] ^= 1;
        Check("the release signature: either key, exactly those bytes", ReleaseSignature.Valid(signed, pcSig + "\n") && ReleaseSignature.Valid(signed, macSig)
              && !ReleaseSignature.Valid(changed, pcSig) && !ReleaseSignature.Valid(changed, macSig)
              && !ReleaseSignature.Valid(signed, "") && !ReleaseSignature.Valid(signed, null) && !ReleaseSignature.Valid(signed, "not base64!"));
        Check("a release without hashes cannot install itself", !Updates.Parse("""{ "tag_name": "v9", "assets": [] }""").CanInstall);

        Check("the startup task is looked for without errors", Record(() => DockConfig.StartupTask()));

        string dir = DockConfig.Folder, file = Path.Combine(dir, "setup.bin");
        File.WriteAllBytes(file, Encoding.ASCII.GetBytes("Muelle"));
        string good = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes("Muelle"))).ToLowerInvariant();
        Check("a file that matches its hash is kept", Updates.VerifyAsync(file, good).GetAwaiter().GetResult() && File.Exists(file));
        Check("a file that does not match is deleted", !Updates.VerifyAsync(file, h1).GetAwaiter().GetResult() && !File.Exists(file));

        // Between the check and the start of the setup, the file is held open: it can be run, not changed or removed.
        string exe = Path.Combine(dir, "held.exe");
        File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), exe);
        using (var held = Updates.Hold(exe))
        {
            bool cannotWrite = false, cannotDelete = false, cannotRename = false;
            try { using var w = new FileStream(exe, FileMode.Open, FileAccess.Write, FileShare.ReadWrite); } catch (IOException) { cannotWrite = true; }
            try { File.Delete(exe); } catch (IOException) { cannotDelete = true; } catch (UnauthorizedAccessException) { cannotDelete = true; }
            try { File.Move(exe, exe + ".moved"); } catch (IOException) { cannotRename = true; } catch (UnauthorizedAccessException) { cannotRename = true; }
            Check("a held setup cannot be written, deleted or renamed", cannotWrite && cannotDelete && cannotRename && File.Exists(exe));
            int code = -1;
            try
            {
                using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, "/c exit 7")
                    { UseShellExecute = true, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden, WorkingDirectory = dir });
                if (p != null && p.WaitForExit(20000)) code = p.ExitCode;
            }
            catch (Exception ex) { Line("        error: " + ex.Message); }
            Check("a held setup can still be started", code == 7);
        }
        try { File.Delete(exe); } catch { }

        var args = Updates.SetupArguments(1234);
        Check("the setup runs in silence, waits for Muelle and starts it again",
              args.Contains("/quiet") && args.Contains("/muellewaitpid=1234") && args.Contains("/muellerelaunch=1"));
        string relaunch = Path.Combine(dir, "relaunch.json");
        Updates.WriteRelaunch(relaunch, new[] { "--settings", "--updated", "--update-failed=0x1", "C:\\a b\\c" });
        var back = Updates.ReadRelaunch(relaunch);
        Check("the arguments come back after the update, without the update's own", back.SequenceEqual(new[] { "--settings", "C:\\a b\\c" }));
        Check("and are read only once", !File.Exists(relaunch) && Updates.ReadRelaunch(relaunch).Count == 0);
        Check("a canceled setup is explained", Updates.FailureText("--update-failed=0x800704C7").Length > 0
              && Updates.FailureText("--update-failed=0x80070643").Contains("0x80070643"));
    }

    // ---------------- Updates, online ----------------
    private static void Online()
    {
        var latest = Updates.LatestAsync().GetAwaiter().GetResult();
        Line($"  (latest release on GitHub: {latest.Tag})");
        Check("GitHub names the latest release, with its setup, hashes and signature", latest.Tag.Length > 0 && latest.CanInstall);
        if (!latest.CanInstall) return;
        using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Muelle/" + Updates.Current);
        Check("its SHA256SUMS.txt carries a valid signature of the author", ReleaseSignature.Valid(
            http.GetByteArrayAsync(latest.SumsUrl).GetAwaiter().GetResult(), http.GetStringAsync(latest.SigUrl).GetAwaiter().GetResult()));
        string expected = Updates.HashFor(http.GetStringAsync(latest.SumsUrl).GetAwaiter().GetResult(), Updates.SetupName);
        Check("its SHA256SUMS.txt has the hash of the setup", expected != null);
        string file = Path.Combine(DockConfig.Folder, Updates.SetupName);
        File.WriteAllBytes(file, http.GetByteArrayAsync(latest.SetupUrl).GetAwaiter().GetResult());
        Check("the downloaded setup matches it (not run)", expected != null && Updates.VerifyAsync(file, expected).GetAwaiter().GetResult());
        File.AppendAllText(file, "x");
        Check("a changed byte makes it fail, and the file is deleted", !Updates.VerifyAsync(file, expected ?? "").GetAwaiter().GetResult() && !File.Exists(file));
    }

    // ---------------- Helpers ----------------
    private static string Sys(string file) => Path.Combine(Environment.SystemDirectory, file);
    private static DockItemConfig Item(string file) => new() { Name = "", Path = Sys(file) };

    private static void WriteConfig(Action<DockConfig> change)
    {
        var c = DockConfig.Load(out _);
        change(c);
        c.Save();
    }

    private static string Fingerprint(string file)
    {
        try { return File.Exists(file) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))) + File.GetLastWriteTimeUtc(file).Ticks : "none"; }
        catch (Exception ex) { return "unreadable: " + ex.Message; }
    }

    private static bool Record(Func<object> f)
    {
        try { Line("        " + (f()?.ToString() ?? "none")); return true; }
        catch (Exception ex) { Line("        error: " + ex.Message); return false; }
    }

    private static void Section(string name, Action body)
    {
        Line("");
        Line("== " + name);
        try { body(); }
        catch (Exception ex) { Fail(name + ": unexpected error", ex.ToString()); }
    }

    private static void Check(string name, bool ok)
    {
        if (ok) { passes++; Line("  PASS  " + name); }
        else Fail(name, null);
    }

    private static void Fail(string name, string detail)
    {
        failures++;
        Line("  FAIL  " + name + (detail != null ? ": " + detail : ""));
    }

    private static void Line(string text) => log.AppendLine(text);
}
