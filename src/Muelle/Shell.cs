//
//  Shell.cs
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

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Muelle;

/// <summary>High-resolution icons and shortcut (.lnk) target resolution.</summary>
internal static class Shell
{
    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE { public int cx, cy; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr pbc, ref Guid riid, out IntPtr ppv);

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP { public int bmType, bmWidth, bmHeight, bmWidthBytes; public ushort bmPlanes, bmBitsPixel; public IntPtr bmBits; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public int biSize, biWidth, biHeight;
        public short biPlanes, biBitCount;
        public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        public uint c0, c1, c2, c3; // room for color masks
    }

    [DllImport("gdi32.dll")] private static extern int GetObject(IntPtr h, int size, ref BITMAP bm);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr h);
    [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint lines, [Out] byte[] bits, ref BITMAPINFO bmi, uint usage);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    /// <summary>Icon of a file/program/shortcut, with transparency, at the requested size.</summary>
    public static ImageSource LoadIcon(string path, int px)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        IntPtr unk = IntPtr.Zero, hbm = IntPtr.Zero;
        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            if (SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out unk) != 0 || unk == IntPtr.Zero) return null;
            var factory = (IShellItemImageFactory)Marshal.GetObjectForIUnknown(unk);
            // SIIGBF_BIGGERSIZEOK | SIIGBF_ICONONLY
            if (factory.GetImage(new SIZE { cx = px, cy = px }, 0x1 | 0x4, out hbm) != 0 || hbm == IntPtr.Zero) return null;
            return FromHBitmap(hbm);
        }
        catch { return null; }
        finally
        {
            if (hbm != IntPtr.Zero) DeleteObject(hbm);
            if (unk != IntPtr.Zero) Marshal.Release(unk);
        }
    }

    private static BitmapSource FromHBitmap(IntPtr hbm)
    {
        var bm = new BITMAP();
        if (GetObject(hbm, Marshal.SizeOf<BITMAP>(), ref bm) == 0) return null;
        int w = bm.bmWidth, h = Math.Abs(bm.bmHeight);
        if (w <= 0 || h <= 0) return null;
        var bmi = new BITMAPINFO { biSize = 40, biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32 };
        var px = new byte[w * h * 4];
        IntPtr dc = GetDC(IntPtr.Zero);
        try { if (GetDIBits(dc, hbm, 0, (uint)h, px, ref bmi, 0) == 0) return null; }
        finally { ReleaseDC(IntPtr.Zero, dc); }

        bool hasAlpha = false;
        for (int i = 3; i < px.Length; i += 4) if (px[i] != 0) { hasAlpha = true; break; }
        if (!hasAlpha) for (int i = 3; i < px.Length; i += 4) px[i] = 255;

        var src = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, px, w * 4);
        src.Freeze();
        return src;
    }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        [PreserveSig] int BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int GetParent(out IntPtr ppsi);
        [PreserveSig] int GetDisplayName(uint sigdn, out IntPtr name);
    }

    /// <summary>Display name of a shell item (e.g., "Microsoft Teams" for shell:AppsFolder\…).</summary>
    public static string DisplayName(string parsingName)
    {
        IntPtr unk = IntPtr.Zero, name = IntPtr.Zero;
        try
        {
            var iid = typeof(IShellItem).GUID;
            if (SHCreateItemFromParsingName(parsingName, IntPtr.Zero, ref iid, out unk) != 0 || unk == IntPtr.Zero) return null;
            var item = (IShellItem)Marshal.GetObjectForIUnknown(unk);
            if (item.GetDisplayName(0 /* SIGDN_NORMALDISPLAY */, out name) != 0) return null;
            return Marshal.PtrToStringUni(name);
        }
        catch { return null; }
        finally
        {
            if (name != IntPtr.Zero) Marshal.FreeCoTaskMem(name);
            if (unk != IntPtr.Zero) Marshal.Release(unk);
        }
    }

    // ---- Icons of programs without a large version ----
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr LoadLibraryEx(string file, IntPtr h, uint flags);
    [DllImport("kernel32.dll")] private static extern bool FreeLibrary(IntPtr h);
    private delegate bool EnumResNameProc(IntPtr module, IntPtr type, IntPtr name, IntPtr param);
    [DllImport("kernel32.dll")] private static extern bool EnumResourceNames(IntPtr module, IntPtr type, EnumResNameProc proc, IntPtr param);
    [DllImport("kernel32.dll")] private static extern IntPtr FindResource(IntPtr module, IntPtr name, IntPtr type);
    [DllImport("kernel32.dll")] private static extern IntPtr LoadResource(IntPtr module, IntPtr res);
    [DllImport("kernel32.dll")] private static extern IntPtr LockResource(IntPtr data);
    [DllImport("kernel32.dll")] private static extern uint SizeofResource(IntPtr module, IntPtr res);
    [DllImport("user32.dll")] private static extern IntPtr CreateIconFromResourceEx(IntPtr bits, uint size, bool icon, uint ver, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr h);

    /// <summary>
    /// Largest icon embedded in the executable, at its original size, only if it is smaller than 256 px.
    /// For such programs Windows returns the small icon inside a frame, which looks tiny in the dock;
    /// the original icon, scaled by the dock in high quality, looks much better.
    /// Returns null if the file has a large icon or no icons at all.
    /// </summary>
    public static ImageSource LoadSmallNativeIcon(string exe)
    {
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe)) return null;
        // Mapped only as a resource file: no code of the program is loaded or run.
        IntPtr mod = LoadLibraryEx(exe, IntPtr.Zero, 0x2 | 0x20); // LOAD_LIBRARY_AS_DATAFILE | LOAD_LIBRARY_AS_IMAGE_RESOURCE
        if (mod == IntPtr.Zero) return null;
        try
        {
            // First icon group (the one Explorer uses for index 0).
            IntPtr group = IntPtr.Zero;
            EnumResourceNames(mod, (IntPtr)14, (m, t, n, p) => { group = n; return false; }, IntPtr.Zero);
            if (group == IntPtr.Zero) return null;
            IntPtr dir = LockResource(LoadResource(mod, FindResource(mod, group, (IntPtr)14)));
            if (dir == IntPtr.Zero) return null;

            int count = Marshal.ReadInt16(dir, 4), best = -1, bestSize = 0, bestBits = 0;
            for (int i = 0; i < count; i++)
            {
                IntPtr e = dir + 6 + i * 14;
                int size = Marshal.ReadByte(e) == 0 ? 256 : Marshal.ReadByte(e);
                int bits = Marshal.ReadInt16(e, 6);
                if (size > bestSize || (size == bestSize && bits > bestBits)) { best = i; bestSize = size; bestBits = bits; }
            }
            if (best < 0 || bestSize >= 256) return null;

            int id = (ushort)Marshal.ReadInt16(dir + 6 + best * 14, 12);
            IntPtr res = FindResource(mod, (IntPtr)id, (IntPtr)3); // RT_ICON
            if (res == IntPtr.Zero) return null;
            IntPtr hIcon = CreateIconFromResourceEx(LockResource(LoadResource(mod, res)), SizeofResource(mod, res), true, 0x30000, bestSize, bestSize, 0);
            if (hIcon == IntPtr.Zero) return null;
            try
            {
                var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(hIcon, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                return src;
            }
            finally { DestroyIcon(hIcon); }
        }
        catch { return null; }
        finally { FreeLibrary(mod); }
    }

    /// <summary>
    /// Installed executable of a ClickOnce application (.appref-ms), looked up in the
    /// %LOCALAPPDATA%\Apps\2.0 cache by manifest name; the most recent one if there are several.
    /// </summary>
    private static string ResolveClickOnce(string appref)
    {
        try
        {
            string text = File.ReadAllText(appref);
            int end = text.IndexOf(".application", StringComparison.OrdinalIgnoreCase);
            if (end < 0) return null;
            int start = text.LastIndexOfAny(new[] { '/', '\\', '#' }, end) + 1;
            string name = text.Substring(start, end - start) + ".exe";
            string cache = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Apps", "2.0");
            if (!Directory.Exists(cache)) return null;
            return new DirectoryInfo(cache).EnumerateFiles(name, SearchOption.AllDirectories)
                .OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault()?.FullName;
        }
        catch { return null; }
    }

    // ---- Shortcuts ----
    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLinkCoClass { }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int cch, IntPtr findData, uint flags);
    }

    /// <summary>Actual program a .lnk points to (or the file itself if it is not a shortcut).</summary>
    public static string ResolveTarget(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        // Store apps: identified by their AppUserModelID (shell:AppsFolder\<AUMID>).
        const string appsFolder = "shell:AppsFolder\\";
        if (path.StartsWith(appsFolder, StringComparison.OrdinalIgnoreCase)) return WindowTracker.AumidPrefix + path.Substring(appsFolder.Length);
        if (path.EndsWith(".appref-ms", StringComparison.OrdinalIgnoreCase)) return ResolveClickOnce(path) ?? path;
        if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return path;
        object link = null;
        try
        {
            link = new ShellLinkCoClass();
            ((IPersistFile)link).Load(path, 0);
            var sb = new StringBuilder(1024);
            ((IShellLinkW)link).GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
            var target = Environment.ExpandEnvironmentVariables(sb.ToString());
            if (!string.IsNullOrWhiteSpace(target)) return target;
            // Links to shell objects (e.g., Control Panel) have no file target: match them by the
            // AppUserModelID stored in the link, which their windows also carry.
            string aumid = WindowTracker.LinkAumid(link);
            return aumid == null ? null : WindowTracker.AumidPrefix + aumid;
        }
        catch { return null; }
        finally { if (link != null) Marshal.ReleaseComObject(link); }
    }
}

/// <summary>A monitor with its area in physical pixels and its DPI scale.</summary>
internal sealed class MonitorInfo
{
    public IntPtr Handle;
    public Native.RECT Bounds, Work;
    public string Device;
    public bool Primary;
    public uint Dpi = 96;
    public double Scale => Dpi / 96.0;
}

internal static class Monitors
{
    /// <summary>All monitors, ordered left to right.</summary>
    public static List<MonitorInfo> All()
    {
        var list = new List<MonitorInfo>();
        Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr hdc, ref Native.RECT r, IntPtr d) =>
        {
            var mi = new Native.MONITORINFOEX { cbSize = Marshal.SizeOf<Native.MONITORINFOEX>() };
            if (Native.GetMonitorInfo(h, ref mi))
            {
                uint dpi = 96;
                try { if (Native.GetDpiForMonitor(h, 0, out uint dx, out _) == 0 && dx > 0) dpi = dx; } catch { }
                list.Add(new MonitorInfo
                {
                    Handle = h, Bounds = mi.rcMonitor, Work = mi.rcWork, Device = mi.szDevice,
                    Primary = (mi.dwFlags & 1) != 0, Dpi = dpi,
                });
            }
            return true;
        }, IntPtr.Zero);
        list.Sort((a, b) => a.Bounds.Left != b.Bounds.Left ? a.Bounds.Left.CompareTo(b.Bounds.Left) : a.Bounds.Top.CompareTo(b.Bounds.Top));
        return list;
    }

    /// <summary>The chosen monitor ("" = the Windows primary display).</summary>
    public static MonitorInfo Pick(string device)
    {
        var all = All();
        MonitorInfo m = null;
        if (!string.IsNullOrEmpty(device))
            m = all.Find(x => string.Equals(x.Device, device, StringComparison.OrdinalIgnoreCase));
        return m ?? all.Find(x => x.Primary) ?? all.FirstOrDefault();
    }

    /// <summary>Rectangles of the Windows taskbars (primary and secondary).</summary>
    public static List<Native.RECT> Taskbars()
    {
        var list = new List<Native.RECT>();
        Native.EnumWindows((h, l) =>
        {
            var cls = Native.ClassOf(h);
            if ((cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd") && Native.IsWindowVisible(h)
                && Native.GetWindowRect(h, out var r))
                list.Add(r);
            return true;
        }, IntPtr.Zero);
        return list;
    }
}

/// <summary>Which programs have open windows (for the running indicator).</summary>
internal static class WindowTracker
{
    /// <summary>Prefix of the keys that identify an app by its AppUserModelID instead of by its path.</summary>
    public const string AumidPrefix = "aumid:";

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetApplicationUserModelId(IntPtr process, ref uint length, StringBuilder id);

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPERTYKEY { public Guid fmtid; public uint pid; }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT { public ushort vt, r1, r2, r3; public IntPtr p, p2; }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PROPERTYKEY key);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT value);
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PROPVARIANT pv);

    /// <summary>AppUserModelID explicitly assigned to the window (used by UWP apps and some desktop apps).</summary>
    private static string WindowAumid(IntPtr hwnd)
    {
        IPropertyStore store = null;
        try
        {
            var iid = typeof(IPropertyStore).GUID;
            if (SHGetPropertyStoreForWindow(hwnd, ref iid, out store) != 0 || store == null) return null;
            var key = new PROPERTYKEY { fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), pid = 5 };
            if (store.GetValue(ref key, out var pv) != 0) return null;
            try { return pv.vt == 31 /* VT_LPWSTR */ ? Marshal.PtrToStringUni(pv.p) : null; }
            finally { PropVariantClear(ref pv); }
        }
        catch { return null; }
        finally { if (store != null) Marshal.ReleaseComObject(store); }
    }

    /// <summary>AppUserModelID stored in a shell link (System.AppUserModel.ID), or null.</summary>
    public static string LinkAumid(object link)
    {
        try
        {
            if (link is not IPropertyStore store) return null;
            var key = new PROPERTYKEY { fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), pid = 5 };
            if (store.GetValue(ref key, out var pv) != 0) return null;
            try { return pv.vt == 31 /* VT_LPWSTR */ ? Marshal.PtrToStringUni(pv.p) : null; }
            finally { PropVariantClear(ref pv); }
        }
        catch { return null; }
    }

    /// <summary>AppUserModelID of the process if it belongs to a package (Store / MSIX apps).</summary>
    private static string ProcessAumid(uint pid)
    {
        IntPtr h = Native.OpenProcess(0x1000 /* QUERY_LIMITED_INFORMATION */, false, pid);
        if (h == IntPtr.Zero) return null;
        try
        {
            uint len = 256;
            var sb = new StringBuilder((int)len);
            return GetApplicationUserModelId(h, ref len, sb) == 0 ? sb.ToString() : null;
        }
        finally { Native.CloseHandle(h); }
    }

    /// <summary>Comparable form of an item key: an "aumid:…" key as is, a file path made absolute.</summary>
    public static string Normalize(string key)
    {
        if (key.StartsWith(AumidPrefix, StringComparison.Ordinal)) return key;
        try { return System.IO.Path.GetFullPath(key); } catch { return key; }
    }

    /// <summary>An open window: how to launch its app (path or "aumid:…"), its executable, and its AUMID if it has one.</summary>
    public sealed record OpenApp(string Launch, string Path, string Aumid);

    /// <summary>Windows found by the last <see cref="Snapshot"/>, in Z order.</summary>
    public static List<OpenApp> LastApps { get; private set; } = new();

    /// <summary>
    /// Processes to terminate for "End task": the owners of the app's windows and, if it is a
    /// Store app, all processes of its package. Never includes Explorer or the UWP app host
    /// (ApplicationFrameHost), which are shared.
    /// </summary>
    public static List<uint> ProcessesFor(string key, IEnumerable<IntPtr> windows)
    {
        var pids = new HashSet<uint>();
        foreach (var h in windows)
        {
            Native.GetWindowThreadProcessId(h, out uint pid);
            if (pid != 0) pids.Add(pid);
        }
        if (key.StartsWith(AumidPrefix, StringComparison.Ordinal))
        {
            string aumid = key.Substring(AumidPrefix.Length);
            foreach (var p in System.Diagnostics.Process.GetProcesses())
            {
                using (p)
                    if (string.Equals(ProcessAumid((uint)p.Id), aumid, StringComparison.OrdinalIgnoreCase)) pids.Add((uint)p.Id);
            }
        }
        uint self = (uint)Environment.ProcessId;
        return pids.Where(pid =>
        {
            if (pid == self) return false;
            string name = System.IO.Path.GetFileName(Native.ProcessImagePath(pid) ?? "");
            return !name.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase)
                && !name.Equals("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase);
        }).ToList();
    }

    /// <summary>Keys a window can match an icon by: its executable and its AUMID(s).</summary>
    public static List<string> KeysFor(IntPtr hwnd)
    {
        var keys = new List<string>();
        Native.GetWindowThreadProcessId(hwnd, out uint pid);
        string path = Native.ProcessImagePath(pid);
        if (path != null) keys.Add(path);
        string a = WindowAumid(hwnd);
        if (a != null) keys.Add(AumidPrefix + a);
        a = ProcessAumid(pid);
        if (a != null) keys.Add(AumidPrefix + a);
        return keys;
    }

    public static Dictionary<string, List<IntPtr>> Snapshot()
    {
        var map = new Dictionary<string, List<IntPtr>>(StringComparer.OrdinalIgnoreCase);
        var cache = new Dictionary<uint, string>();
        var aumids = new Dictionary<uint, string>();
        var apps = new List<OpenApp>();
        uint self = (uint)Environment.ProcessId;
        Native.EnumWindows((h, l) =>
        {
            if (!Native.IsWindowVisible(h)) return true;
            if (Native.GetWindow(h, 4 /* GW_OWNER */) != IntPtr.Zero) return true;
            long ex = Native.GetWindowLongPtr(h, Native.GWL_EXSTYLE).ToInt64();
            if ((ex & Native.WS_EX_TOOLWINDOW) != 0) return true;
            if (Native.GetWindowTextLength(h) == 0) return true;
            if (Native.DwmGetWindowAttribute(h, 14 /* DWMWA_CLOAKED */, out int cloaked, 4) == 0 && cloaked != 0) return true;
            var cls = Native.ClassOf(h);
            if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return true;
            Native.GetWindowThreadProcessId(h, out uint pid);
            if (pid == self) return true;
            if (!cache.TryGetValue(pid, out var path)) cache[pid] = path = Native.ProcessImagePath(pid);
            if (path == null) return true;
            if (!map.TryGetValue(path, out var wins)) map[path] = wins = new List<IntPtr>();
            wins.Add(h);
            // Store apps are identified by their AppUserModelID (e.g., Teams or Kate).
            if (!aumids.TryGetValue(pid, out var procAumid)) aumids[pid] = procAumid = ProcessAumid(pid);
            string winAumid = WindowAumid(h);
            string aumid = winAumid ?? procAumid;
            if (aumid != null)
            {
                string key = AumidPrefix + aumid;
                if (!map.TryGetValue(key, out var awins)) map[key] = awins = new List<IntPtr>();
                awins.Add(h);
            }
            // Identity the app would be launched with: Store package, UWP frame, or executable.
            bool frameHost = path.EndsWith(@"\ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase);
            string launch = procAumid != null ? AumidPrefix + procAumid
                          : frameHost ? (winAumid != null ? AumidPrefix + winAumid : null)
                          : path;
            if (launch != null) apps.Add(new OpenApp(launch, path, aumid == null ? null : AumidPrefix + aumid));
            return true;
        }, IntPtr.Zero);
        LastApps = apps;
        return map;
    }
}
