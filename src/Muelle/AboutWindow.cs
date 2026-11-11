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
        FontSize = 13;
        Theme.Attach(this);
        Theme.Changed += SetIcon;
        Closed += (s, e) => Theme.Changed -= SetIcon;
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
        icon = new Image { Width = 64, Height = 64, Margin = new Thickness(0, 0, 16, 0) };
        RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
        SetIcon();
        header.Children.Add(icon);
        var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(new TextBlock { Text = "Muelle", FontSize = 26, FontWeight = FontWeights.SemiBold });
        titles.Children.Add(Muted(tr("Dock para Windows  ·  versión ", "A dock for Windows  ·  version ") + versionText, 13, new Thickness(0)));
        header.Children.Add(titles);
        root.Children.Add(header);

        root.Children.Add(Line(tr("«Muelle» es la palabra castellana para un muelle de puerto, en inglés «dock»: el lugar donde amarran los barcos.",
                                  "\"Muelle\" is the Spanish word for a dock, the place where ships moor."), null, 4));
        root.Children.Add(Muted(tr("Ícono: el barco de tres palos del grabado de Frans Huys según Pieter Bruegel el Viejo (hacia 1561-1565), Museo Metropolitano de Arte, dominio público.",
                                   "Icon: the three-master of Frans Huys's engraving after Pieter Bruegel the Elder (c. 1561-1565), The Metropolitan Museum of Art, public domain."),
                                12, new Thickness(0, 0, 0, 10)));
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
        root.Children.Add(Muted(tr("Este programa es software libre: puede redistribuirlo y/o modificarlo bajo los términos de la " +
                      "Licencia Pública General GNU, versión 2, publicada por la Free Software Foundation. Se distribuye con " +
                      "la esperanza de que sea útil, pero SIN NINGUNA GARANTÍA, ni siquiera la garantía implícita de " +
                      "COMERCIABILIDAD o IDONEIDAD PARA UN PROPÓSITO PARTICULAR.",
                      "This program is free software: you can redistribute it and/or modify it under the terms of the " +
                      "GNU General Public License, version 2, as published by the Free Software Foundation. It is distributed " +
                      "in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of " +
                      "MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE."), 12, new Thickness(0, 8, 0, 14)));

        // New versions: asked on request; the answer shows on the line above the buttons.
        var updateRow = new DockPanel { Margin = new Thickness(0, 0, 0, 16) };
        var check = Button(tr("Buscar versión nueva", "Check for a new version"), null);
        check.Margin = new Thickness(0, 0, 12, 0);
        var status = Muted("", 12, new Thickness(0));
        status.VerticalAlignment = VerticalAlignment.Center;
        check.Click += async (s, e) =>
        {
            check.IsEnabled = false;
            status.Text = tr("Consultando GitHub…", "Asking GitHub…");
            try
            {
                var latest = await Updates.LatestAsync();
                if (latest != null && latest > Updates.Current)
                {
                    status.Text = tr($"Está la versión {latest}.", $"Version {latest} is out.");
                    UpdateWindow.ShowNotice(latest);
                }
                else status.Text = tr("Tenés la última versión.", "You have the latest version.");
            }
            catch { status.Text = tr("No se pudo consultar GitHub.", "GitHub could not be reached."); }
            check.IsEnabled = true;
        };
        DockPanel.SetDock(check, Dock.Left);
        updateRow.Children.Add(check);
        updateRow.Children.Add(status);
        root.Children.Add(updateRow);

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

    private Image icon;

    private void SetIcon()
    {
        if (icon != null) icon.Source = Theme.Icon(null, 256);
    }

    private static TextBlock Muted(string text, double size, Thickness margin)
    {
        var t = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = size, Margin = margin };
        t.SetResourceReference(TextBlock.ForegroundProperty, "Mu.TextMuted");
        return t;
    }

    private static TextBlock Line(string text, FontWeight? weight = null, double after = 4) => new()
    {
        Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, after),
        FontWeight = weight ?? FontWeights.Normal,
    };

    private static Button Button(string text, Action action)
    {
        var b = new Button { Content = text, Margin = new Thickness(8, 0, 0, 0) };
        if (action != null) b.Click += (s, e) => action();
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
