//
//  SetupWindow.cs
//  Muelle setup: the setup window, in English and Spanish.
//
//  Copyright (C) 2026 Gabriel Castiglia. Licensed under the GNU GPL, version 2 only.
//  Date: November 8, 2026
//  SPDX-License-Identifier: GPL-2.0-only
//
//  This program is free software: you can redistribute it and/or modify it under the terms of
//  the GNU General Public License, version 2, as published by the Free Software Foundation.
//  It is distributed WITHOUT ANY WARRANTY. See the LICENSE file for details.
//

using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WixToolset.BootstrapperApplicationApi;
using static Muelle.Loc;

namespace Muelle.Setup;

/// <summary>
/// The setup window: a header with the icon, the version and the language menu, one page at a time
/// (start, progress, result) and the buttons of that page. A language change redraws the current page.
/// The window also works without an engine (app == null) to render the pages for the manuals.
/// </summary>
internal sealed class SetupWindow : Window
{
    private const string LicenseUrl = "https://www.gnu.org/licenses/old-licenses/gpl-2.0.html";

    private readonly SetupApplication app;
    private readonly ContentControl page = new();
    private readonly StackPanel buttons = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly ProgressBar bar = new() { Height = 14, Minimum = 0, Maximum = 100, Margin = new Thickness(0, 14, 0, 0) };
    private Action redraw;
    private TextBlock subtitle;

    // Values used by the pages; filled from the engine or, for a preview, by hand.
    public string Version = "", OlderVersion, NewerVersion, InstallFolder = @"C:\Program Files\Muelle", ErrorText;
    public bool Applying { get; private set; }

    public SetupWindow(SetupApplication app)
    {
        this.app = app;
        if (app != null)
        {
            Version = app.Version;
            OlderVersion = app.OlderVersion;
            NewerVersion = app.NewerVersion;
            InstallFolder = app.InstallFolder;
        }
        Width = 560;
        Height = 360;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(0xF6, 0xF7, 0xF9));
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        FontSize = 13;
        try { Icon = BitmapFrame.Create(new Uri("pack://application:,,,/muelle.ico")); } catch { }

        var root = new DockPanel { Margin = new Thickness(28, 22, 28, 20) };
        var header = BuildHeader();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(page);
        Content = root;

        Loc.Changed += Redraw;
        Closed += (s, e) => Loc.Changed -= Redraw;
        Closing += OnClosing;
        ShowLoading();
    }

    private void Redraw()
    {
        Title = tr("Instalación de Muelle", "Muelle Setup");
        if (subtitle != null) subtitle.Text = tr("Dock para Windows  ·  versión ", "A dock for Windows  ·  version ") + Version;
        redraw?.Invoke();
    }

    private FrameworkElement BuildHeader()
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 18) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        try
        {
            grid.Children.Add(new Image
            {
                Source = BitmapFrame.Create(new Uri("pack://application:,,,/muelle.ico")),
                Width = 48, Height = 48, Margin = new Thickness(0, 0, 14, 0),
            });
        }
        catch { /* the window still works without the icon */ }
        var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(new TextBlock { Text = "Muelle", FontSize = 24, FontWeight = FontWeights.SemiBold });
        subtitle = new TextBlock { Foreground = Brushes.DimGray };
        titles.Children.Add(subtitle);
        Grid.SetColumn(titles, 1);
        grid.Children.Add(titles);

        // The language menu: its label carries both languages, so it can be found from either one.
        var language = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
        language.Children.Add(new TextBlock { Text = "Idioma / Language", FontSize = 11.5, Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 3) });
        var combo = new ComboBox { Width = 120 };
        combo.Items.Add("English");
        combo.Items.Add("Español");
        combo.SelectedIndex = Loc.Current == UiLanguage.Es ? 1 : 0;
        combo.SelectionChanged += (s, e) => Loc.Current = combo.SelectedIndex == 1 ? UiLanguage.Es : UiLanguage.En;
        language.Children.Add(combo);
        Grid.SetColumn(language, 2);
        grid.Children.Add(language);

        return grid;
    }

    // ---------------- Pages ----------------
    private void Show(Action build)
    {
        redraw = build;
        Redraw();
    }

    public void ShowLoading() => Show(() =>
    {
        Body(tr("Revisando este equipo…", "Checking this computer…"));
        Buttons();
    });

    public void ShowStart(SetupState state)
    {
        switch (state)
        {
            case SetupState.Install:
                Show(() =>
                {
                    Body(tr("Muelle se instalará para todos los usuarios en:", "Muelle will be installed for all users in:"),
                         InstallFolder, null, LicenseLine());
                    Buttons((tr("Instalar", "Install"), () => app?.Plan(LaunchAction.Install), true), CancelButton());
                });
                break;
            case SetupState.Upgrade:
                Show(() =>
                {
                    Body(string.Format(tr("Muelle {0} está instalado. Se actualizará a la versión {1}; la configuración de cada usuario se conserva.",
                                          "Muelle {0} is installed. It will be updated to version {1}; each user's settings are kept."),
                                       OlderVersion, Version), null, null, LicenseLine());
                    Buttons((tr("Actualizar", "Update"), () => app?.Plan(LaunchAction.Install), true), CancelButton());
                });
                break;
            case SetupState.Maintenance:
                Show(() =>
                {
                    Body(string.Format(tr("Muelle {0} ya está instalado en este equipo.", "Muelle {0} is already installed on this computer."), Version),
                         null, tr("Reparar vuelve a copiar sus archivos; desinstalar lo quita del equipo.",
                                  "Repair copies its files again; uninstall removes it from the computer."));
                    Buttons((tr("Reparar", "Repair"), () => app?.Plan(LaunchAction.Repair), false),
                            (tr("Desinstalar", "Uninstall"), () => app?.Plan(LaunchAction.Uninstall), false),
                            CloseButton(true));
                });
                break;
            case SetupState.Uninstall:
                Show(() =>
                {
                    Body(tr("Muelle se quitará de este equipo.", "Muelle will be removed from this computer."), null,
                         tr("La configuración de cada usuario (carpeta %APPDATA%\\Muelle) se conserva.",
                            "Each user's settings (the %APPDATA%\\Muelle folder) are kept."));
                    Buttons((tr("Desinstalar", "Uninstall"), () => app?.Plan(LaunchAction.Uninstall), true), CancelButton());
                });
                break;
            case SetupState.Newer:
                Show(() =>
                {
                    Body(string.Format(tr("Ya está instalada una versión más nueva de Muelle ({0}). Esta versión no se puede instalar encima.",
                                          "A newer version of Muelle ({0}) is already installed. This version cannot be installed over it."),
                                       NewerVersion));
                    Buttons(CloseButton(true));
                });
                break;
        }
    }

    public void ShowProgress(LaunchAction action)
    {
        Applying = true;
        Show(() =>
        {
            string text = action switch
            {
                LaunchAction.Uninstall => tr("Desinstalando Muelle…", "Uninstalling Muelle…"),
                LaunchAction.Repair => tr("Reparando Muelle…", "Repairing Muelle…"),
                _ => OlderVersion != null ? tr("Actualizando Muelle…", "Updating Muelle…") : tr("Instalando Muelle…", "Installing Muelle…"),
            };
            var panel = BodyPanel(text);
            if (bar.Parent is Panel old) old.Children.Remove(bar);
            panel.Children.Add(bar);
            page.Content = panel;
            Buttons((tr("Cancelar", "Cancel"), () => { if (app != null) app.Canceled = true; }, false));
        });
    }

    public void SetProgress(int percent) => bar.Value = Math.Max(bar.Value, Math.Min(100, Math.Max(0, percent)));

    public void ShowResult(bool ok, bool canceled, bool restart)
    {
        Applying = false;
        var action = app?.Planned ?? LaunchAction.Install;
        Show(() =>
        {
            if (canceled)
            {
                Body(tr("Se canceló. El equipo quedó como estaba.", "Canceled. The computer was left as it was."));
                Buttons(CloseButton(true));
                return;
            }
            if (!ok)
            {
                Body(tr("La operación no se pudo completar.", "The operation could not be completed."),
                     null, ErrorText ?? app?.LastError);
                Buttons((tr("Ver registro", "View log"), OpenLog, false), CloseButton(true));
                return;
            }
            string done = action switch
            {
                LaunchAction.Uninstall => tr("Muelle se desinstaló.", "Muelle was uninstalled."),
                LaunchAction.Repair => tr("Muelle quedó reparado.", "Muelle was repaired."),
                _ => tr("Muelle quedó instalado. Está en el menú Inicio.", "Muelle is installed. It is in the Start menu."),
            };
            Body(done, null, restart ? tr("Hay que reiniciar Windows para terminar.", "Windows must be restarted to finish.") : null);
            if (action == LaunchAction.Install)
                Buttons((tr("Abrir Muelle", "Open Muelle"), LaunchMuelle, true), CloseButton(false));
            else
                Buttons(CloseButton(true));
        });
    }

    // ---------------- Building blocks ----------------
    private StackPanel BodyPanel(string text)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 14 });
        return panel;
    }

    /// <summary>Page body: a main text, an optional highlighted line, an optional note and an optional extra block.</summary>
    private void Body(string text, string highlight = null, string note = null, UIElement extra = null)
    {
        var panel = BodyPanel(text);
        if (highlight != null)
            panel.Children.Add(new TextBlock { Text = highlight, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap });
        if (note != null)
            panel.Children.Add(new TextBlock { Text = note, Foreground = Brushes.DimGray, Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap });
        if (extra != null) panel.Children.Add(extra);
        page.Content = panel;
    }

    private UIElement LicenseLine()
    {
        var link = new Hyperlink(new Run(tr("Licencia Pública General GNU, versión 2", "GNU General Public License, version 2")))
        {
            NavigateUri = new Uri(LicenseUrl),
        };
        link.RequestNavigate += (s, e) => Open(LicenseUrl);
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 16, 0, 0) };
        text.Inlines.Add(new Run(tr("Muelle es software libre, bajo la ", "Muelle is free software, under the ")));
        text.Inlines.Add(link);
        text.Inlines.Add(new Run("."));
        return text;
    }

    private void Buttons(params (string text, Action action, bool isDefault)[] list)
    {
        buttons.Children.Clear();
        foreach (var (text, action, isDefault) in list)
        {
            var b = new Button { Content = text, MinWidth = 96, Padding = new Thickness(14, 5, 14, 5), Margin = new Thickness(8, 0, 0, 0), IsDefault = isDefault };
            b.Click += (s, e) => action();
            buttons.Children.Add(b);
        }
    }

    private (string, Action, bool) CancelButton() => (tr("Cancelar", "Cancel"), Close, false);
    private (string, Action, bool) CloseButton(bool isDefault) => (tr("Cerrar", "Close"), Close, isDefault);

    private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!Applying || app == null)
        {
            if (app != null && app.Planned == LaunchAction.Unknown) app.CancelBeforeStart();
            return;
        }
        // While installing, closing the window means canceling; the engine rolls the changes back.
        e.Cancel = true;
        var answer = MessageBox.Show(this, tr("¿Cancelar la operación? Se deshará lo hecho hasta ahora.", "Cancel the operation? What was done so far will be undone."),
                                     "Muelle", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes && app != null) app.Canceled = true;
    }

    private void LaunchMuelle()
    {
        Open(Path.Combine(InstallFolder, "Muelle.exe"));
        Close();
    }

    private void OpenLog()
    {
        string log = app?.LogPath;
        if (!string.IsNullOrEmpty(log)) Open(log);
    }

    private static void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); } catch { }
    }
}
