//
//  Theme.cs
//  Muelle: a dock for Windows.
//
//  Copyright (C) 2026 Gabriel Castiglia. Licensed under the GNU GPL, version 2 only.
//  Date: November 11, 2026
//  SPDX-License-Identifier: GPL-2.0-only
//
//  This program is free software: you can redistribute it and/or modify it under the terms of
//  the GNU General Public License, version 2, as published by the Free Software Foundation.
//  It is distributed WITHOUT ANY WARRANTY. See the LICENSE file for details.
//

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Muelle;

/// <summary>
/// An accent colour: the tile of the icon (icons/muelle_&lt;key&gt;.ico, drawn by scripts/make_icon.py, which
/// keeps the same keys and the light tones as tiles) and the highlight colour of the windows and the dock.
/// </summary>
internal sealed record Accent(string Key, string Es, string En, Color Light, Color Dark)
{
    public string Name => Loc.tr(Es, En);
}

/// <summary>
/// Light, dark or as Windows, and the accent colour. The colours of the windows are brushes in the
/// application resources (Theme.xaml), replaced here; the dock reads its own from <see cref="Dock"/>.
/// </summary>
internal static class Theme
{
    public const string DefaultAccent = "blue";

    public static readonly Accent[] Accents =
    {
        new("blue", "Azul", "Blue", Rgb(0x1F5FD1), Rgb(0x6E9BFF)),          // the sea: the default
        new("gold", "Dorado", "Gold", Rgb(0x9A6F00), Rgb(0xE2B84C)),
        new("purple", "Morado", "Purple", Rgb(0x5B2A86), Rgb(0xB392E0)),
        new("sky", "Celeste", "Sky blue", Rgb(0x1A8CD8), Rgb(0x5CB8F0)),
        new("ember", "Naranja", "Orange", Rgb(0xD9652F), Rgb(0xF08A5D)),
        new("red", "Rojo", "Red", Rgb(0xC8102E), Rgb(0xF2545B)),
        new("yellow", "Amarillo", "Yellow", Rgb(0xB88A00), Rgb(0xF5C842)),
        new("green", "Verde", "Green", Rgb(0x1E8A43), Rgb(0x4CC27A)),
        new("teal", "Verde azulado", "Teal", Rgb(0x00796B), Rgb(0x4DB6AC)),
        new("pink", "Rosa", "Pink", Rgb(0xC2185B), Rgb(0xF06292)),
        new("black", "Negro", "Black", Rgb(0x1D1D1F), Rgb(0xC7C7CC)),
        new("white", "Blanco", "White", Rgb(0x8E8E93), Rgb(0xF5F5F7)),
    };

    /// <summary>Appearance settings: as Windows, light or dark.</summary>
    public const string Auto = "auto", Light = "light", Dark = "dark";

    public static Accent Get(string key) => Accents.FirstOrDefault(a => a.Key == key) ?? Accents[0];
    public static bool IsKnownAccent(string key) => Accents.Any(a => a.Key == key);

    public static bool IsDark { get; private set; } = true;
    public static Accent Current { get; private set; } = Accents[0];
    public static Color AccentColor => IsDark ? Current.Dark : Current.Light;
    public static DockPalette Dock { get; private set; } = DockPalette.For(true, Accents[0].Dark);

    /// <summary>Raised after the appearance or the accent changes, so open windows and the docks redraw.</summary>
    public static event Action Changed;

    private static string appearance = Dark;
    private static bool loaded;

    /// <summary>Sets the appearance ("auto", "light" or "dark") and the accent, and applies them.</summary>
    public static void Set(string appearanceSetting, string accentKey)
    {
        if (!loaded)
        {
            loaded = true;
            Application.Current.Resources.MergedDictionaries.Add(
                new ResourceDictionary { Source = new Uri("pack://application:,,,/Muelle;component/Theme.xaml") });
            // "As Windows": follow the change of Settings → Personalization → Colors while Muelle runs.
            SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (e.Category != UserPreferenceCategory.General || appearance != Auto) return;
                Application.Current?.Dispatcher.BeginInvoke(new Action(() => { if (SystemDark() != IsDark) Set(appearance, Current.Key); }));
            };
        }
        appearance = appearanceSetting is Light or Dark ? appearanceSetting : Auto;
        Current = Get(accentKey);
        IsDark = appearance == Auto ? SystemDark() : appearance == Dark;
        Dock = DockPalette.For(IsDark, AccentColor);
        ApplyResources();
        Changed?.Invoke();
    }

    private static readonly Dictionary<string, (uint Light, uint Dark)> Palette = new()
    {
        ["Mu.Window"] = (0xF3F3F6, 0x1C1C1E),
        ["Mu.Card"] = (0xFFFFFF, 0x2A2A2D),
        ["Mu.CardBorder"] = (0xE3E3E8, 0x38383B),
        ["Mu.Popup"] = (0xFCFCFD, 0x2C2C2F),
        ["Mu.PopupBorder"] = (0xE0E0E5, 0x3A3A3C),
        ["Mu.Text"] = (0x1D1D1F, 0xF5F5F7),
        ["Mu.TextMuted"] = (0x6E6E73, 0xA1A1A6),
        ["Mu.Hover"] = (0xEBEBF0, 0x3A3A3D),
        ["Mu.Pressed"] = (0xE1E1E6, 0x454548),
        ["Mu.Separator"] = (0xE8E8ED, 0x38383A),
        ["Mu.Field"] = (0xFFFFFF, 0x1F1F22),
        ["Mu.Track"] = (0xD1D1D6, 0x55555A),
    };

    private static void ApplyResources()
    {
        var res = Application.Current.Resources;
        foreach (var (key, (light, dark)) in Palette) res[key] = Frozen(Rgb(IsDark ? dark : light));
        var a = AccentColor;
        res["Mu.Accent"] = Frozen(a);
        res["Mu.AccentSoft"] = Frozen(Color.FromArgb(IsDark ? (byte)0x50 : (byte)0x2E, a.R, a.G, a.B));
        res["Mu.OnAccent"] = Frozen(OnColor(a));
    }

    /// <summary>Text on a fill of colour c: nearly black on pale tones, white otherwise.</summary>
    public static Color OnColor(Color c) =>
        (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255 > 0.6 ? Rgb(0x111111) : Colors.White;

    /// <summary>Windows "app mode": true when apps are set to dark (Settings → Personalization → Colors).</summary>
    public static bool SystemDark()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return k?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }

    /// <summary>The icon in the chosen colour (all sizes), for the window and its taskbar button.</summary>
    public static ImageSource WindowIcon() => LoadIcon(null);

    /// <summary>One frame of the icon in the colour given (or the chosen one), the nearest to <paramref name="size"/>.</summary>
    public static ImageSource Icon(string key = null, int size = 256)
    {
        try
        {
            var dec = BitmapDecoder.Create(IconUri(key), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            return dec.Frames.OrderBy(f => Math.Abs(f.PixelWidth - size)).First();
        }
        catch { return null; }
    }

    private static ImageSource LoadIcon(string key)
    {
        try { return BitmapFrame.Create(IconUri(key), BitmapCreateOptions.None, BitmapCacheOption.OnLoad); }
        catch { return null; }
    }

    private static Uri IconUri(string key) => new($"pack://application:,,,/icons/muelle_{Get(key ?? Current.Key).Key}.ico");

    /// <summary>
    /// Gives a window the colours of the theme and keeps them: background, text, icon and, on Windows 11,
    /// a title bar of the same colour as the window.
    /// </summary>
    public static void Attach(Window w)
    {
        w.SetResourceReference(Window.BackgroundProperty, "Mu.Window");
        w.SetResourceReference(Window.ForegroundProperty, "Mu.Text");
        w.SetResourceReference(Window.FontFamilyProperty, "Mu.Font");
        void Refresh()
        {
            w.Icon = WindowIcon();
            TitleBar(w);
        }
        Refresh();
        w.SourceInitialized += (s, e) => TitleBar(w);
        Changed += Refresh;
        w.Closed += (s, e) => Changed -= Refresh;
    }

    private static void TitleBar(Window w)
    {
        var hwnd = new WindowInteropHelper(w).Handle;
        if (hwnd == IntPtr.Zero) return;
        int on = IsDark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int));                       // DWMWA_USE_IMMERSIVE_DARK_MODE
        var c = Rgb(IsDark ? Palette["Mu.Window"].Dark : Palette["Mu.Window"].Light);
        int caption = c.R | (c.G << 8) | (c.B << 16);
        DwmSetWindowAttribute(hwnd, 35, ref caption, sizeof(int));                  // DWMWA_CAPTION_COLOR (Windows 11)
        var t = Rgb(IsDark ? Palette["Mu.Text"].Dark : Palette["Mu.Text"].Light);
        int text = t.R | (t.G << 8) | (t.B << 16);
        DwmSetWindowAttribute(hwnd, 36, ref text, sizeof(int));                     // DWMWA_TEXT_COLOR (Windows 11)
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public static Color Rgb(uint v) => Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
    public static Color Argb(uint v) => Color.FromArgb((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
    public static SolidColorBrush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
}

/// <summary>The colours of the dock itself: dark glass on a dark theme, frosted light glass on a light one.</summary>
internal sealed class DockPalette
{
    public Brush GlassBase, GlassSheen, PanelEdge, PanelHover, DotBrush, LabelBrush, LabelText, DashFill, Mark;
    public Pen GlassPen, GlassInner, DotPen, LabelPen, DashPen;
    /// <summary>Panel colour; its opacity comes from the settings.</summary>
    public Color Panel;
    public Color Shadow;

    public static DockPalette For(bool dark, Color accent)
    {
        static Pen P(uint argb, double w) { var p = new Pen(Theme.Frozen(Theme.Argb(argb)), w); p.Freeze(); return p; }
        static Brush B(uint argb) => Theme.Frozen(Theme.Argb(argb));
        static Brush Sheen(uint a0, uint a1, uint a2)
        {
            var g = new LinearGradientBrush(new GradientStopCollection
            {
                new GradientStop(Theme.Argb(a0 << 24 | 0xFFFFFF), 0),
                new GradientStop(Theme.Argb(a1 << 24 | 0xFFFFFF), 0.45),
                new GradientStop(Theme.Argb(a2 << 24 | 0xFFFFFF), 1),
            }, new Point(0, 0), new Point(1, 1));
            g.Freeze();
            return g;
        }
        var dash = dark ? P(0xC0FFFFFF, 1.6) : P(0xA0303036, 1.6);
        dash = dash.Clone();
        dash.DashStyle = new DashStyle(new double[] { 3, 2.5 }, 0);
        dash.Freeze();
        var p = new DockPalette
        {
            GlassBase = dark ? B(0x70161921) : B(0xA8F2F2F5),
            GlassSheen = dark ? Sheen(0x34, 0x10, 0x06) : Sheen(0x80, 0x30, 0x18),
            GlassPen = dark ? P(0x55FFFFFF, 1) : P(0x90FFFFFF, 1),
            GlassInner = dark ? P(0x30000000, 1) : P(0x26000000, 1),
            Panel = dark ? Theme.Rgb(0x1D1F26) : Theme.Rgb(0xF2F2F5),
            PanelEdge = dark ? B(0x40FFFFFF) : B(0x1F000000),
            PanelHover = dark ? B(0x38FFFFFF) : B(0x1A000000),
            DotBrush = Theme.Frozen(accent),
            DotPen = dark ? P(0x70000000, 0.8) : P(0x50FFFFFF, 0.8),
            LabelBrush = dark ? B(0xE61E2129) : B(0xF2FFFFFF),
            LabelText = dark ? Brushes.White : Theme.Frozen(Theme.Rgb(0x1D1D1F)),
            LabelPen = dark ? P(0x40FFFFFF, 1) : P(0x22000000, 1),
            DashPen = dash,
            DashFill = dark ? B(0x30FFFFFF) : B(0x18000000),
            Mark = dark ? Brushes.White : Theme.Frozen(Theme.Rgb(0x303036)),
            Shadow = Colors.Black,
        };
        return p;
    }
}
