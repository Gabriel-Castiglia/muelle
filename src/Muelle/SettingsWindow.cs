//
//  SettingsWindow.cs
//  Muelle: a dock for Windows.
//
//  Copyright (C) 2026 Gabriel Castiglia. Licensed under the GNU GPL, version 3 or later.
//  Date: November 6, 2026
//  SPDX-License-Identifier: GPL-3.0-or-later
//
//  This program is free software: you can redistribute it and/or modify it under the terms of
//  the GNU General Public License as published by the Free Software Foundation, either version 3
//  of the License, or (at your option) any later version.
//  It is distributed WITHOUT ANY WARRANTY. See the LICENSE file for details.
//

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static Muelle.Loc;

namespace Muelle;

/// <summary>
/// Settings window: gathers all options on a single screen, grouped by topic.
/// Every change is applied immediately; no "OK" button is needed. The whole content is rebuilt
/// when the interface language changes or an option is changed from the dock menu.
/// </summary>
internal sealed class SettingsWindow : Window
{
    private readonly DockManager manager;
    private DockConfig Config => manager.Config;

    // Controls that other options enable, disable or refresh; created again on every rebuild.
    private ListBox appList;
    private Slider zoomSlider;
    private TextBlock zoomValue;
    private ComboBox monitorCombo;
    private StackPanel backdropGroup, zoomRow, panelRow;
    private ScrollViewer scroller;

    public SettingsWindow(DockManager manager)
    {
        this.manager = manager;
        // Moderate, resizable size: two columns to keep it short; scrolls if it does not fit.
        Width = 900;
        Height = Math.Min(600, SystemParameters.WorkArea.Height - 40);
        MinWidth = 760;
        MinHeight = 360;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        FontSize = 13;
        Background = new SolidColorBrush(Color.FromRgb(0xF7, 0xF7, 0xF9));

        Rebuild();
        Activated += (s, e) => RefreshApps();
        Loc.Changed += Rebuild;
        Closed += (s, e) => Loc.Changed -= Rebuild;
    }

    /// <summary>Builds (or rebuilds) every control from the current configuration and language.</summary>
    public void Rebuild()
    {
        double offset = scroller?.VerticalOffset ?? 0;
        Title = tr("Configuración de Muelle", "Muelle Settings");

        var left = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
        left.Children.Add(BuildStyle());
        left.Children.Add(BuildPosition());
        left.Children.Add(BuildBehavior());
        var right = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
        right.Children.Add(BuildSize());
        right.Children.Add(BuildLanguage());
        right.Children.Add(BuildApps());

        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition());
        columns.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(right, 1);
        columns.Children.Add(left);
        columns.Children.Add(right);

        var footer = BuildFooter();
        DockPanel.SetDock(footer, Dock.Bottom);
        var layout = new DockPanel { Margin = new Thickness(16, 8, 16, 12) };
        layout.Children.Add(footer);
        scroller = new ScrollViewer { Content = columns, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        layout.Children.Add(scroller);
        Content = layout;
        scroller.ScrollToVerticalOffset(offset);

        RefreshEnabled();
        RefreshApps();
    }

    // ---------------- Sections ----------------
    private UIElement BuildStyle()
    {
        var (box, card) = Section(tr("Estilo", "Style"));
        box.Children.Add(Radio("style", tr("Dock flotante (estilo Mac)", "Floating dock (Mac style)"),
            tr("Iconos centrados que se amplían al pasar el puntero.", "Centered icons that grow under the pointer."),
            Config.Style == DockStyle.Dock, () => { Config.Style = DockStyle.Dock; Apply(); }));
        box.Children.Add(Radio("style", tr("Panel fijo (estilo Ubuntu)", "Fixed panel (Ubuntu style)"),
            tr("Barra continua de borde a borde, con los iconos alineados al inicio.",
               "Continuous edge-to-edge bar, with the icons aligned to the start."),
            Config.Style == DockStyle.Panel, () => { Config.Style = DockStyle.Panel; Apply(); }));

        var opacity = new Slider { Minimum = 0, Maximum = 100, TickFrequency = 5, IsSnapToTickEnabled = true, Value = Config.PanelOpacity };
        var opacityValue = Value(Config.PanelOpacity + " %");
        opacity.ValueChanged += (s, e) =>
        {
            Config.PanelOpacity = (int)opacity.Value;
            opacityValue.Text = Config.PanelOpacity + " %";
            manager.SaveConfig();
            manager.KickAll();
        };
        panelRow = new StackPanel { Margin = new Thickness(22, 4, 0, 0) };
        panelRow.Children.Add(Row(tr("Opacidad del panel:", "Panel opacity:"), opacity, opacityValue));
        box.Children.Add(panelRow);
        return card;
    }

    private UIElement BuildPosition()
    {
        var (box, card) = Section(tr("Ubicación", "Location"));

        var side = new ComboBox();
        var sides = new[]
        {
            (tr("Izquierda", "Left"), DockEdge.Left), (tr("Derecha", "Right"), DockEdge.Right),
            (tr("Arriba", "Top"), DockEdge.Top), (tr("Abajo", "Bottom"), DockEdge.Bottom),
        };
        foreach (var (name, _) in sides) side.Items.Add(name);
        side.SelectedIndex = Array.FindIndex(sides, x => x.Item2 == Config.Edge);
        side.SelectionChanged += (s, e) => { if (side.SelectedIndex >= 0) { Config.Edge = sides[side.SelectedIndex].Item2; Apply(); } };
        var sideRow = Row(tr("Borde de la pantalla:", "Screen edge:"), side, null);
        sideRow.Margin = new Thickness(0, 2, 0, 8);
        box.Children.Add(sideRow);

        box.Children.Add(new TextBlock { Text = tr("Mostrar el dock en:", "Show the dock on:"), Margin = new Thickness(0, 0, 0, 2) });
        var monitors = Monitors.All();
        bool specific = !Config.AllMonitors && !string.IsNullOrEmpty(Config.Monitor);
        monitorCombo = new ComboBox { Margin = new Thickness(22, 4, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
        box.Children.Add(Radio("mon", tr("Solo en la pantalla principal", "The primary screen only"), null, !Config.AllMonitors && !specific,
            () => { Config.AllMonitors = false; Config.Monitor = ""; Apply(); }));
        box.Children.Add(Radio("mon", tr("En todas las pantallas (una copia en cada una)", "Every screen (one copy on each)"), null, Config.AllMonitors,
            () => { Config.AllMonitors = true; Apply(); }));
        box.Children.Add(Radio("mon", tr("En una pantalla concreta:", "A specific screen:"), null, specific, () =>
        {
            Config.AllMonitors = false;
            if (monitors.Count == 0) return;
            if (monitorCombo.SelectedIndex < 0) monitorCombo.SelectedIndex = 0;
            Config.Monitor = monitors[monitorCombo.SelectedIndex].Device;
            Apply();
        }));
        for (int i = 0; i < monitors.Count; i++)
        {
            var m = monitors[i];
            monitorCombo.Items.Add($"{tr("Pantalla", "Screen")} {i + 1}  ·  {m.Bounds.Width}×{m.Bounds.Height}  ·  {Math.Round(m.Scale * 100)} %"
                                   + (m.Primary ? tr("  (principal)", "  (primary)") : ""));
            if (string.Equals(m.Device, Config.Monitor, StringComparison.OrdinalIgnoreCase)) monitorCombo.SelectedIndex = i;
        }
        // Without a specific screen chosen, the list shows the primary one (only a display: the handler below
        // changes nothing unless "A specific screen" is selected).
        if (monitorCombo.SelectedIndex < 0 && monitors.Count > 0)
            monitorCombo.SelectedIndex = Math.Max(0, monitors.FindIndex(m => m.Primary));
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
        var (box, card) = Section(tr("Iconos", "Icons"));

        var size = new Slider
        {
            Minimum = DockConfig.MinIconSize, Maximum = DockConfig.MaxIconSize, TickFrequency = 4,
            IsSnapToTickEnabled = true, Value = Config.IconSize,
        };
        var sizeValue = Value(Config.IconSize + " px");
        size.ValueChanged += (s, e) =>
        {
            int px = (int)size.Value;
            double ratio = Config.IconSize > 0 ? (double)Config.ZoomSize / Config.IconSize : 1;
            Config.IconSize = px;
            Config.ZoomSize = (int)Math.Round(px * Math.Clamp(ratio, 1, 2));
            sizeValue.Text = px + " px";
            UpdateZoomLabel();
            Apply();
        };
        box.Children.Add(Row(tr("Tamaño:", "Size:"), size, sizeValue));

        zoomSlider = new Slider { Minimum = 0, Maximum = 100, TickFrequency = 10, IsSnapToTickEnabled = true };
        zoomSlider.Value = Math.Round((Config.ZoomSize / (double)Config.IconSize - 1) * 100 / 10) * 10;
        zoomValue = Value("");
        zoomSlider.ValueChanged += (s, e) =>
        {
            Config.ZoomSize = (int)Math.Round(Config.IconSize * (1 + zoomSlider.Value / 100));
            UpdateZoomLabel();
            Apply();
        };
        zoomRow = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        zoomRow.Children.Add(Row(tr("Ampliación al pasar:", "Magnification:"), zoomSlider, zoomValue));
        box.Children.Add(zoomRow);
        UpdateZoomLabel();

        backdropGroup = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        backdropGroup.Children.Add(new TextBlock { Text = tr("Fondo del dock flotante:", "Floating dock background:") });
        backdropGroup.Children.Add(Radio("bg", tr("Vidrio sutil", "Subtle glass"), null, Config.Backdrop == Backdrop.Glass,
            () => { Config.Backdrop = Backdrop.Glass; Apply(); }));
        backdropGroup.Children.Add(Radio("bg", tr("Transparente", "Transparent"), null, Config.Backdrop == Backdrop.Clear,
            () => { Config.Backdrop = Backdrop.Clear; Apply(); }));
        box.Children.Add(backdropGroup);
        return card;
    }

    private UIElement BuildBehavior()
    {
        var (box, card) = Section(tr("Comportamiento", "Behavior"));
        box.Children.Add(Check(tr("Reservar su franja de pantalla (las ventanas maximizadas no lo tapan)",
                                  "Reserve its screen strip (maximized windows do not cover it)"), Config.ReserveSpace,
            v => { Config.ReserveSpace = v; Apply(); }));
        box.Children.Add(new TextBlock
        {
            Text = tr("Al reservar la franja, Windows reacomoda los iconos del escritorio que quedaban debajo y puede moverlos a otro monitor. Si eso molesta, desmarcar esta opción.",
                      "When the strip is reserved, Windows rearranges the desktop icons that were underneath and may move them to another monitor. If that is a problem, turn this option off."),
            TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontSize = 11.5, Margin = new Thickness(22, 0, 0, 4),
        });
        box.Children.Add(Check(tr("Ocultar automáticamente (aparece al llevar el puntero al borde)",
                                  "Hide automatically (shows up when the pointer reaches the edge)"), Config.AutoHide,
            v => { Config.AutoHide = v; Apply(); }));
        box.Children.Add(Check(tr("Mostrar también las apps abiertas que no están fijadas (desaparecen al cerrarse)",
                                  "Also show open apps that are not pinned (they leave when closed)"), Config.ShowRunningApps,
            v => { Config.ShowRunningApps = v; Apply(); manager.RefreshRunning(); }));
        box.Children.Add(Check(tr("Bloquear los iconos (no se pueden agregar, quitar ni reordenar arrastrando)",
                                  "Lock the icons (they cannot be added, removed or reordered by dragging)"), Config.LockItems,
            v => manager.SetLocked(v)));
        box.Children.Add(Check(tr("Iniciar Muelle al encender Windows", "Start Muelle when Windows starts"), Config.StartWithWindows, v =>
        {
            Config.StartWithWindows = v;
            DockConfig.SetStartup(v);
            manager.SaveConfig();
        }));
        return card;
    }

    private UIElement BuildLanguage()
    {
        // The title carries both languages, so the section can be found from either one.
        var (box, card) = Section("Idioma / Language");
        var combo = new ComboBox();
        combo.Items.Add("English");
        combo.Items.Add("Español");
        combo.SelectedIndex = Loc.Current == UiLanguage.Es ? 1 : 0;
        combo.SelectionChanged += (s, e) =>
        {
            var chosen = combo.SelectedIndex == 1 ? UiLanguage.Es : UiLanguage.En;
            // Rebuilding happens through Loc.Changed, after this handler returns.
            if (chosen != Loc.Current) Dispatcher.BeginInvoke(new Action(() => manager.SetLanguage(chosen)));
        };
        box.Children.Add(Row(tr("Idioma de la interfaz:", "Interface language:"), combo, null));
        return card;
    }

    private UIElement BuildApps()
    {
        var (box, card) = Section(tr("Aplicaciones del dock", "Dock applications"));
        box.Children.Add(new TextBlock
        {
            Text = tr("También se pueden agregar arrastrando un acceso directo o un .exe sobre el dock, y reordenar o quitar arrastrando los iconos.",
                      "Apps can also be added by dropping a shortcut or an .exe on the dock, and reordered or removed by dragging the icons."),
            TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontSize = 12,
        });
        appList = new ListBox { Height = 120, Margin = new Thickness(0, 4, 0, 6) };
        box.Children.Add(appList);

        var buttons = new WrapPanel();
        buttons.Children.Add(Button(tr("Agregar…", "Add…"), () => { manager.AddWithDialog(); RefreshApps(); }));
        buttons.Children.Add(Button(tr("Quitar", "Remove"), () => WithSelected((it, i) => { manager.Items.RemoveAt(i); ItemsChanged(i - 1); })));
        buttons.Children.Add(Button(tr("Subir", "Move up"), () => WithSelected((it, i) =>
        {
            if (i == 0) return;
            manager.Items.RemoveAt(i); manager.Items.Insert(i - 1, it); ItemsChanged(i - 1);
        })));
        buttons.Children.Add(Button(tr("Bajar", "Move down"), () => WithSelected((it, i) =>
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
        var folder = Button(tr("Abrir carpeta de configuración", "Open settings folder"), () =>
        {
            manager.FlushConfig();
            try { Process.Start("explorer.exe", "/select,\"" + DockConfig.FilePath + "\""); } catch { }
        });
        var aboutBtn = Button(tr("Acerca de…", "About…"), manager.OpenAbout);
        var close = Button(tr("Cerrar", "Close"), Close);
        close.IsDefault = true;
        close.IsCancel = true;
        close.Margin = new Thickness(0);
        DockPanel.SetDock(folder, Dock.Left);
        DockPanel.SetDock(aboutBtn, Dock.Left);
        DockPanel.SetDock(close, Dock.Right);
        row.Children.Add(folder);
        row.Children.Add(aboutBtn);
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
        zoomValue.Text = Config.ZoomSize <= Config.IconSize
            ? tr("Sin ampliación", "None")
            : tr("hasta ", "up to ") + Config.ZoomSize + " px";
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

    /// <summary>
    /// A label, a control that takes the remaining width and an optional value on the right. The label
    /// column fits the longest label of either language, so the row adapts to the window width.
    /// </summary>
    private static Grid Row(string label, FrameworkElement control, FrameworkElement value)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = value == null ? new GridLength(0) : new GridLength(105) });
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 8, 0) };
        g.Children.Add(text);
        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        g.Children.Add(control);
        if (value != null)
        {
            value.Margin = new Thickness(10, 0, 0, 0);
            Grid.SetColumn(value, 2);
            g.Children.Add(value);
        }
        return g;
    }

    private static TextBlock Value(string text) => new() { Text = text, VerticalAlignment = VerticalAlignment.Center };

    private static RadioButton Radio(string group, string text, string hint, bool isChecked, Action onChecked)
    {
        var content = new StackPanel();
        content.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });
        if (hint != null) content.Children.Add(new TextBlock { Text = hint, FontSize = 11.5, Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap });
        var rb = new RadioButton { GroupName = group, Content = content, IsChecked = isChecked, Margin = new Thickness(0, 3, 0, 3) };
        rb.Checked += (s, e) => { if (rb.IsLoaded) onChecked(); };
        return rb;
    }

    private static CheckBox Check(string text, bool isChecked, Action<bool> onChange)
    {
        var cb = new CheckBox
        {
            Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
            IsChecked = isChecked, Margin = new Thickness(0, 3, 0, 3),
        };
        cb.Click += (s, e) => onChange(cb.IsChecked == true);
        return cb;
    }

    private static Button Button(string text, Action onClick)
    {
        var b = new Button { Content = text, Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 6), MinWidth = 70 };
        b.Click += (s, e) => onClick();
        return b;
    }
}
