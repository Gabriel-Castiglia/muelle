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

namespace Muelle;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        string shot = null;
        for (int i = 0; i < args.Length; i++)
            if (args[i] == "--screenshot" && i + 1 < args.Length) shot = Path.GetFullPath(args[++i]);

        // Only one dock instance at a time (the test screenshot can run separately).
        Mutex single = null;
        if (shot == null)
        {
            single = new Mutex(true, @"Local\Muelle.Dock.Single", out bool created);
            if (!created) return 0;
        }

        // The application exits only via "Salir de Muelle": with several monitors there are several windows.
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (s, e) => { Log(e.Exception.ToString()); e.Handled = true; };
        var manager = new DockManager(shot);
        manager.Rebuild();
        // "--settings": opens the settings window at startup (useful for a shortcut).
        if (args.Contains("--settings")) manager.OpenSettings();
        int rc = app.Run();
        GC.KeepAlive(single);
        return rc;
    }

    public static void Log(string text)
    {
        try
        {
            Directory.CreateDirectory(DockConfig.Folder);
            File.AppendAllText(Path.Combine(DockConfig.Folder, "muelle.log"), DateTime.Now.ToString("s") + "  " + text + Environment.NewLine);
        }
        catch { }
    }
}
