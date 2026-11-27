//
//  Program.cs
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

namespace Muelle;

internal static class Program
{
    /// <summary>Largest size of muelle.log before it is rotated to muelle.old.log.</summary>
    private const long MaxLogBytes = 512 * 1024;
    private static readonly object LogLock = new();

    [STAThread]
    private static int Main(string[] args)
    {
        // Only one dock instance at a time (a test screenshot or the self-test can run separately). Checked before
        // anything else: a second instance must not read (and delete) the arguments an update left for the first.
        Mutex single = null;
        if (!args.Contains("--screenshot") && !args.Contains("--self-test"))
        {
            single = new Mutex(true, @"Local\Muelle.Dock.Single", out bool created);
            if (!created) return 0;
        }

        // Started again by the setup after an update: the arguments it had before come back.
        args = Updates.AfterUpdateArgs(args, out bool updated, out string updateFailure);

        // Test options (not shown to users): "--screenshot <file.png>" renders a window to a PNG and exits
        // without touching the configuration file;
        // "--window dock|settings|settings-locked|about|update|menu|stack|stack-editor" picks the window (dock by default); "--language en|es|ja", "--theme light|dark" and "--accent <key>"
        // override the stored interface language, appearance and colour; "--sample" shows example apps and a stack
        // instead of the user's own. "--self-test <log>" checks the main parts on a throwaway configuration
        // (see SelfTest.cs) and exits with the number of failures.
        string shot = null, window = "dock", language = null, theme = null, accent = null, selfTest = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--self-test" && i + 1 < args.Length) selfTest = Path.GetFullPath(args[++i]);
            else if (args[i] == "--screenshot" && i + 1 < args.Length) shot = Path.GetFullPath(args[++i]);
            else if (args[i] == "--window" && i + 1 < args.Length) window = args[++i];
            else if (args[i] == "--language" && i + 1 < args.Length) language = args[++i];
            else if (args[i] == "--theme" && i + 1 < args.Length) theme = args[++i];
            else if (args[i] == "--accent" && i + 1 < args.Length) accent = args[++i];
        }

        // The self-test never touches the dock that may be running, nor its configuration.
        if (selfTest != null) return SelfTest.Run(selfTest);
        // Example screenshots start from the default settings, not from those of whoever takes them.
        bool sample = shot != null && args.Contains("--sample");
        if (sample) DockConfig.FolderOverride = Path.Combine(Path.GetTempPath(), "MuelleSample-" + Environment.ProcessId);

        // An error that escapes everything (for example while loading the styles at start) goes to the log
        // before the process ends; without this it would only reach the Windows event log.
        AppDomain.CurrentDomain.UnhandledException += (s, e) => Log("Fatal error: " + e.ExceptionObject);

        // The application exits only through "Quit Muelle": with several monitors there are several windows.
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (s, e) => { Log(e.Exception.ToString()); e.Handled = true; };
        var manager = new DockManager(shot, window);
        if (language != null) Loc.Current = Loc.FromCode(language);
        if (sample) manager.UseSample();
        if (shot != null && (theme != null || accent != null))
        {
            // Only in memory: a screenshot never writes the configuration.
            manager.Config.Appearance = theme ?? manager.Config.Appearance;
            manager.Config.Accent = Theme.Get(accent ?? manager.Config.Accent).Key;
            Theme.Set(manager.Config.Appearance, manager.Config.Accent);
        }
        // Windows is signing out or shutting down: write any pending configuration change.
        app.SessionEnding += (s, e) => manager.FlushConfig();
        manager.Start();
        // "--settings": opens the settings window at startup (useful for a shortcut).
        if (shot == null && args.Contains("--settings")) manager.OpenSettings();
        if (updated || updateFailure != null) UpdateWindow.ShowResult(updated, updateFailure);
        int rc = app.Run();
        if (sample) try { Directory.Delete(DockConfig.FolderOverride, true); } catch { }
        GC.KeepAlive(single);
        return rc;
    }

    /// <summary>Appends a line to %APPDATA%\Muelle\muelle.log, keeping the file small.</summary>
    public static void Log(string text)
    {
        // Lines also come from background threads (icon refresh, update clean-up): one writer at a time, or two
        // lines at once would make one of the appends fail and that line would be lost.
        lock (LogLock)
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
}
