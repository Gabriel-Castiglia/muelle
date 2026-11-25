//
//  SettingsWindow.cs
//  Muelle: a dock for Windows.
//
//  Copyright (C) 2026 Gabriel Castiglia. Licensed under the GNU GPL, version 2 only.
//  Date: November 6, 2026 (theme and appearance section: November 11, 2026)
//  SPDX-License-Identifier: GPL-2.0-only
//
//  This program is free software: you can redistribute it and/or modify it under the terms of
//  the GNU General Public License, version 2, as published by the Free Software Foundation.
//  It is distributed WITHOUT ANY WARRANTY. See the LICENSE file for details.
//

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    /// <summary>Which list the "Dock applications" section edits, when each screen has its own (index in <see cref="DockManager.EditableLists"/>).</summary>
    private int editing;
    private List<DockItem> CurrentList
    {
        get
        {
            var lists = manager.EditableLists();
            return lists[Math.Clamp(editing, 0, lists.Count - 1)].Items;
        }
    }

    public SettingsWindow(DockManager manager)
    {
        this.manager = manager;
        // Moderate, resizable size: two columns to keep it short; scrolls if it does not fit.
        Width = 960;
        Height = Math.Min(700, SystemParameters.WorkArea.Height - 40);
        MinWidth = 800;
        MinHeight = 380;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontSize = 13;
        Theme.Attach(this);

        Rebuild();
        Activated += (s, e) => RefreshApps();
        Loc.Changed += Rebuild;
        Closed += (s, e) => Loc.Changed -= Rebuild;
    }

    /// <summary>Builds (or rebuilds) every control from the current configuration and language.</summary>
    public void Rebuild()
    {
        double offset = scroller?.VerticalOffset ?? 0;
        Title = tr("Configuración de Muelle", "Muelle Settings", "Muelle の設定");

        var left = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
        left.Children.Add(BuildStyle());
        left.Children.Add(BuildPosition());
        left.Children.Add(BuildBehavior());
        var right = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
        right.Children.Add(BuildAppearance());
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
        var layout = new DockPanel { Margin = new Thickness(18, 8, 10, 14) };
        layout.Children.Add(footer);
        columns.Margin = new Thickness(0, 0, 8, 0);
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
        var (box, card) = Section(tr("Estilo", "Style", "スタイル"));
        box.Children.Add(Radio("style", tr("Dock flotante (estilo Mac)", "Floating dock (Mac style)", "フローティングドック (Mac 風)"),
            tr("Iconos centrados que se amplían al pasar el puntero.", "Centered icons that grow under the pointer.", "中央に並んだアイコンがポインターの下で拡大します。"),
            Config.Style == DockStyle.Dock, () => { Config.Style = DockStyle.Dock; Apply(); }));
        box.Children.Add(Radio("style", tr("Panel fijo (estilo Ubuntu)", "Fixed panel (Ubuntu style)", "固定パネル (Ubuntu 風)"),
            tr("Barra continua de borde a borde, con los iconos alineados al inicio.",
               "Continuous edge-to-edge bar, with the icons aligned to the start.", "端から端まで続くバーで、アイコンは先頭に揃えて並びます。"),
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
        panelRow.Children.Add(Row(tr("Opacidad del panel:", "Panel opacity:", "パネルの不透明度:"), opacity, opacityValue));
        box.Children.Add(panelRow);
        return card;
    }

    private UIElement BuildPosition()
    {
        var (box, card) = Section(tr("Ubicación", "Location", "位置"));

        var side = new ComboBox();
        var sides = new[]
        {
            (tr("Izquierda", "Left", "左"), DockEdge.Left), (tr("Derecha", "Right", "右"), DockEdge.Right),
            (tr("Arriba", "Top", "上"), DockEdge.Top), (tr("Abajo", "Bottom", "下"), DockEdge.Bottom),
        };
        foreach (var (name, _) in sides) side.Items.Add(name);
        side.SelectedIndex = Array.FindIndex(sides, x => x.Item2 == Config.Edge);
        side.SelectionChanged += (s, e) => { if (side.SelectedIndex >= 0) { Config.Edge = sides[side.SelectedIndex].Item2; Apply(); } };
        var sideRow = Row(tr("Borde de la pantalla:", "Screen edge:", "画面の端:"), side, null);
        sideRow.Margin = new Thickness(0, 2, 0, 8);
        box.Children.Add(sideRow);

        box.Children.Add(new TextBlock { Text = tr("Mostrar el dock en:", "Show the dock on:", "ドックを表示する画面:"), Margin = new Thickness(0, 0, 0, 2) });
        var monitors = Monitors.Listed();
        bool specific = !Config.AllMonitors && !string.IsNullOrEmpty(Config.Monitor);
        monitorCombo = new ComboBox { Margin = new Thickness(22, 4, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
        box.Children.Add(Radio("mon", tr("Solo en la pantalla principal", "The primary screen only", "メイン画面のみ"), null, !Config.AllMonitors && !specific,
            () => { Config.AllMonitors = false; Config.Monitor = ""; Apply(); }));
        box.Children.Add(Radio("mon", tr("En todas las pantallas (una copia en cada una)", "Every screen (one copy on each)", "すべての画面 (各画面に 1 つずつ)"), null, Config.AllMonitors,
            () => { Config.AllMonitors = true; Apply(); }));
        box.Children.Add(Radio("mon", tr("En una pantalla concreta:", "A specific screen:", "特定の画面:"), null, specific, () =>
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
            monitorCombo.Items.Add($"{tr("Pantalla", "Screen", "画面")} {i + 1}  ·  {m.Bounds.Width}×{m.Bounds.Height}  ·  {Math.Round(m.Scale * 100)} %"
                                   + (m.Primary ? tr("  (principal)", "  (primary)", " (メイン)") : ""));
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
        box.Children.Add(Check(tr("Cada pantalla con sus propios iconos", "Each screen has its own icons", "画面ごとに別のアイコン"), Config.PerMonitor, v =>
        {
            Config.PerMonitor = v;
            editing = 0;
            Apply();
            Dispatcher.BeginInvoke(new Action(Rebuild));
        }));
        box.Children.Add(Hint(tr("Una pantalla nueva empieza con una copia de los iconos comunes. Si se desconecta, sus iconos se guardan y vuelven con ella.",
                                 "A new screen starts with a copy of the common icons. If it is disconnected, its icons are kept and come back with it.", "新しい画面は共通アイコンのコピーで始まります。画面を外してもアイコンは保持され、再接続すると戻ります。"),
                              new Thickness(46, 0, 0, 0)));
        return card;
    }

    private UIElement BuildSize()
    {
        var (box, card) = Section(tr("Iconos", "Icons", "アイコン"));

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
        box.Children.Add(Row(tr("Tamaño:", "Size:", "サイズ:"), size, sizeValue));

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
        zoomRow.Children.Add(Row(tr("Ampliación al pasar:", "Magnification:", "拡大:"), zoomSlider, zoomValue));
        box.Children.Add(zoomRow);
        UpdateZoomLabel();

        backdropGroup = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        backdropGroup.Children.Add(new TextBlock { Text = tr("Fondo del dock flotante:", "Floating dock background:", "フローティングドックの背景:") });
        backdropGroup.Children.Add(Radio("bg", tr("Vidrio sutil", "Subtle glass", "控えめなガラス"), null, Config.Backdrop == Backdrop.Glass,
            () => { Config.Backdrop = Backdrop.Glass; Apply(); }));
        backdropGroup.Children.Add(Radio("bg", tr("Transparente", "Transparent", "透明"), null, Config.Backdrop == Backdrop.Clear,
            () => { Config.Backdrop = Backdrop.Clear; Apply(); }));
        box.Children.Add(backdropGroup);
        return card;
    }

    private UIElement BuildBehavior()
    {
        var (box, card) = Section(tr("Comportamiento", "Behavior", "動作"));
        box.Children.Add(Check(tr("Reservar su franja de pantalla (las ventanas maximizadas no lo tapan)",
                                  "Reserve its screen strip (maximized windows do not cover it)", "画面の帯を確保する (最大化したウィンドウで隠れません)"), Config.ReserveSpace,
            v => { Config.ReserveSpace = v; Apply(); }));
        box.Children.Add(Hint(tr("Al reservar la franja, Windows reacomoda los iconos del escritorio que quedaban debajo y puede moverlos a otro monitor. Si eso molesta, desmarcar esta opción.",
                                 "When the strip is reserved, Windows rearranges the desktop icons that were underneath and may move them to another monitor. If that is a problem, turn this option off.", "帯を確保すると、Windows はその下にあったデスクトップアイコンを並べ替え、別のモニターへ移すことがあります。問題がある場合はこのオプションをオフにしてください。"),
                              new Thickness(46, 0, 0, 6)));
        box.Children.Add(Check(tr("Ocultar automáticamente (aparece al llevar el puntero al borde)",
                                  "Hide automatically (shows up when the pointer reaches the edge)", "自動的に隠す (ポインターが端に来ると表示)"), Config.AutoHide,
            v => { Config.AutoHide = v; Apply(); }));
        box.Children.Add(Check(tr("Mostrar también las apps abiertas que no están fijadas (desaparecen al cerrarse)",
                                  "Also show open apps that are not pinned (they leave when closed)", "固定していない起動中のアプリも表示する (閉じると消えます)"), Config.ShowRunningApps,
            v => { Config.ShowRunningApps = v; Apply(); manager.RefreshRunning(); }));
        box.Children.Add(Check(tr("Bloquear los iconos (no se pueden agregar, quitar ni reordenar arrastrando)",
                                  "Lock the icons (they cannot be added, removed or reordered by dragging)", "アイコンをロックする (ドラッグによる追加・削除・並べ替えができなくなります)"), Config.LockItems,
            v => manager.SetLocked(v)));
        box.Children.Add(Check(tr("Iniciar Muelle al encender Windows", "Start Muelle when Windows starts", "Windows の起動時に Muelle を開始する"), Config.StartWithWindows, v =>
        {
            Config.StartWithWindows = v;
            DockConfig.SetStartup(v);
            manager.SaveConfig();
        }));
        box.Children.Add(Check(tr("Buscar versiones nuevas una vez al día (se consulta GitHub; se instalan solo si se acepta)",
                                  "Look for new versions once a day (GitHub is asked; they install only if accepted)", "1 日 1 回新しいバージョンを確認する (GitHub に問い合わせ、承認した場合のみインストール)"), Config.CheckUpdates, v =>
        {
            Config.CheckUpdates = v;
            manager.SaveConfig();
        }));
        return card;
    }

    private UIElement BuildAppearance()
    {
        var (box, card) = Section(tr("Apariencia", "Appearance", "外観"));
        var segHost = new Border { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 12) };
        segHost.SetResourceReference(StyleProperty, "Mu.SegmentHost");
        var segs = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (key, label) in new[]
        {
            (Theme.Auto, tr("Como Windows", "As Windows", "Windows に合わせる")), (Theme.Light, tr("Claro", "Light", "ライト")), (Theme.Dark, tr("Oscuro", "Dark", "ダーク")),
        })
        {
            var rb = new RadioButton { Content = label, GroupName = "appearance", IsChecked = Config.Appearance == key };
            rb.SetResourceReference(StyleProperty, "Mu.Segment");
            rb.Checked += (s, e) => { if (rb.IsLoaded) manager.SetTheme(key, Config.Accent); };
            segs.Children.Add(rb);
        }
        segHost.Child = segs;
        box.Children.Add(segHost);

        box.Children.Add(new TextBlock { Text = tr("Color:", "Colour:", "色:"), Margin = new Thickness(0, 0, 0, 4) });
        var tiles = new UniformGrid { Columns = 6, Margin = new Thickness(-3, 0, -3, 4) };
        var marks = new List<(string Key, Border Tile)>();
        void Mark()
        {
            foreach (var (k, t) in marks)
            {
                bool on = k == Config.Accent;
                if (on) { t.SetResourceReference(Border.BorderBrushProperty, "Mu.Accent"); t.SetResourceReference(Border.BackgroundProperty, "Mu.AccentSoft"); }
                else { t.BorderBrush = Brushes.Transparent; t.Background = Brushes.Transparent; }
            }
        }
        foreach (var a in Theme.Accents)
        {
            var img = new Image { Width = 40, Height = 40, Source = Theme.Icon(a.Key, 64), Margin = new Thickness(0, 2, 0, 3) };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            // Room for two lines, so that a long name ("Verde azulado") wraps instead of being cut.
            var name = new TextBlock { Text = a.Name, FontSize = 11, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Height = 30 };
            var tile = new Border
            {
                Padding = new Thickness(2, 4, 2, 4), Margin = new Thickness(3), CornerRadius = new CornerRadius(9),
                BorderThickness = new Thickness(2), Cursor = System.Windows.Input.Cursors.Hand, ToolTip = a.Name,
                Child = new StackPanel { Children = { img, name } },
            };
            var key = a.Key;
            tile.MouseLeftButtonUp += (s, e) => { manager.SetTheme(Config.Appearance, key); Mark(); };
            marks.Add((key, tile));
            tiles.Children.Add(tile);
        }
        Mark();
        box.Children.Add(tiles);
        box.Children.Add(Hint(tr("El color se usa en el ícono de las ventanas de Muelle, en el punto de las apps abiertas y en los controles.",
                                 "The colour is used for the icon of Muelle's windows, the dot of open apps and the controls.", "この色は Muelle のウィンドウのアイコン、起動中のアプリの点、コントロールに使われます。"),
                              new Thickness(0, 2, 0, 0)));
        return card;
    }

    private UIElement BuildLanguage()
    {
        // The title carries every language, so the section can be found from any of them.
        var (box, card) = Section("Language / Idioma / 言語");
        var combo = new ComboBox();
        foreach (var (_, name) in Loc.All) combo.Items.Add(name);
        combo.SelectedIndex = Array.FindIndex(Loc.All, l => l.Lang == Loc.Current);
        combo.SelectionChanged += (s, e) =>
        {
            if (combo.SelectedIndex < 0) return;
            var chosen = Loc.All[combo.SelectedIndex].Lang;
            // Rebuilding happens through Loc.Changed, after this handler returns.
            if (chosen != Loc.Current) Dispatcher.BeginInvoke(new Action(() => manager.SetLanguage(chosen)));
        };
        box.Children.Add(Row(tr("Idioma de la interfaz:", "Interface language:", "表示言語:"), combo, null));
        return card;
    }

    private UIElement BuildApps()
    {
        var (box, card) = Section(tr("Aplicaciones del dock", "Dock applications", "ドックのアプリケーション"));
        box.Children.Add(Hint(tr("También se pueden agregar arrastrando un acceso directo o un .exe sobre el dock, y reordenar o quitar arrastrando los iconos.",
                                 "Apps can also be added by dropping a shortcut or an .exe on the dock, and reordered or removed by dragging the icons.", "ショートカットや .exe をドックにドロップしてアプリを追加することもでき、アイコンをドラッグして並べ替えや削除ができます。"),
                              new Thickness(0)));
        // With its own icons per screen: which screen's icons are listed.
        var lists = manager.EditableLists();
        if (Config.PerMonitor && lists.Count > 0 && lists[0].Monitor != null)
        {
            var screen = new ComboBox();
            for (int i = 0; i < lists.Count; i++)
            {
                var m = lists[i].Monitor;
                screen.Items.Add($"{tr("Pantalla", "Screen", "画面")} {i + 1}  ·  {m.Bounds.Width}×{m.Bounds.Height}" + (m.Primary ? tr("  (principal)", "  (primary)", " (メイン)") : ""));
            }
            editing = Math.Clamp(editing, 0, lists.Count - 1);
            screen.SelectedIndex = editing;
            screen.SelectionChanged += (s, e) => { if (screen.SelectedIndex >= 0) { editing = screen.SelectedIndex; RefreshApps(0); } };
            var screenRow = Row(tr("Iconos de:", "Icons of:", "アイコンの対象:"), screen, null);
            screenRow.Margin = new Thickness(0, 8, 0, 0);
            box.Children.Add(screenRow);
        }
        appList = new ListBox { Height = 150, Margin = new Thickness(0, 6, 0, 8) };
        appList.MouseDoubleClick += (s, e) => WithSelected((it, i) => { if (it.IsStack) StackWindow.Edit(manager, CurrentList, it, i); });
        box.Children.Add(appList);

        var buttons = new WrapPanel();
        buttons.Children.Add(Button(tr("Agregar…", "Add…", "追加…"), () => { manager.AddWithDialog(CurrentList); RefreshApps(); }));
        buttons.Children.Add(Button(tr("Nueva pila…", "New stack…", "新しいスタック…"), () => StackWindow.Edit(manager, CurrentList, null, DockManager.PinnedCountOf(CurrentList))));
        buttons.Children.Add(Button(tr("Editar pila…", "Edit stack…", "スタックを編集…"), () => WithSelected((it, i) => { if (it.IsStack) StackWindow.Edit(manager, CurrentList, it, i); })));
        buttons.Children.Add(Button(tr("Quitar", "Remove", "削除"), () => WithSelected((it, i) => { CurrentList.RemoveAt(i); ItemsChanged(i - 1); })));
        buttons.Children.Add(Button(tr("Subir", "Move up", "上へ"), () => WithSelected((it, i) =>
        {
            if (i == 0) return;
            var l = CurrentList;
            l.RemoveAt(i); l.Insert(i - 1, it); ItemsChanged(i - 1);
        })));
        buttons.Children.Add(Button(tr("Bajar", "Move down", "下へ"), () => WithSelected((it, i) =>
        {
            var l = CurrentList;
            if (i >= DockManager.PinnedCountOf(l) - 1) return;
            l.RemoveAt(i); l.Insert(i + 1, it); ItemsChanged(i + 1);
        })));
        box.Children.Add(buttons);
        return card;
    }

    private UIElement BuildFooter()
    {
        var row = new DockPanel { Margin = new Thickness(0, 10, 8, 0), LastChildFill = false };
        var folder = Button(tr("Abrir carpeta de configuración", "Open settings folder", "設定フォルダーを開く"), () =>
        {
            manager.FlushConfig();
            try { Process.Start("explorer.exe", "/select,\"" + DockConfig.FilePath + "\"")?.Dispose(); } catch { }
        });
        var aboutBtn = Button(tr("Acerca de…", "About…", "バージョン情報…"), manager.OpenAbout);
        var close = Button(tr("Cerrar", "Close", "閉じる"), Close);
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
            ? tr("Sin ampliación", "None", "なし")
            : tr("hasta ", "up to ", "最大 ") + Config.ZoomSize + " px";
    }

    private void RefreshApps(int select = -1)
    {
        int keep = select >= 0 ? select : appList.SelectedIndex;
        appList.Items.Clear();
        foreach (var it in CurrentList.Where(i => !i.Transient)) // pinned ones only
            appList.Items.Add(it.IsStack ? $"{it.Name}   ·   {tr("pila", "stack", "スタック")} ({it.Children.Count})" : it.Name);
        if (appList.Items.Count > 0) appList.SelectedIndex = Math.Clamp(keep, 0, appList.Items.Count - 1);
    }

    private void WithSelected(Action<DockItem, int> action)
    {
        int i = appList.SelectedIndex;
        var list = CurrentList;
        if (i < 0 || i >= DockManager.PinnedCountOf(list)) return;
        action(list[i], i);
    }

    private void ItemsChanged(int select)
    {
        manager.SaveItems(CurrentList);
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
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 14.5, Margin = new Thickness(0, 0, 0, 8) },
                    box,
                },
            },
        };
        card.SetResourceReference(StyleProperty, "Mu.CardFrame");
        return (box, card);
    }

    /// <summary>A short explanation in the muted colour of the theme.</summary>
    private static TextBlock Hint(string text, Thickness margin)
    {
        var t = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 11.5, Margin = margin };
        t.SetResourceReference(TextBlock.ForegroundProperty, "Mu.TextMuted");
        return t;
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
        if (hint != null) content.Children.Add(Hint(hint, new Thickness(0, 1, 0, 0)));
        var rb = new RadioButton { GroupName = group, Content = content, IsChecked = isChecked, Margin = new Thickness(0, 4, 0, 4) };
        rb.Checked += (s, e) => { if (rb.IsLoaded) onChecked(); };
        return rb;
    }

    private static CheckBox Check(string text, bool isChecked, Action<bool> onChange)
    {
        var cb = new CheckBox
        {
            Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
            IsChecked = isChecked, Margin = new Thickness(0, 5, 0, 5),
        };
        cb.Click += (s, e) => onChange(cb.IsChecked == true);
        return cb;
    }

    private static Button Button(string text, Action onClick)
    {
        var b = new Button { Content = text, Margin = new Thickness(0, 0, 8, 6) };
        b.Click += (s, e) => onClick();
        return b;
    }
}
