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

using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Muelle;

/// <summary>"About Muelle" window: version, author, copyright and license (GPL v3 or later).</summary>
internal sealed class AboutWindow : Window
{
    private const string Author = "M. Gabriel Castiglia";
    private const string Created = "2026-11-06";
    private const string LicenseName = "GNU General Public License v3.0 o posterior (GPL-3.0-or-later)";
    private const string LicenseUrl = "https://www.gnu.org/licenses/gpl-3.0.html";

    public AboutWindow()
    {
        Title = "Acerca de Muelle";
        Width = 480;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;
        Background = new SolidColorBrush(Color.FromRgb(0xF6, 0xF7, 0xF9));
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 13;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        string versionText = version == null ? "" : (version.Build > 0 ? $"{version.Major}.{version.Minor}.{version.Build}" : $"{version.Major}.{version.Minor}");

        var root = new StackPanel { Margin = new Thickness(28, 24, 28, 20) };

        // Header: icon + name + version.
        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        try
        {
            var icon = new Image
            {
                Source = BitmapFrame.Create(new Uri("pack://application:,,,/muelle.ico")),
                Width = 56, Height = 56, Margin = new Thickness(0, 0, 16, 0),
            };
            header.Children.Add(icon);
        }
        catch { /* the window still works without the icon */ }
        var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(new TextBlock { Text = "Muelle", FontSize = 26, FontWeight = FontWeights.SemiBold });
        titles.Children.Add(new TextBlock { Text = "Dock para Windows  ·  versión " + versionText, Foreground = Brushes.DimGray });
        header.Children.Add(titles);
        root.Children.Add(header);

        root.Children.Add(Line("Creado por " + Author + " (" + Created + ")."));
        root.Children.Add(Line("Copyright © 2026 " + Author + "."));
        root.Children.Add(Line("Licencia: " + LicenseName + ".", FontWeights.SemiBold));
        root.Children.Add(new TextBlock
        {
            Text = "Este programa es software libre: puede redistribuirlo y/o modificarlo bajo los términos de la " +
                   "Licencia Pública General GNU publicada por la Free Software Foundation, versión 3 o (a su elección) " +
                   "cualquier versión posterior. Se distribuye con la esperanza de que sea útil, pero SIN NINGUNA GARANTÍA, " +
                   "ni siquiera la garantía implícita de COMERCIABILIDAD o IDONEIDAD PARA UN PROPÓSITO PARTICULAR.",
            TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontSize = 12, Margin = new Thickness(0, 8, 0, 18),
        });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(Button("Ver licencia", ShowLicense));
        buttons.Children.Add(Button("gnu.org/licenses", () => Open(LicenseUrl)));
        var close = Button("Cerrar", Close);
        close.IsDefault = true;
        close.IsCancel = true;
        buttons.Children.Add(close);
        root.Children.Add(buttons);

        Content = root;
    }

    private static TextBlock Line(string text, FontWeight? weight = null) => new()
    {
        Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4),
        FontWeight = weight ?? FontWeights.Normal,
    };

    private static Button Button(string text, Action action)
    {
        var b = new Button { Content = text, Padding = new Thickness(14, 5, 14, 5), Margin = new Thickness(8, 0, 0, 0), MinWidth = 84 };
        b.Click += (s, e) => action();
        return b;
    }

    /// <summary>Opens the LICENSE.txt shipped next to the executable; falls back to the GNU website.</summary>
    private static void ShowLicense()
    {
        string file = System.IO.Path.Combine(AppContext.BaseDirectory, "LICENSE.txt");
        Open(System.IO.File.Exists(file) ? file : LicenseUrl);
    }

    private static void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); } catch { }
    }
}
