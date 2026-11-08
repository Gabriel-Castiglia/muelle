//
//  Program.cs
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

using System.Windows;

namespace Muelle;

internal static class Program
{
    /// <summary>Largest size of muelle.log before it is rotated to muelle.old.log.</summary>
    private const long MaxLogBytes = 512 * 1024;

    [STAThread]
    private static int Main(string[] args)
    {
        // Test options (not shown to users): "--screenshot <file.png>" renders a window to a PNG and exits,
        // without touching the configuration file; "--window dock|settings|about" picks the window
        // (dock by default) and "--language en|es" overrides the stored interface language.
        string shot = null, window = "dock", language = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--screenshot" && i + 1 < args.Length) shot = Path.GetFullPath(args[++i]);
            else if (args[i] == "--window" && i + 1 < args.Length) window = args[++i];
            else if (args[i] == "--language" && i + 1 < args.Length) language = args[++i];
        }

        // Only one dock instance at a time (a test screenshot can run separately).
        Mutex single = null;
        if (shot == null)
        {
            single = new Mutex(true, @"Local\Muelle.Dock.Single", out bool created);
            if (!created) return 0;
        }

        // The application exits only through "Quit Muelle": with several monitors there are several windows.
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (s, e) => { Log(e.Exception.ToString()); e.Handled = true; };
        var manager = new DockManager(shot, window);
        if (language != null) Loc.Current = Loc.FromCode(language);
        // Windows is signing out or shutting down: write any pending configuration change.
        app.SessionEnding += (s, e) => manager.FlushConfig();
        manager.Start();
        // "--settings": opens the settings window at startup (useful for a shortcut).
        if (shot == null && args.Contains("--settings")) manager.OpenSettings();
        int rc = app.Run();
        GC.KeepAlive(single);
        return rc;
    }

    /// <summary>Appends a line to %APPDATA%\Muelle\muelle.log, keeping the file small.</summary>
    public static void Log(string text)
    {
        try
        {
            Directory.CreateDirectory(DockConfig.Folder);
            string file = Path.Combine(DockConfig.Folder, "muelle.log");
            var info = new FileInfo(file);
            if (info.Exists && info.Length > MaxLogBytes)
                File.Move(file, Path.Combine(DockConfig.Folder, "muelle.old.log"), true);
            File.AppendAllText(file, DateTime.Now.ToString("s") + "  " + text + Environment.NewLine);
        }
        catch { }
    }
}
