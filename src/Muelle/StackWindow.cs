//
//  StackWindow.cs
//  Muelle: a dock for Windows.
//
//  Copyright (C) 2026 Gabriel Castiglia. Licensed under the GNU GPL, version 2 only.
//  Date: November 20, 2026
//  SPDX-License-Identifier: GPL-2.0-only
//
//  This program is free software: you can redistribute it and/or modify it under the terms of
//  the GNU General Public License, version 2, as published by the Free Software Foundation.
//  It is distributed WITHOUT ANY WARRANTY. See the LICENSE file for details.
//

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static Muelle.Loc;

namespace Muelle;

/// <summary>
/// Creates or edits a stack: its name, its icon (its own, or its first shortcuts in a grid) and its shortcuts, which
/// can be added, removed and reordered. Changes are made on a copy and reach the dock with "Save".
/// </summary>
internal sealed class StackWindow : Window
{
    private static StackWindow open;

    private readonly DockManager manager;
    private readonly List<DockItem> list;
    private readonly DockItem old;
    private readonly int index;
    /// <summary>The stack being edited (a copy: the dock does not change until "Save").</summary>
    public DockItemConfig Stack { get; }
    private readonly List<DockItem> shown = new();
    /// <summary>The stack as it was opened, to tell whether it has changed since (<see cref="Dirty"/>).</summary>
    private readonly string saved;

    private TextBox nameBox;
    private Image preview;
    private ListBox entries;

    /// <summary>
    /// Opens the editor for a stack of a list, or for a new stack (null) at a position (one editor at a time). An
    /// editor already open is never closed in silence: for the same stack it comes to the front, and if it holds
    /// changes that were not saved, the user is asked first whether to drop them.
    /// </summary>
    public static void Edit(DockManager manager, List<DockItem> list, DockItem stack, int index)
    {
        if (open != null)
        {
            if (stack != null && open.old == stack && open.list == list)
            {
                open.Front();
                return;
            }
            if (open.Dirty)
            {
                open.Front();
                var answer = MessageBox.Show(open,
                    tr($"La pila «{open.Stack.Name}» tiene cambios sin guardar. ¿Descartarlos y editar otra pila?",
                       $"The stack “{open.Stack.Name}” has changes that were not saved. Discard them and edit another stack?",
                       $"スタック「{open.Stack.Name}」には保存していない変更があります。変更を破棄して別のスタックを編集しますか?"),
                    "Muelle", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
                if (answer != MessageBoxResult.Yes) return;
            }
            open.Close();
        }
        open = new StackWindow(manager, list, stack, index);
        open.Closed += (s, e) => { if (s == open) open = null; };
        open.Show();
        open.Activate();
    }

    public StackWindow(DockManager manager, List<DockItem> list, DockItem stack, int index)
    {
        this.manager = manager;
        this.list = list;
        old = stack;
        this.index = index;
        Stack = stack?.Cfg.Clone() ?? new DockItemConfig { Name = tr("Pila", "Stack", "スタック"), Stack = new() };
        foreach (var c in Stack.Stack) shown.Add(DockItem.From(c));
        saved = Snapshot();

        Width = 460;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontSize = 13;
        Topmost = true;
        Theme.Attach(this);
        Build();
        // A change of language rebuilds the texts; what is being edited lives in Stack and shown, not in the controls.
        Loc.Changed += Build;
        Closed += (s, e) => Loc.Changed -= Build;
    }

    private void Build()
    {
        int selected = entries?.SelectedIndex ?? 0;   // kept across a rebuild for a change of language
        Title = old == null ? tr("Nueva pila", "New stack", "新しいスタック") : tr("Editar pila", "Edit stack", "スタックの編集");
        var root = new StackPanel { Margin = new Thickness(20, 16, 20, 14) };

        // Icon and name.
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        preview = new Image { Width = 56, Height = 56, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top };
        RenderOptions.SetBitmapScalingMode(preview, BitmapScalingMode.HighQuality);
        DockPanel.SetDock(preview, Dock.Left);
        head.Children.Add(preview);
        var fields = new StackPanel();
        fields.Children.Add(new TextBlock { Text = tr("Nombre:", "Name:", "名前:"), Margin = new Thickness(0, 0, 0, 3) });
        nameBox = new TextBox { Text = Stack.Name, Margin = new Thickness(0, 0, 0, 8) };
        nameBox.TextChanged += (s, e) => Stack.Name = nameBox.Text;
        fields.Children.Add(nameBox);
        var iconRow = new WrapPanel();
        iconRow.Children.Add(Button(tr("Elegir icono…", "Choose icon…", "アイコンを選択…"), ChooseIcon));
        iconRow.Children.Add(Button(tr("Icono automático", "Automatic icon", "自動アイコン"), () => { Stack.Icon = null; RefreshPreview(); }));
        fields.Children.Add(iconRow);
        head.Children.Add(fields);
        root.Children.Add(head);

        // Shortcuts.
        root.Children.Add(new TextBlock { Text = tr("Accesos de la pila:", "Shortcuts in the stack:", "スタック内のショートカット:"), Margin = new Thickness(0, 0, 0, 4) });
        entries = new ListBox { Height = 180, Margin = new Thickness(0, 0, 0, 8) };
        root.Children.Add(entries);
        var buttons = new WrapPanel();
        buttons.Children.Add(Button(tr("Agregar…", "Add…", "追加…"), AddWithDialog));
        buttons.Children.Add(Button(tr("Quitar", "Remove", "削除"), () => WithSelected(i => { RemoveAt(i); RefreshList(i - 1); })));
        buttons.Children.Add(Button(tr("Subir", "Move up", "上へ"), () => WithSelected(i => { if (i > 0) { Move(i, i - 1); RefreshList(i - 1); } })));
        buttons.Children.Add(Button(tr("Bajar", "Move down", "下へ"), () => WithSelected(i => { if (i < shown.Count - 1) { Move(i, i + 1); RefreshList(i + 1); } })));
        root.Children.Add(buttons);

        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var cancel = Button(tr("Cancelar", "Cancel", "キャンセル"), Close);
        cancel.IsCancel = true;
        var save = Button(tr("Guardar", "Save", "保存"), Save);
        save.IsDefault = true;
        save.Margin = new Thickness(0);
        footer.Children.Add(cancel);
        footer.Children.Add(save);
        root.Children.Add(footer);
        Content = root;

        RefreshList(selected);
    }

    // ---------------- Editing (also used by the self-test) ----------------
    public void AddPaths(IEnumerable<string> paths)
    {
        foreach (var raw in paths)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            string p = raw.Trim();
            if (Stack.Stack.Any(c => string.Equals(c.Path, p, StringComparison.OrdinalIgnoreCase))) continue;
            var cfg = new DockItemConfig { Name = DockItem.NiceName(p), Path = p };
            Stack.Stack.Add(cfg);
            shown.Add(DockItem.From(cfg));
        }
        RefreshList(shown.Count - 1);
    }

    public void RemoveAt(int i)
    {
        if (i < 0 || i >= shown.Count) return;
        Stack.Stack.RemoveAt(i);
        shown.RemoveAt(i);
    }

    public void Move(int from, int to)
    {
        if (from < 0 || from >= shown.Count || to < 0 || to >= shown.Count) return;
        var c = Stack.Stack[from]; Stack.Stack.RemoveAt(from); Stack.Stack.Insert(to, c);
        var d = shown[from]; shown.RemoveAt(from); shown.Insert(to, d);
    }

    /// <summary>Puts the stack in the dock (replacing the one edited) and closes.</summary>
    public void Save()
    {
        if (string.IsNullOrWhiteSpace(Stack.Name)) Stack.Name = tr("Pila", "Stack", "スタック");
        Stack.Name = Stack.Name.Trim();
        // While it was being edited the stack left the dock (it was unpinned), or its dock is no longer shown (each
        // screen with its own icons was turned off): saving would bring the stack back, or put it where it is not seen.
        if ((old != null && !list.Contains(old)) || !manager.IsShown(list))
        {
            MessageBox.Show(this, tr("La pila ya no está en el dock (se quitó o cambió la lista de iconos mientras la editabas): no se guardó.",
                                     "The stack is no longer in the dock (it was removed, or the list of icons changed while you edited it): it was not saved.",
                                     "スタックはもうドックにありません (編集中に削除されたか、アイコンの一覧が変わりました)。保存しませんでした。"),
                            "Muelle", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
            return;
        }
        manager.PutStack(list, Stack.Clone(), old, index);
        Close();
    }

    /// <summary>Whether the stack has changes that "Save" has not taken to the dock yet.</summary>
    public bool Dirty => Snapshot() != saved;

    private string Snapshot() => System.Text.Json.JsonSerializer.Serialize(Stack);

    // ---------------- Helpers ----------------
    private void Front()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    private void AddWithDialog()
    {
        var dlg = Ui.AppsDialog(tr("Agregar a la pila", "Add to the stack", "スタックに追加"));
        if (dlg.ShowDialog(this) == true) AddPaths(dlg.FileNames);
    }

    private void ChooseIcon()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = tr("Icono de la pila", "Stack icon", "スタックのアイコン"),
            Filter = tr("Iconos, programas e imágenes", "Icons, programs and pictures", "アイコン、プログラム、画像") + "|*.ico;*.exe;*.dll;*.png;*.lnk|"
                   + tr("Todos los archivos", "All files", "すべてのファイル") + "|*.*",
            DereferenceLinks = false,
        };
        if (dlg.ShowDialog(this) == true) { Stack.Icon = dlg.FileName; RefreshPreview(); }
    }

    private void RefreshPreview()
    {
        ImageSource icon = string.IsNullOrWhiteSpace(Stack.Icon) ? null : Shell.LoadChosenIcon(Stack.Icon);
        preview.Source = icon ?? DockItem.StackIcon(shown.Select(c => c.Icon));
    }

    private void RefreshList(int select)
    {
        entries.Items.Clear();
        foreach (var c in shown)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new Image { Source = c.Icon, Width = 20, Height = 20, Margin = new Thickness(0, 0, 8, 0) });
            row.Children.Add(new TextBlock { Text = c.Name, VerticalAlignment = VerticalAlignment.Center });
            entries.Items.Add(new ListBoxItem { Content = row });
        }
        if (entries.Items.Count > 0) entries.SelectedIndex = Math.Clamp(select, 0, entries.Items.Count - 1);
        RefreshPreview();
    }

    private void WithSelected(Action<int> action)
    {
        if (entries.SelectedIndex >= 0) action(entries.SelectedIndex);
    }

    private static Button Button(string text, Action onClick) => Ui.Button(text, onClick);
}
