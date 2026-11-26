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

    private readonly DockManager manager;

    public AboutWindow(DockManager manager)
    {
        this.manager = manager;
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
        Title = tr("Acerca de Muelle", "About Muelle", "Muelle について");
        string versionText = Updates.Current.ToString();

        var root = new StackPanel { Margin = new Thickness(28, 24, 28, 20) };

        // Header: icon + name + version.
        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        icon = new Image { Width = 64, Height = 64, Margin = new Thickness(0, 0, 16, 0) };
        RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
        SetIcon();
        header.Children.Add(icon);
        var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(new TextBlock { Text = "Muelle", FontSize = 26, FontWeight = FontWeights.SemiBold });
        titles.Children.Add(Muted(tr("Dock para Windows  ·  versión ", "A dock for Windows  ·  version ", "Windows 用ドック  ·  バージョン ") + versionText, 13, new Thickness(0)));
        header.Children.Add(titles);
        root.Children.Add(header);

        root.Children.Add(Line(tr("«Muelle» es la palabra castellana para un muelle de puerto, en inglés «dock»: el lugar donde amarran los barcos.",
                                  "\"Muelle\" is the Spanish word for a dock, the place where ships moor.", "「Muelle」はスペイン語で埠頭 (ドック)、船が係留する場所を意味します。"), null, 4));
        root.Children.Add(Muted(tr("Icono: el barco de tres palos del grabado de Frans Huys según Pieter Bruegel el Viejo (hacia 1561-1565), Museo Metropolitano de Arte, dominio público.",
                                   "Icon: the three-master of Frans Huys's engraving after Pieter Bruegel the Elder (c. 1561-1565), The Metropolitan Museum of Art, public domain.", "アイコン: ピーテル・ブリューゲル (父) の原画によるフランス・ハイスの版画 (1561-1565 年頃) の三本マストの帆船。メトロポリタン美術館、パブリックドメイン。"),
                                12, new Thickness(0, 0, 0, 10)));
        root.Children.Add(Line(tr("Creado por " + Author + " (6 de noviembre de 2026).", "Created by " + Author + " (November 6, 2026).", "作者: " + Author + " (2026 年 11 月 6 日)。")));
        root.Children.Add(Line("Copyright © 2026 " + Author + "."));
        var repo = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
        var link = new Hyperlink(new Run(Repository.Replace("https://", ""))) { NavigateUri = new Uri(Repository) };
        link.RequestNavigate += (s, e) => Shell.Open(Repository);
        repo.Inlines.Add(new Run(tr("Código fuente: ", "Source code: ", "ソースコード: ")));
        repo.Inlines.Add(link);
        root.Children.Add(repo);
        root.Children.Add(Line(tr("Licencia: GNU General Public License, versión 2 (GPL-2.0-only).",
                                  "License: GNU General Public License, version 2 (GPL-2.0-only).", "ライセンス: GNU General Public License バージョン 2 (GPL-2.0-only)。"), FontWeights.SemiBold));
        root.Children.Add(Muted(tr("Este programa es software libre: puede redistribuirlo y/o modificarlo bajo los términos de la " +
                      "Licencia Pública General GNU, versión 2, publicada por la Free Software Foundation. Se distribuye con " +
                      "la esperanza de que sea útil, pero SIN NINGUNA GARANTÍA, ni siquiera la garantía implícita de " +
                      "COMERCIABILIDAD o IDONEIDAD PARA UN PROPÓSITO PARTICULAR.",
                      "This program is free software: you can redistribute it and/or modify it under the terms of the " +
                      "GNU General Public License, version 2, as published by the Free Software Foundation. It is distributed " +
                      "in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of " +
                      "MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.", "このプログラムはフリーソフトウェアです。Free Software Foundation が公表した GNU 一般公衆利用許諾書 バージョン 2 の条件の下で、" +
                      "再頒布および改変することができます。このプログラムは有用であることを願って頒布されますが、" +
                      "商品性や特定目的への適合性の黙示の保証も含め、いかなる保証もありません。"), 12, new Thickness(0, 8, 0, 14)));

        // New versions: asked on request; the answer shows on the line above the buttons.
        var updateRow = new DockPanel { Margin = new Thickness(0, 0, 0, 16) };
        var check = Button(tr("Buscar actualizaciones", "Check for updates", "更新を確認"), null);
        check.Margin = new Thickness(0, 0, 12, 0);
        var status = Muted("", 12, new Thickness(0));
        status.VerticalAlignment = VerticalAlignment.Center;
        check.Click += async (s, e) =>
        {
            check.IsEnabled = false;
            status.Text = tr("Consultando GitHub…", "Asking GitHub…", "GitHub に問い合わせています…");
            status.Text = await Updates.CheckAsync(manager);
            check.IsEnabled = true;
        };
        DockPanel.SetDock(check, Dock.Left);
        updateRow.Children.Add(check);
        updateRow.Children.Add(status);
        root.Children.Add(updateRow);

        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(Button(tr("Ver licencia", "View license", "ライセンスを表示"), ShowLicense));
        buttons.Children.Add(Button("gnu.org/licenses", () => Shell.Open(LicenseUrl)));
        var close = Button(tr("Cerrar", "Close", "閉じる"), Close);
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

    private static TextBlock Muted(string text, double size, Thickness margin) => Ui.Muted(text, size, margin);

    private static TextBlock Line(string text, FontWeight? weight = null, double after = 4) => new()
    {
        Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, after),
        FontWeight = weight ?? FontWeights.Normal,
    };

    /// <summary>A button of the bottom rows, spaced from the one on its left; <paramref name="action"/> may be set later.</summary>
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
        Shell.Open(File.Exists(file) ? file : LicenseUrl);
    }
}
