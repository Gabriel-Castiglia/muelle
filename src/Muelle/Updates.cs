//
//  Updates.cs
//  Muelle: a dock for Windows.
//
//  Copyright (C) 2026 Gabriel Castiglia. Licensed under the GNU GPL, version 2 only.
//  Date: November 11, 2026
//  SPDX-License-Identifier: GPL-2.0-only
//
//  This program is free software: you can redistribute it and/or modify it under the terms of
//  the GNU General Public License, version 2, as published by the Free Software Foundation.
//  It is distributed WITHOUT ANY WARRANTY. See the LICENSE file for details.
//

using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static Muelle.Loc;

namespace Muelle;

/// <summary>
/// Asks GitHub for the latest published version of Muelle. Nothing is sent but the request itself
/// (with "Muelle/&lt;version&gt;" as the program name), and nothing is downloaded or installed: the user
/// decides whether to open the download page.
/// </summary>
internal static class Updates
{
    private const string Api = "https://api.github.com/repos/Gabriel-Castiglia/muelle/releases/latest";
    public const string Page = AboutWindow.Repository + "/releases/latest";

    /// <summary>The running version, as major.minor.patch.</summary>
    public static Version Current
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        }
    }

    /// <summary>The latest version on GitHub when it is newer than this one; null if it is not, or if GitHub did not answer.</summary>
    public static async Task<Version> NewerAsync()
    {
        try
        {
            var latest = await LatestAsync();
            return latest != null && latest > Current ? latest : null;
        }
        catch (Exception ex)
        {
            Program.Log("Could not check for a new version: " + ex.Message);
            return null;
        }
    }

    /// <summary>The latest published version; throws if GitHub cannot be reached.</summary>
    public static async Task<Version> LatestAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Muelle/" + Current);
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        using var doc = JsonDocument.Parse(await http.GetStringAsync(Api));
        var tag = doc.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v', 'V');
        return Version.TryParse(tag, out var v) ? new Version(v.Major, v.Minor, Math.Max(0, v.Build)) : null;
    }

    public static void OpenPage()
    {
        try { Process.Start(new ProcessStartInfo(Page) { UseShellExecute = true }); } catch { }
    }
}

/// <summary>The small window that says a newer version is out, with a button to its download page.</summary>
internal sealed class UpdateWindow : Window
{
    private static UpdateWindow open;

    /// <summary>Shows the notice (only one at a time).</summary>
    public static void ShowNotice(Version newer)
    {
        if (open != null) { open.Activate(); return; }
        open = new UpdateWindow(newer);
        open.Closed += (s, e) => open = null;
        open.Show();
        open.Activate();
    }

    public UpdateWindow(Version newer)
    {
        Width = 440;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontSize = 13;
        Topmost = true;
        Theme.Attach(this);
        Build(newer);
        void Rebuild() => Build(newer);
        Loc.Changed += Rebuild;
        Closed += (s, e) => Loc.Changed -= Rebuild;
    }

    private void Build(Version newer)
    {
        Title = "Muelle";
        var root = new StackPanel { Margin = new Thickness(24, 20, 24, 18) };
        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        var icon = new Image { Width = 48, Height = 48, Margin = new Thickness(0, 0, 14, 0) };
        icon.Source = Theme.Icon(null, 256);
        RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
        header.Children.Add(icon);
        var title = new TextBlock
        {
            Text = tr("Hay una versión nueva de Muelle", "A new version of Muelle is out"),
            FontSize = 17, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap,
        };
        header.Children.Add(title);
        root.Children.Add(header);
        root.Children.Add(new TextBlock
        {
            Text = tr($"Muelle {newer} está en GitHub; tenés la {Updates.Current}. La página de descarga tiene el instalador y lo que cambió.",
                      $"Muelle {newer} is on GitHub; you have {Updates.Current}. The download page has the setup and what changed."),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 18),
        });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var later = new Button { Content = tr("Ahora no", "Not now"), IsCancel = true, Margin = new Thickness(0, 0, 8, 0) };
        later.Click += (s, e) => Close();
        var get = new Button { Content = tr("Ir a la descarga", "Go to the download"), IsDefault = true };
        get.Click += (s, e) => { Updates.OpenPage(); Close(); };
        buttons.Children.Add(later);
        buttons.Children.Add(get);
        root.Children.Add(buttons);
        Content = root;
    }
}
