//
//  Preview.cs
//  Muelle setup: renders setup pages to images (test option).
//
//  Copyright (C) 2026 Gabriel Castiglia. Licensed under the GNU GPL, version 2 only.
//  Date: November 8, 2026
//  SPDX-License-Identifier: GPL-2.0-only
//
//  This program is free software: you can redistribute it and/or modify it under the terms of
//  the GNU General Public License, version 2, as published by the Free Software Foundation.
//  It is distributed WITHOUT ANY WARRANTY. See the LICENSE file for details.
//

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WixToolset.BootstrapperApplicationApi;

namespace Muelle.Setup;

/// <summary>
/// Test option: renders one page of the setup window to a PNG without the setup engine, for the manuals
/// and for checking that every text fits in both languages. Pages: install, upgrade, maintenance, uninstall,
/// newer, progress, installed, uninstalled, failed.
/// </summary>
internal static class Preview
{
    public static int Render(string[] args)
    {
        string Arg(string name, string fallback)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }
        string png = Path.GetFullPath(Arg("--screenshot", "setup.png"));
        string pageName = Arg("--page", "install");
        Loc.Current = Loc.FromCode(Arg("--language", "en"));
        string version = typeof(Preview).Assembly.GetName().Version is Version v ? $"{v.Major}.{v.Minor}.{Math.Max(0, v.Build)}" : "";

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var w = new SetupWindow(null)
        {
            Version = version, OlderVersion = "1.0.0", NewerVersion = "9.0.0",
            ErrorText = "Error 0x80070643: Fatal error during installation.",
        };
        switch (pageName)
        {
            case "install": w.ShowStart(SetupState.Install); break;
            case "upgrade": w.ShowStart(SetupState.Upgrade); break;
            case "maintenance": w.ShowStart(SetupState.Maintenance); break;
            case "uninstall": w.ShowStart(SetupState.Uninstall); break;
            case "newer": w.ShowStart(SetupState.Newer); break;
            case "progress": w.OlderVersion = null; w.ShowProgress(LaunchAction.Install); w.SetProgress(45); break;
            case "installed": w.ShowResult(true, false, false); break;
            case "failed": w.ShowResult(false, false, false); break;
            default: return 2;
        }
        w.ContentRendered += (s, e) => w.Dispatcher.BeginInvoke(new Action(() =>
        {
            Save(w, png);
            app.Shutdown();
        }), DispatcherPriority.ContextIdle);
        w.Show();
        app.Run();
        return File.Exists(png) ? 0 : 1;
    }

    private static void Save(Window window, string path)
    {
        var content = (FrameworkElement)window.Content;
        double width = content.ActualWidth + content.Margin.Left + content.Margin.Right;
        double height = content.ActualHeight + content.Margin.Top + content.Margin.Bottom;
        var dpi = VisualTreeHelper.GetDpi(content);
        var rtb = new RenderTargetBitmap((int)Math.Ceiling(width * dpi.DpiScaleX), (int)Math.Ceiling(height * dpi.DpiScaleY),
                                         dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var dc = background.RenderOpen()) dc.DrawRectangle(window.Background, null, new Rect(0, 0, width, height));
        rtb.Render(background);
        rtb.Render(content);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using var fs = File.Create(path);
        enc.Save(fs);
    }
}
