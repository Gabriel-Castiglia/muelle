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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Muelle;

/// <summary>
/// Settings window: gathers all options on a single screen, grouped by topic.
/// Every change is applied immediately; no "OK" button is needed.
/// </summary>
internal sealed class SettingsWindow : Window
{
    private readonly DockManager manager;
    private DockConfig Config => manager.Config;

    private readonly ListBox appList = new() { Height = 120, Margin = new Thickness(0, 4, 0, 6) };
    private readonly Slider zoomSlider = new() { Minimum = 0, Maximum = 100, TickFrequency = 10, IsSnapToTickEnabled = true };
    private readonly TextBlock zoomValue = new() { Width = 130, VerticalAlignment = VerticalAlignment.Center };
    private readonly ComboBox monitorCombo = new() { Margin = new Thickness(22, 4, 0, 0), MinWidth = 300, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly StackPanel backdropGroup = new();
    private readonly StackPanel zoomRow = new() { Orientation = Orientation.Horizontal };
    private readonly StackPanel panelRow = new() { Orientation = Orientation.Horizontal };

    public SettingsWindow(DockManager manager)
    {
        this.manager = manager;
        Title = "Configuración de Muelle";
        // Moderate, resizable size: two columns to keep it short; scrolls if it does not fit.
        Width = 900;
        Height = Math.Min(560, SystemParameters.WorkArea.Height - 40);
        MinWidth = 520;
        MinHeight = 320;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        FontSize = 13;
        Background = new SolidColorBrush(Color.FromRgb(0xF7, 0xF7, 0xF9));

        var left = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
        left.Children.Add(BuildStyle());
        left.Children.Add(BuildPosition());
        left.Children.Add(BuildBehavior());
        var right = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
        right.Children.Add(BuildSize());
        right.Children.Add(BuildApps());

        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition());
        columns.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(right, 1);
        columns.Children.Add(left);
        columns.Children.Add(right);

        var footer = BuildFooter();
        System.Windows.Controls.DockPanel.SetDock(footer, System.Windows.Controls.Dock.Bottom);
        var layout = new DockPanel { Margin = new Thickness(16, 8, 16, 12) };
        layout.Children.Add(footer);
        layout.Children.Add(new ScrollViewer { Content = columns, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = layout;

        RefreshEnabled();
        RefreshApps();
        Activated += (s, e) => RefreshApps();
    }

    // ---------------- Sections ----------------
    private UIElement BuildStyle()
    {
        var (box, card) = Section("Estilo");
        box.Children.Add(Radio("style", "Dock flotante (estilo Mac)", "Iconos centrados que se amplían al pasar el puntero.",
            Config.Style == DockStyle.Dock, () => { Config.Style = DockStyle.Dock; Apply(); }));
        box.Children.Add(Radio("style", "Panel fijo (estilo Ubuntu)", "Barra continua de borde a borde, con los iconos alineados al inicio.",
            Config.Style == DockStyle.Panel, () => { Config.Style = DockStyle.Panel; Apply(); }));

        panelRow.Margin = new Thickness(22, 4, 0, 0);
        panelRow.Children.Add(new TextBlock { Text = "Opacidad del panel:", Width = 128, VerticalAlignment = VerticalAlignment.Center });
        var opacity = new Slider { Minimum = 0, Maximum = 100, TickFrequency = 5, IsSnapToTickEnabled = true, Width = 160, Value = Config.PanelOpacity };
        var opacityValue = new TextBlock { Text = Config.PanelOpacity + " %", Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        opacity.ValueChanged += (s, e) =>
        {
            Config.PanelOpacity = (int)opacity.Value;
            opacityValue.Text = Config.PanelOpacity + " %";
            manager.SaveConfig();
            manager.KickAll();
        };
        panelRow.Children.Add(opacity);
        panelRow.Children.Add(opacityValue);
        box.Children.Add(panelRow);
        return card;
    }

    private UIElement BuildPosition()
    {
        var (box, card) = Section("Ubicación");

        var sideRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 8) };
        sideRow.Children.Add(new TextBlock { Text = "Borde de la pantalla:", Width = 150, VerticalAlignment = VerticalAlignment.Center });
        var side = new ComboBox { Width = 160 };
        var sides = new[] { ("Izquierda", DockEdge.Left), ("Derecha", DockEdge.Right), ("Arriba", DockEdge.Top), ("Abajo", DockEdge.Bottom) };
        foreach (var (name, _) in sides) side.Items.Add(name);
        side.SelectedIndex = Array.FindIndex(sides, x => x.Item2 == Config.Edge);
        side.SelectionChanged += (s, e) => { if (side.SelectedIndex >= 0) { Config.Edge = sides[side.SelectedIndex].Item2; Apply(); } };
        sideRow.Children.Add(side);
        box.Children.Add(sideRow);

        box.Children.Add(new TextBlock { Text = "Mostrar el dock en:", Margin = new Thickness(0, 0, 0, 2) });
        var monitors = Monitors.All();
        bool specific = !Config.AllMonitors && !string.IsNullOrEmpty(Config.Monitor);
        box.Children.Add(Radio("mon", "Sólo en la pantalla principal", null, !Config.AllMonitors && !specific,
            () => { Config.AllMonitors = false; Config.Monitor = ""; Apply(); }));
        box.Children.Add(Radio("mon", "En todas las pantallas (una copia en cada una)", null, Config.AllMonitors,
            () => { Config.AllMonitors = true; Apply(); }));
        box.Children.Add(Radio("mon", "En una pantalla concreta:", null, specific, () =>
        {
            Config.AllMonitors = false;
            if (monitorCombo.SelectedIndex < 0) monitorCombo.SelectedIndex = 0;
            Config.Monitor = monitors[monitorCombo.SelectedIndex].Device;
            Apply();
        }));
        for (int i = 0; i < monitors.Count; i++)
        {
            var m = monitors[i];
            monitorCombo.Items.Add($"Pantalla {i + 1}  ·  {m.Bounds.Width}×{m.Bounds.Height}  ·  {Math.Round(m.Scale * 100)} %" + (m.Primary ? "  (principal)" : ""));
            if (string.Equals(m.Device, Config.Monitor, StringComparison.OrdinalIgnoreCase)) monitorCombo.SelectedIndex = i;
        }
        monitorCombo.SelectionChanged += (s, e) =>
        {
            if (monitorCombo.SelectedIndex < 0 || Config.AllMonitors || string.IsNullOrEmpty(Config.Monitor)) return;
            Config.Monitor = monitors[monitorCombo.SelectedIndex].Device;
            Apply();
        };
        box.Children.Add(monitorCombo);
        return card;
    }

    private UIElement BuildSize()
    {
        var (box, card) = Section("Iconos");

        var sizeRow = new StackPanel { Orientation = Orientation.Horizontal };
        sizeRow.Children.Add(new TextBlock { Text = "Tamaño:", Width = 150, VerticalAlignment = VerticalAlignment.Center });
        var size = new Slider { Minimum = 24, Maximum = 96, TickFrequency = 4, IsSnapToTickEnabled = true, Width = 200, Value = Config.IconSize };
        var sizeValue = new TextBlock { Text = Config.IconSize + " px", Width = 110, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        size.ValueChanged += (s, e) =>
        {
            int px = (int)size.Value;
            double ratio = Config.IconSize > 0 ? (double)Config.ZoomSize / Config.IconSize : 1;
            Config.IconSize = px;
            Config.ZoomSize = (int)Math.Round(px * Math.Max(1, ratio));
            sizeValue.Text = px + " px";
            UpdateZoomLabel();
            Apply();
        };
        sizeRow.Children.Add(size);
        sizeRow.Children.Add(sizeValue);
        box.Children.Add(sizeRow);

        zoomRow.Margin = new Thickness(0, 6, 0, 0);
        zoomRow.Children.Add(new TextBlock { Text = "Ampliación al pasar:", Width = 150, VerticalAlignment = VerticalAlignment.Center });
        zoomSlider.Width = 200;
        zoomSlider.Value = Math.Round((Config.ZoomSize / (double)Config.IconSize - 1) * 100 / 10) * 10;
        zoomValue.Margin = new Thickness(10, 0, 0, 0);
        zoomSlider.ValueChanged += (s, e) =>
        {
            Config.ZoomSize = (int)Math.Round(Config.IconSize * (1 + zoomSlider.Value / 100));
            UpdateZoomLabel();
            Apply();
        };
        zoomRow.Children.Add(zoomSlider);
        zoomRow.Children.Add(zoomValue);
        box.Children.Add(zoomRow);
        UpdateZoomLabel();

        backdropGroup.Margin = new Thickness(0, 8, 0, 0);
        backdropGroup.Children.Add(new TextBlock { Text = "Fondo del dock flotante:" });
        backdropGroup.Children.Add(Radio("bg", "Vidrio sutil", null, Config.Backdrop == Backdrop.Glass, () => { Config.Backdrop = Backdrop.Glass; Apply(); }));
        backdropGroup.Children.Add(Radio("bg", "Transparente", null, Config.Backdrop == Backdrop.Clear, () => { Config.Backdrop = Backdrop.Clear; Apply(); }));
        box.Children.Add(backdropGroup);
        return card;
    }

    private UIElement BuildBehavior()
    {
        var (box, card) = Section("Comportamiento");
        box.Children.Add(Check("Reservar su franja de pantalla (las ventanas maximizadas no lo tapan)", Config.ReserveSpace,
            v => { Config.ReserveSpace = v; Apply(); }));
        box.Children.Add(new TextBlock
        {
            Text = "Al reservar la franja, Windows reacomoda los iconos del escritorio que quedaban debajo y puede moverlos a otro monitor. Si eso molesta, desmarcar esta opción.",
            TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontSize = 11.5, Margin = new Thickness(22, 0, 0, 4),
        });
        box.Children.Add(Check("Ocultar automáticamente (aparece al llevar el puntero al borde)", Config.AutoHide,
            v => { Config.AutoHide = v; Apply(); }));
        box.Children.Add(Check("Mostrar también las apps abiertas que no están fijadas (desaparecen al cerrarse)", Config.ShowRunningApps,
            v => { Config.ShowRunningApps = v; Apply(); }));
        box.Children.Add(Check("Bloquear los iconos (no se pueden agregar, quitar ni reordenar arrastrando)", Config.LockItems,
            v => { Config.LockItems = v; manager.SaveConfig(); }));
        box.Children.Add(Check("Iniciar Muelle al encender Windows", Config.StartWithWindows, v =>
        {
            Config.StartWithWindows = v;
            DockConfig.SetStartup(v);
            manager.SaveConfig();
        }));
        return card;
    }

    private UIElement BuildApps()
    {
        var (box, card) = Section("Aplicaciones del dock");
        box.Children.Add(new TextBlock
        {
            Text = "También se pueden agregar arrastrando un acceso directo o un .exe sobre el dock, y reordenar o quitar arrastrando los iconos.",
            TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontSize = 12,
        });
        box.Children.Add(appList);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(Button("Agregar…", () => { manager.AddWithDialog(); RefreshApps(); }));
        buttons.Children.Add(Button("Quitar", () => WithSelected((it, i) => { manager.Items.RemoveAt(i); ItemsChanged(i - 1); })));
        buttons.Children.Add(Button("Subir", () => WithSelected((it, i) =>
        {
            if (i == 0) return;
            manager.Items.RemoveAt(i); manager.Items.Insert(i - 1, it); ItemsChanged(i - 1);
        })));
        buttons.Children.Add(Button("Bajar", () => WithSelected((it, i) =>
        {
            if (i >= manager.PinnedCount - 1) return;
            manager.Items.RemoveAt(i); manager.Items.Insert(i + 1, it); ItemsChanged(i + 1);
        })));
        box.Children.Add(buttons);
        return card;
    }

    private UIElement BuildFooter()
    {
        var row = new DockPanel { Margin = new Thickness(0, 10, 0, 0), LastChildFill = false };
        var folder = Button("Abrir carpeta de configuración", () =>
        {
            manager.SaveConfig();
            try { Process.Start("explorer.exe", "/select,\"" + DockConfig.FilePath + "\""); } catch { }
        });
        DockPanel.SetDock(folder, System.Windows.Controls.Dock.Left);
        var aboutBtn = Button("Acerca de…", manager.OpenAbout);
        DockPanel.SetDock(aboutBtn, System.Windows.Controls.Dock.Left);
        row.Children.Add(aboutBtn);
        var close = Button("Cerrar", Close);
        close.IsDefault = true;
        close.IsCancel = true;
        DockPanel.SetDock(close, System.Windows.Controls.Dock.Right);
        row.Children.Add(folder);
        row.Children.Add(close);
        return row;
    }

    // ---------------- Logic ----------------
    private void Apply()
    {
        RefreshEnabled();
        manager.Changed();
    }

    /// <summary>Dims the options that do not apply to the chosen style.</summary>
    private void RefreshEnabled()
    {
        bool dock = Config.Style == DockStyle.Dock;
        zoomRow.IsEnabled = dock;
        panelRow.IsEnabled = !dock;
        backdropGroup.IsEnabled = dock;
        monitorCombo.IsEnabled = !Config.AllMonitors;
    }

    private void UpdateZoomLabel()
    {
        zoomValue.Text = Config.ZoomSize <= Config.IconSize ? "Sin ampliación" : "hasta " + Config.ZoomSize + " px";
    }

    private void RefreshApps(int select = -1)
    {
        int keep = select >= 0 ? select : appList.SelectedIndex;
        appList.Items.Clear();
        foreach (var it in manager.Items.Where(i => !i.Transient)) appList.Items.Add(it.Name); // pinned ones only
        if (appList.Items.Count > 0) appList.SelectedIndex = Math.Clamp(keep, 0, appList.Items.Count - 1);
    }

    private void WithSelected(Action<DockItem, int> action)
    {
        int i = appList.SelectedIndex;
        if (i < 0 || i >= manager.PinnedCount) return;
        action(manager.Items[i], i);
    }

    private void ItemsChanged(int select)
    {
        manager.SaveItems();
        manager.KickAll();
        RefreshApps(Math.Max(0, select));
    }

    // ---------------- Controls ----------------
    /// <summary>Creates a titled card; returns the panel that holds the controls and the card itself.</summary>
    private static (StackPanel box, Border card) Section(string title)
    {
        var box = new StackPanel();
        var card = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE4, 0xE9)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 12),
            Margin = new Thickness(0, 6, 0, 6),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 14, Margin = new Thickness(0, 0, 0, 6) },
                    box,
                },
            },
        };
        return (box, card);
    }

    private static RadioButton Radio(string group, string text, string hint, bool isChecked, Action onChecked)
    {
        var content = new StackPanel();
        content.Children.Add(new TextBlock { Text = text });
        if (hint != null) content.Children.Add(new TextBlock { Text = hint, FontSize = 11.5, Foreground = Brushes.DimGray });
        var rb = new RadioButton { GroupName = group, Content = content, IsChecked = isChecked, Margin = new Thickness(0, 3, 0, 3) };
        rb.Checked += (s, e) => { if (rb.IsLoaded) onChecked(); };
        return rb;
    }

    private static CheckBox Check(string text, bool isChecked, Action<bool> onChange)
    {
        var cb = new CheckBox { Content = text, IsChecked = isChecked, Margin = new Thickness(0, 3, 0, 3) };
        cb.Click += (s, e) => onChange(cb.IsChecked == true);
        return cb;
    }

    private static Button Button(string text, Action onClick)
    {
        var b = new Button { Content = text, Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0), MinWidth = 70 };
        b.Click += (s, e) => onClick();
        return b;
    }
}
