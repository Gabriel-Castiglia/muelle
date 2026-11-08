//
//  AboutWindow.cs
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
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static Muelle.Loc;

namespace Muelle;

/// <summary>"About Muelle" window: version, the meaning of the name, author, copyright, license and repository.</summary>
internal sealed class AboutWindow : Window
{
    private const string Author = "Gabriel Castiglia";
    private const string LicenseUrl = "https://www.gnu.org/licenses/old-licenses/gpl-2.0.html";
    public const string Repository = "https://github.com/Gabriel-Castiglia/muelle";

    public AboutWindow()
    {
        Width = 480;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;
        Background = new SolidColorBrush(Color.FromRgb(0xF6, 0xF7, 0xF9));
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        FontSize = 13;
        Build();
        Loc.Changed += Build;
        Closed += (s, e) => Loc.Changed -= Build;
    }

    private void Build()
    {
        Title = tr("Acerca de Muelle", "About Muelle");
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        string versionText = version == null ? "" : $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";

        var root = new StackPanel { Margin = new Thickness(28, 24, 28, 20) };

        // Header: icon + name + version.
        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        try
        {
            header.Children.Add(new Image
            {
                Source = BitmapFrame.Create(new Uri("pack://application:,,,/muelle.ico")),
                Width = 56, Height = 56, Margin = new Thickness(0, 0, 16, 0),
            });
        }
        catch { /* the window still works without the icon */ }
        var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(new TextBlock { Text = "Muelle", FontSize = 26, FontWeight = FontWeights.SemiBold });
        titles.Children.Add(new TextBlock
        {
            Text = tr("Dock para Windows  ·  versión ", "A dock for Windows  ·  version ") + versionText,
            Foreground = Brushes.DimGray,
        });
        header.Children.Add(titles);
        root.Children.Add(header);

        root.Children.Add(Line(tr("«Muelle» es la palabra castellana para un muelle de puerto, en inglés «dock»: el lugar donde amarran los barcos.",
                                  "\"Muelle\" is the Spanish word for a dock, the place where ships moor."), null, 10));
        root.Children.Add(Line(tr("Creado por " + Author + " (6 de noviembre de 2026).", "Created by " + Author + " (November 6, 2026).")));
        root.Children.Add(Line("Copyright © 2026 " + Author + "."));
        var repo = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
        var link = new Hyperlink(new Run(Repository.Replace("https://", ""))) { NavigateUri = new Uri(Repository) };
        link.RequestNavigate += (s, e) => Open(Repository);
        repo.Inlines.Add(new Run(tr("Código fuente: ", "Source code: ")));
        repo.Inlines.Add(link);
        root.Children.Add(repo);
        root.Children.Add(Line(tr("Licencia: GNU General Public License, versión 2 (GPL-2.0-only).",
                                  "License: GNU General Public License, version 2 (GPL-2.0-only)."), FontWeights.SemiBold));
        root.Children.Add(new TextBlock
        {
            Text = tr("Este programa es software libre: puede redistribuirlo y/o modificarlo bajo los términos de la " +
                      "Licencia Pública General GNU, versión 2, publicada por la Free Software Foundation. Se distribuye con " +
                      "la esperanza de que sea útil, pero SIN NINGUNA GARANTÍA, ni siquiera la garantía implícita de " +
                      "COMERCIABILIDAD o IDONEIDAD PARA UN PROPÓSITO PARTICULAR.",
                      "This program is free software: you can redistribute it and/or modify it under the terms of the " +
                      "GNU General Public License, version 2, as published by the Free Software Foundation. It is distributed " +
                      "in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of " +
                      "MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE."),
            TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontSize = 12, Margin = new Thickness(0, 8, 0, 18),
        });

        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(Button(tr("Ver licencia", "View license"), ShowLicense));
        buttons.Children.Add(Button("gnu.org/licenses", () => Open(LicenseUrl)));
        var close = Button(tr("Cerrar", "Close"), Close);
        close.IsDefault = true;
        close.IsCancel = true;
        buttons.Children.Add(close);
        root.Children.Add(buttons);

        Content = root;
    }

    private static TextBlock Line(string text, FontWeight? weight = null, double after = 4) => new()
    {
        Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, after),
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
        string file = Path.Combine(AppContext.BaseDirectory, "LICENSE.txt");
        Open(File.Exists(file) ? file : LicenseUrl);
    }

    private static void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); } catch { }
    }
}
