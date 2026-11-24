//
//  RenameWindow.cs
//  Muelle: a dock for Windows.
//
//  Copyright (C) 2026 Gabriel Castiglia. Licensed under the GNU GPL, version 2 only.
//  Date: November 24, 2026
//  SPDX-License-Identifier: GPL-2.0-only
//
//  This program is free software: you can redistribute it and/or modify it under the terms of
//  the GNU General Public License, version 2, as published by the Free Software Foundation.
//  It is distributed WITHOUT ANY WARRANTY. See the LICENSE file for details.
//

using System.Windows;
using System.Windows.Controls;
using static Muelle.Loc;

namespace Muelle;

/// <summary>Asks for the name of a dock icon. An empty name means the automatic one (the program's own).</summary>
internal sealed class RenameWindow : Window
{
    private readonly TextBox box;
    private bool accepted;

    /// <summary>Shows the window over the dock; returns the new name, or null if it was canceled.</summary>
    public static string Ask(Window owner, string current)
    {
        var w = new RenameWindow(current);
        try { w.Owner = owner; } catch { }
        w.ShowDialog();
        return w.accepted ? w.box.Text : null;
    }

    private RenameWindow(string current)
    {
        Title = tr("Renombrar", "Rename", "名前の変更");
        Width = 380;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;
        FontSize = 13;
        Topmost = true;
        Theme.Attach(this);

        var root = new StackPanel { Margin = new Thickness(20, 16, 20, 14) };
        root.Children.Add(new TextBlock { Text = tr("Nombre:", "Name:", "名前:"), Margin = new Thickness(0, 0, 0, 3) });
        box = new TextBox { Text = current ?? "", Margin = new Thickness(0, 0, 0, 4) };
        root.Children.Add(box);
        root.Children.Add(new TextBlock
        {
            Text = tr("Vacío: vuelve al nombre del programa.", "Empty: goes back to the program's name.", "空欄にするとプログラム名に戻ります。"),
            FontSize = 11.5, Opacity = 0.7, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10),
        });

        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = tr("Cancelar", "Cancel", "キャンセル"), IsCancel = true, Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (s, e) => Close();
        var save = new Button { Content = tr("Guardar", "Save", "保存"), IsDefault = true };
        save.Click += (s, e) => { accepted = true; Close(); };
        footer.Children.Add(cancel);
        footer.Children.Add(save);
        root.Children.Add(footer);
        Content = root;

        Loaded += (s, e) => { box.Focus(); box.SelectAll(); };
    }
}
