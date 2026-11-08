//
//  DockView.cs
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
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using static Muelle.Loc;

namespace Muelle;

/// <summary>
/// Draws the dock and handles the pointer: magnification zoom, bounce, running-app indicator,
/// drag to reorder or remove, and file drop to add.
/// Works on two axes: "a" (along the dock) and "c" (distance from the screen edge).
/// </summary>
internal sealed class DockView : FrameworkElement
{
    private sealed class Entry
    {
        public DockItem Item;
        public bool IsGap, IsPlaceholder;
        public double W = 1, BaseCenter, Pos, Size, Center;
    }

    private const double BouncePeriod = 0.55;

    private readonly DockWindow host;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private double lastTime, lastHover;
    private bool animating;

    private Point mouse;
    private bool mouseInside;
    private double k, kTarget;              // how much zoom is applied right now (0..1)

    private DockItem pressItem;
    private Point pressPoint;
    private bool dragging, removeIntent, externalDrag, gapIsExternal;
    private int gapIndex = -1;
    private double gapWeight, gapTarget;
    private double hideT;                   // 0 = visible, 1 = hidden
    private readonly List<(Point p, double t)> poofs = new();

    private List<Entry> layout = new();
    private double shelfA0, shelfA1;
    private int hoverIndex = -1;

    /// <summary>Test screenshot only: simulates the pointer hovering over this icon.</summary>
    public int DemoHoverIndex = -1;

    private static readonly Brush Catch = Freeze(new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)));
    private static readonly Brush GlassBase = Freeze(new SolidColorBrush(Color.FromArgb(0x70, 0x16, 0x19, 0x21)));
    private static readonly Brush GlassSheen = Freeze(new LinearGradientBrush(
        new GradientStopCollection
        {
            new GradientStop(Color.FromArgb(0x34, 0xFF, 0xFF, 0xFF), 0),
            new GradientStop(Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF), 0.45),
            new GradientStop(Color.FromArgb(0x06, 0xFF, 0xFF, 0xFF), 1),
        }, new Point(0, 0), new Point(1, 1)));
    private static readonly Pen GlassPen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF))), 1));
    private static readonly Pen GlassInner = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromArgb(0x30, 0x00, 0x00, 0x00))), 1));
    private Brush panelBrush;               // panel background, recreated when the opacity changes
    private int panelAlpha = -1;
    private static readonly Brush PanelEdge = Freeze(new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)));
    private static readonly Brush PanelHover = Freeze(new SolidColorBrush(Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF)));
    private static readonly Brush DotBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF)));
    private static readonly Pen DotPen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromArgb(0x70, 0, 0, 0))), 0.8));
    private static readonly Brush LabelBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xE6, 0x1E, 0x21, 0x29)));
    private static readonly Pen LabelPen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF))), 1));
    private static readonly Pen DashPen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromArgb(0xC0, 0xFF, 0xFF, 0xFF))), 1.6)
    { DashStyle = new DashStyle(new double[] { 3, 2.5 }, 0) });
    private static readonly Brush DashFill = Freeze(new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)));
    private static readonly Brush RemoveBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xE5, 0x48, 0x4D)));
    private static readonly Typeface Face = new(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);

    private static T Freeze<T>(T f) where T : Freezable { f.Freeze(); return f; }

    public DockView(DockWindow host)
    {
        this.host = host;
        Focusable = false;
        AllowDrop = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        Effect = new DropShadowEffect
        {
            BlurRadius = 12, ShadowDepth = 1.5, Direction = 270, Opacity = 0.38,
            Color = Colors.Black, RenderingBias = RenderingBias.Performance,
        };
    }

    // ---------------- Measurements (in logical pixels) ----------------
    private DockConfig Cfg => host.Config;
    /// <summary>
    /// Effective icon size: the configured one, or smaller if the icons do not fit along this
    /// screen. Each window computes it from its own length, so it only shrinks on the small screen.
    /// </summary>
    public double Base
    {
        get
        {
            double size = Cfg.IconSize, len = L;
            int n = Math.Max(1, host.Items.Count);
            if (len <= 0) return size;
            // Occupied length ≈ n·b + (n−1)·0.22·b (gaps) + 2·(0.3·b + 2) (background margins).
            double fit = Math.Floor((len * 0.96 - 4) / (1.22 * n + 0.38));
            return Math.Max(16, Math.Min(size, fit));
        }
    }
    public bool IsPanel => Cfg.Style == DockStyle.Panel;
    public double Zoom => IsPanel ? Base : Math.Max(Base, Cfg.ZoomSize);
    public double Pad => Math.Round(Base * 0.3) + 2;
    public double Thickness => Base + 2 * Pad;
    private double Gap => Math.Round(Base * 0.22);
    public bool Vertical => Cfg.Edge is DockEdge.Left or DockEdge.Right;
    /// <summary>Total window width: the panel plus room for the zoom, the bounce and the label.</summary>
    public double CrossExtent => Thickness + (Zoom - Base) + Base * 0.75 + (Vertical ? 230 : 46);
    private double L => Vertical ? ActualHeight : ActualWidth;

    private Rect R(double a, double c, double alongLen, double crossLen)
    {
        alongLen = Math.Max(0, alongLen); crossLen = Math.Max(0, crossLen);
        return Cfg.Edge switch
        {
            DockEdge.Left => new Rect(c, a, crossLen, alongLen),
            DockEdge.Right => new Rect(ActualWidth - c - crossLen, a, crossLen, alongLen),
            DockEdge.Top => new Rect(a, c, alongLen, crossLen),
            _ => new Rect(a, ActualHeight - c - crossLen, alongLen, crossLen),
        };
    }

    private Point P(double a, double c) => R(a, c, 0, 0).TopLeft;

    /// <summary>Icon rectangle in screen coordinates (physical pixels), or null if it is not in the dock.</summary>
    public Native.RECT? IconScreenRect(DockItem item)
    {
        if (!IsVisible) return null;
        if (layout.Count == 0) ComputeLayout();
        var e = layout.FirstOrDefault(x => x.Item == item);
        if (e == null) return null;
        var r = R(e.Pos, Pad, e.Size, e.Size);
        Point a = PointToScreen(r.TopLeft), b = PointToScreen(r.BottomRight);
        return new Native.RECT((int)a.X, (int)a.Y, (int)b.X, (int)b.Y);
    }

    private (double a, double c) AC(Point p) => Cfg.Edge switch
    {
        DockEdge.Left => (p.Y, p.X),
        DockEdge.Right => (p.Y, ActualWidth - p.X),
        DockEdge.Top => (p.X, p.Y),
        _ => (p.X, ActualHeight - p.Y),
    };

    // ---------------- Animation ----------------
    public void Kick()
    {
        if (animating) return;
        animating = true;
        lastTime = clock.Elapsed.TotalSeconds;
        CompositionTarget.Rendering += OnFrame;
    }

    private void OnFrame(object sender, EventArgs e)
    {
        double now = clock.Elapsed.TotalSeconds;
        double dt = Math.Clamp(now - lastTime, 0, 0.1);
        lastTime = now;
        bool busy = Step(now, dt);
        InvalidateVisual();
        host.OnFrame();
        if (!busy && !host.KeepAnimating)
        {
            animating = false;
            CompositionTarget.Rendering -= OnFrame;
        }
    }

    private static double Approach(double v, double target, double dt, double rate) =>
        target + (v - target) * Math.Exp(-rate * dt);

    private bool Step(double now, double dt)
    {
        bool hover = mouseInside || externalDrag || dragging || host.MenuOpen;
        if (hover) lastHover = now;

        double hideTarget = Cfg.AutoHide && !hover && now - lastHover > 0.7 ? 1 : 0;
        hideT = Approach(hideT, hideTarget, dt, 11);

        bool zone = (mouseInside || externalDrag || dragging) && hideT < 0.5;
        if (DemoHoverIndex >= 0) zone = true;
        kTarget = zone ? 1 : 0;
        k = Approach(k, kTarget, dt, 13);

        gapWeight = Approach(gapWeight, gapTarget, dt, 16);
        if (!dragging && !externalDrag && gapTarget == 0 && gapWeight < 0.01) { gapWeight = 0; gapIndex = -1; }

        bool bouncing = false;
        foreach (var it in host.Items)
        {
            if (double.IsNaN(it.BounceStart)) continue;
            int idx = (int)((now - it.BounceStart) / BouncePeriod);
            if (idx != it.BounceIdx)
            {
                it.BounceIdx = idx;
                if (idx >= 4 || (idx >= 2 && it.Running)) { it.BounceStart = double.NaN; continue; }
            }
            bouncing = true;
        }
        poofs.RemoveAll(p => now - p.t > 0.45);

        bool settled = Math.Abs(k - kTarget) < 0.002 && Math.Abs(gapWeight - gapTarget) < 0.002
                       && Math.Abs(hideT - hideTarget) < 0.002 && !bouncing && poofs.Count == 0;
        if (settled) { k = kTarget; gapWeight = gapTarget; hideT = hideTarget; }
        bool waitingToHide = Cfg.AutoHide && hideTarget == 0 && !hover;
        return !settled || waitingToHide || dragging;
    }

    public void StartBounce(DockItem it)
    {
        it.BounceStart = clock.Elapsed.TotalSeconds;
        it.BounceIdx = 0;
        Kick();
    }

    private double BounceOffset(DockItem it, double now)
    {
        if (double.IsNaN(it.BounceStart)) return 0;
        double t = (now - it.BounceStart) / BouncePeriod;
        int idx = (int)t;
        double f = t - idx;
        return Base * 0.62 * 4 * f * (1 - f) * Math.Max(0.3, 1 - 0.18 * idx);
    }

    // ---------------- Layout (Mac-style zoom) ----------------
    private void ComputeLayout()
    {
        var list = new List<Entry>();
        foreach (var it in host.Items)
        {
            if (dragging && it == pressItem) continue;
            list.Add(new Entry { Item = it });
        }
        if (gapIndex >= 0 && gapWeight > 0.001)
            list.Insert(Math.Clamp(gapIndex, 0, list.Count), new Entry { IsGap = true, W = gapWeight });
        if (list.Count == 0) list.Add(new Entry { IsPlaceholder = true });

        double b = Base, g = Gap, z = Zoom, range = b * 2.7;
        double total = -g;
        foreach (var e in list) total += (b + g) * e.W;
        // Panel: icons aligned to the start of the edge; dock: centered.
        double start = IsPanel ? Math.Round(Base * 0.35) : (L - total) / 2, a = start;
        foreach (var e in list) { e.BaseCenter = a + b * e.W / 2; a += (b + g) * e.W; }

        double mA = AC(mouse).a;
        if (DemoHoverIndex >= 0 && DemoHoverIndex < list.Count) mA = list[DemoHoverIndex].BaseCenter + b * 0.18;

        double total2 = -g;
        foreach (var e in list)
        {
            double d = Math.Abs(mA - e.BaseCenter) / range;
            double f = d < 1 ? 0.5 + 0.5 * Math.Cos(Math.PI * d) : 0;
            e.Size = e.W * (b + (z - b) * k * f);
            total2 += e.Size + g * e.W;
        }
        double frac = total > 0 ? Math.Clamp((mA - start) / total, 0, 1) : 0.5;
        double s2 = start - frac * (total2 - total);
        a = s2;
        foreach (var e in list) { e.Pos = a; e.Center = a + e.Size / 2; a += e.Size + g * e.W; }
        shelfA0 = s2;
        shelfA1 = s2 + Math.Max(total2, b);
        layout = list;

        hoverIndex = -1;
        if (DemoHoverIndex >= 0) hoverIndex = Math.Min(DemoHoverIndex, list.Count - 1);
        else if ((mouseInside || externalDrag) && !dragging)
        {
            var (ma, mc) = AC(mouse);
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e.IsGap) continue;
                if (ma >= e.Pos - g / 2 && ma < e.Pos + e.Size + g / 2 && mc < Pad + e.Size + 6) { hoverIndex = i; break; }
            }
        }
    }

    private int InsertionIndex(double mA)
    {
        int idx = 0;
        foreach (var e in layout)
            if (!e.IsGap && !e.IsPlaceholder && e.Center < mA) idx++;
        return idx;
    }

    // ---------------- Rendering ----------------
    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth < 1 || ActualHeight < 1) return;
        double now = clock.Elapsed.TotalSeconds;
        ComputeLayout();
        double T = Thickness, b = Base;
        double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double hideOff = -hideT * (T + (Zoom - b) + b * 0.8 + 14);

        // Nearly transparent area that receives the pointer and dropped files.
        if (hideT < 0.97) dc.DrawRectangle(Catch, null, R(0, hideOff, L, T));
        if (Cfg.AutoHide) dc.DrawRectangle(Catch, null, R(0, 0, L, 3 / ppd));

        // Fixed panel: continuous edge-to-edge bar, attached to the side of the screen.
        if (IsPanel)
        {
            byte alpha = (byte)Math.Round(Cfg.PanelOpacity * 2.55);
            if (alpha != panelAlpha) { panelAlpha = alpha; panelBrush = Freeze(new SolidColorBrush(Color.FromArgb(alpha, 0x1D, 0x1F, 0x26))); }
            dc.DrawRectangle(panelBrush, null, R(0, hideOff, L, T));
            dc.DrawRectangle(PanelEdge, null, R(0, T - 1 + hideOff, L, 1));
            if (hoverIndex >= 0 && hoverIndex < layout.Count && !layout[hoverIndex].IsGap && !dragging)
            {
                var he = layout[hoverIndex];
                double m = Math.Round(Base * 0.14) + 2;
                dc.DrawRoundedRectangle(PanelHover, null, R(he.Pos - m, Pad - m + hideOff, he.Size + 2 * m, he.Size + 2 * m), 8, 8);
            }
        }
        // Floating dock: glass-effect background.
        else if (Cfg.Backdrop == Backdrop.Glass)
        {
            var r = R(shelfA0 - 10, 5 + hideOff, shelfA1 - shelfA0 + 20, T - 10);
            double rad = Math.Min(18, (T - 10) / 2.6);
            dc.DrawRoundedRectangle(GlassBase, GlassInner, r, rad, rad);
            dc.DrawRoundedRectangle(GlassSheen, null, r, rad, rad);
            var inner = r; inner.Inflate(-1, -1);
            dc.DrawRoundedRectangle(null, GlassPen, inner, rad - 1, rad - 1);
        }

        double dotC = (5 + Pad) / 2 + hideOff;
        for (int i = 0; i < layout.Count; i++)
        {
            var e = layout[i];
            if (e.IsGap)
            {
                if (gapIsExternal && e.W > 0.35) DrawDropSlot(dc, R(e.Pos, Pad + hideOff, e.Size, e.Size));
                continue;
            }
            if (e.IsPlaceholder)
            {
                DrawDropSlot(dc, R(e.Pos, Pad + hideOff, e.Size, e.Size));
                continue;
            }
            var it = e.Item;
            double bo = BounceOffset(it, now);
            DrawIcon(dc, it, R(e.Pos, Pad + bo + hideOff, e.Size, e.Size));
            if (it.Running) dc.DrawEllipse(DotBrush, DotPen, P(e.Center, dotC), 2.4, 2.4);
        }

        // Name label.
        if (hoverIndex >= 0 && hoverIndex < layout.Count && k > 0.04 && !dragging && !host.MenuOpen)
        {
            var e = layout[hoverIndex];
            string text = e.IsPlaceholder ? tr("Arrastra aquí tus apps", "Drag your apps here") : e.Item?.Name;
            if (!string.IsNullOrEmpty(text))
            {
                var ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Face, 12.5, Brushes.White, ppd);
                double w = ft.Width + 20, h = ft.Height + 8;
                double bo = e.Item != null ? BounceOffset(e.Item, now) : 0;
                double c0 = Pad + e.Size + bo + (Vertical ? 12 : 8) + hideOff;
                var r = Vertical ? R(e.Center - h / 2, c0, h, w) : R(e.Center - w / 2, c0, w, h);
                dc.PushOpacity(Math.Clamp(k * 1.4, 0, 1));
                dc.DrawRoundedRectangle(LabelBrush, LabelPen, r, h / 2, h / 2);
                dc.DrawText(ft, new Point(r.X + 10, r.Y + 4));
                dc.Pop();
            }
        }

        // Icon being dragged.
        if (dragging && pressItem != null)
        {
            double sz = b * 1.15;
            var r = new Rect(mouse.X - sz / 2, mouse.Y - sz / 2, sz, sz);
            dc.PushOpacity(removeIntent ? 0.5 : 0.92);
            DrawIcon(dc, pressItem, r);
            dc.Pop();
            if (removeIntent)
            {
                var c = new Point(r.Right - 3, r.Top + 3);
                dc.DrawEllipse(RemoveBrush, DotPen, c, 8, 8);
                var x = Freeze(new Pen(Brushes.White, 1.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });
                dc.DrawLine(x, new Point(c.X - 3.2, c.Y - 3.2), new Point(c.X + 3.2, c.Y + 3.2));
                dc.DrawLine(x, new Point(c.X - 3.2, c.Y + 3.2), new Point(c.X + 3.2, c.Y - 3.2));
            }
        }

        // Fade-out animation when an icon is removed.
        foreach (var (p, t0) in poofs)
        {
            double f = Math.Clamp((now - t0) / 0.45, 0, 1);
            byte alpha = (byte)(220 * (1 - f));
            var br = new SolidColorBrush(Color.FromArgb(alpha, 0xFF, 0xFF, 0xFF));
            for (int i = 0; i < 7; i++)
            {
                double ang = i * Math.PI * 2 / 7 + 0.4;
                double dist = 6 + 26 * f;
                dc.DrawEllipse(br, null, new Point(p.X + Math.Cos(ang) * dist, p.Y + Math.Sin(ang) * dist), 5 * (1 - f) + 1.5, 5 * (1 - f) + 1.5);
            }
        }
    }

    private void DrawDropSlot(DrawingContext dc, Rect r)
    {
        if (r.Width < 4) return;
        var rr = r; rr.Inflate(-2, -2);
        double rad = rr.Width * 0.24;
        dc.DrawRoundedRectangle(DashFill, DashPen, rr, rad, rad);
        var c = new Point(rr.X + rr.Width / 2, rr.Y + rr.Height / 2);
        double s = rr.Width * 0.18;
        var pen = new Pen(Brushes.White, Math.Max(1.5, rr.Width * 0.06)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawLine(pen, new Point(c.X - s, c.Y), new Point(c.X + s, c.Y));
        dc.DrawLine(pen, new Point(c.X, c.Y - s), new Point(c.X, c.Y + s));
    }

    private static void DrawIcon(DrawingContext dc, DockItem it, Rect r)
    {
        if (r.Width < 1) return;
        if (it.Icon != null) { dc.DrawImage(it.Icon, r); return; }
        // No icon: colored rounded square with the initial.
        int hash = 0;
        foreach (char ch in it.Name ?? "") hash = hash * 31 + ch;
        double hue = (hash & 0xFFFF) % 360;
        var top = FromHsl(hue, 0.55, 0.58); var bottom = FromHsl(hue + 25, 0.6, 0.42);
        var brush = new LinearGradientBrush(top, bottom, 90);
        double rad = r.Width * 0.24;
        var rr = r; rr.Inflate(-r.Width * 0.06, -r.Width * 0.06);
        dc.DrawRoundedRectangle(brush, null, rr, rad, rad);
        string letter = string.IsNullOrEmpty(it.Name) ? "?" : it.Name.Substring(0, 1).ToUpperInvariant();
        var ft = new FormattedText(letter, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Face, rr.Height * 0.5, Brushes.White, 1.25);
        dc.DrawText(ft, new Point(rr.X + (rr.Width - ft.Width) / 2, rr.Y + (rr.Height - ft.Height) / 2));
    }

    private static Color FromHsl(double h, double s, double l)
    {
        h = ((h % 360) + 360) % 360 / 360;
        double q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
        double Hue(double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 0.5) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }
        return Color.FromRgb((byte)(Hue(h + 1.0 / 3) * 255), (byte)(Hue(h) * 255), (byte)(Hue(h - 1.0 / 3) * 255));
    }

    // ---------------- Pointer ----------------
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        mouseInside = true;
        mouse = e.GetPosition(this);
        Kick();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        if (!dragging && pressItem == null) mouseInside = false;
        Kick();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        mouse = e.GetPosition(this);
        if (!dragging && pressItem == null) mouseInside = true;
        // With locked icons, dragging is disabled (neither reordering nor removing).
        if (pressItem != null && !dragging && !Cfg.LockItems && (mouse - pressPoint).Length > 6) BeginDrag();
        if (dragging) UpdateDrag();
        Kick();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        mouse = e.GetPosition(this);
        ComputeLayout();
        if (hoverIndex >= 0)
        {
            var en = layout[hoverIndex];
            if (en.IsPlaceholder) { host.AddWithDialog(0); e.Handled = true; return; }
            pressItem = en.Item;
            pressPoint = mouse;
            CaptureMouse();
        }
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        mouse = e.GetPosition(this);
        var item = pressItem;
        if (dragging)
        {
            dragging = false;
            ReleaseMouseCapture();
            FinishDrag(item);
        }
        else if (item != null)
        {
            pressItem = null;
            ReleaseMouseCapture();
            ComputeLayout();
            if (hoverIndex >= 0 && layout[hoverIndex].Item == item)
                host.ActivateItem(item, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
        }
        pressItem = null;
        mouseInside = new Rect(RenderSize).Contains(mouse) && AC(mouse).c < Thickness + Zoom;
        e.Handled = true;
        Kick();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        if (dragging)
        {
            // Drag interrupted (e.g., another window captured the pointer): the original position is restored.
            dragging = false; pressItem = null; gapTarget = 0; gapIndex = -1; gapWeight = 0;
            Kick();
        }
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ChangedButton == MouseButton.Middle && hoverIndex >= 0 && layout[hoverIndex].Item != null)
        {
            host.ActivateItem(layout[hoverIndex].Item, true);
            e.Handled = true;
        }
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        mouse = e.GetPosition(this);
        ComputeLayout();
        var item = hoverIndex >= 0 ? layout[hoverIndex].Item : null;
        int at = hoverIndex >= 0 ? InsertionIndex(layout[hoverIndex].Center + 1) : host.Items.Count;
        host.ShowMenu(item, at);
        e.Handled = true;
    }

    private void BeginDrag()
    {
        dragging = true;
        gapIsExternal = false;
        gapIndex = host.Items.IndexOf(pressItem);
        gapWeight = gapTarget = 1;
    }

    private void UpdateDrag()
    {
        var (a, c) = AC(mouse);
        // An unpinned running app cannot be removed (it leaves by itself when it closes): dragged out, it
        // simply returns to its place, without the removal animation.
        removeIntent = !pressItem.Transient
                       && (c > Thickness + Math.Max(60, Zoom - Base + 40) || c < -40 || a < -40 || a > L + 40);
        gapTarget = removeIntent ? 0 : 1;
        if (!removeIntent) gapIndex = InsertionIndex(a);
    }

    private void FinishDrag(DockItem item)
    {
        if (item == null) return;
        if (removeIntent)
        {
            poofs.Add((mouse, clock.Elapsed.TotalSeconds));
            host.RemoveItem(item);
        }
        else host.MoveItem(item, gapIndex);
        removeIntent = false;
        gapIndex = -1; gapWeight = 0; gapTarget = 0;
    }

    // ---------------- File drop (add apps) ----------------
    protected override void OnDragEnter(DragEventArgs e) => HandleDragOver(e);
    protected override void OnDragOver(DragEventArgs e) => HandleDragOver(e);

    private void HandleDragOver(DragEventArgs e)
    {
        e.Handled = true;
        if (Cfg.LockItems || !e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effects = DragDropEffects.None; return; }
        externalDrag = true;
        mouse = e.GetPosition(this);
        if (!gapIsExternal || gapIndex < 0) { gapIsExternal = true; gapWeight = 0; }
        gapIndex = InsertionIndex(AC(mouse).a);
        gapTarget = 1;
        e.Effects = e.AllowedEffects.HasFlag(DragDropEffects.Link) ? DragDropEffects.Link
                  : e.AllowedEffects.HasFlag(DragDropEffects.Copy) ? DragDropEffects.Copy : DragDropEffects.None;
        Kick();
    }

    protected override void OnDragLeave(DragEventArgs e)
    {
        externalDrag = false;
        gapTarget = 0;
        Kick();
    }

    protected override void OnDrop(DragEventArgs e)
    {
        e.Handled = true;
        int idx = gapIndex;
        externalDrag = false; gapTarget = 0; gapWeight = 0; gapIndex = -1; gapIsExternal = false;
        if (!Cfg.LockItems && e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            host.AddPaths(files, idx < 0 ? host.Items.Count : idx);
        Kick();
    }
}
