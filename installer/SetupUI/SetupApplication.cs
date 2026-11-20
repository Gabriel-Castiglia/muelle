//
//  SetupApplication.cs
//  Muelle setup: the bootstrapper application that drives the WiX engine.
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
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Microsoft.Win32;
using WixToolset.BootstrapperApplicationApi;

namespace Muelle.Setup;

/// <summary>What the setup can do, as offered on its first page.</summary>
internal enum SetupState { Install, Upgrade, Maintenance, Uninstall, Newer }

/// <summary>
/// Bootstrapper application: talks to the WiX engine (detect, plan, apply) and drives the setup window.
/// The engine calls back on its own thread; everything that touches the window goes through the dispatcher.
/// Without a full user interface (/quiet, /passive, or an upgrade removing this version) it works on its own.
/// Muelle's own updates (Updates.cs in the app) start it with "/quiet /norestart /muellewaitpid=&lt;pid&gt;
/// /muellerelaunch=1": it waits for that Muelle to be gone before anything else, and at the end starts the installed
/// Muelle again ("--updated", or "--update-failed=&lt;code&gt;" if nothing was installed).
/// </summary>
internal sealed class SetupApplication : BootstrapperApplication
{
    /// <summary>Id of the MSI package in the bundle (Bundle.wxs).</summary>
    private const string PackageId = "MuelleMsi";
    /// <summary>Windows Installer error 1602: the user canceled.</summary>
    private const int UserCanceled = unchecked((int)0x80070642);

    public IBootstrapperCommand Command { get; private set; }
    public IEngine Engine => engine;
    public string Version { get; private set; }
    /// <summary>Version of an older Muelle that this setup will replace, if any.</summary>
    public string OlderVersion { get; private set; }
    /// <summary>Version of a newer Muelle already installed, if any.</summary>
    public string NewerVersion { get; private set; }
    public bool Installed { get; private set; }
    public LaunchAction Planned { get; private set; }
    public string LastError { get; private set; }
    public bool Canceled { get; set; }

    private Dispatcher dispatcher;
    private SetupWindow window;
    private IntPtr windowHandle;
    private int exitCode;
    /// <summary>Process id of the Muelle that started this update (/muellewaitpid=); 0 = none.</summary>
    private int waitPid;
    /// <summary>/muellerelaunch=1: start Muelle again when done.</summary>
    private bool relaunch, relaunched;

    protected override void OnCreate(CreateEventArgs args)
    {
        base.OnCreate(args);
        Command = args.Command;
    }

    protected override void Run()
    {
        dispatcher = Dispatcher.CurrentDispatcher;
        Version = engine.GetVariableVersion("WixBundleVersion");
        Loc.Current = InitialLanguage();
        ReadUpdateSwitches();
        WaitForMuelle();

        DetectRelatedBundle += OnDetectRelatedBundle;
        DetectPackageComplete += OnDetectPackageComplete;
        DetectComplete += OnDetectComplete;
        PlanComplete += OnPlanComplete;
        Progress += (s, e) => { Report(e.OverallPercentage); e.Cancel = Canceled; };
        ExecuteProgress += (s, e) => { Report(e.OverallPercentage); e.Cancel = Canceled; };
        Error += OnError;
        ApplyComplete += OnApplyComplete;

        // The window always exists, because the engine needs its handle to apply; it is shown only with a
        // full or passive interface.
        window = new SetupWindow(this);
        window.Closed += (s, e) => dispatcher.InvokeShutdown();
        windowHandle = new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
        if (HasWindow) window.Show();
        engine.Detect();
        Dispatcher.Run();
        engine.Quit(exitCode);
    }

    /// <summary>Full or passive display: a window is shown (passive shows progress only).</summary>
    private bool HasWindow => Command.Display == Display.Full || Command.Display == Display.Passive;
    private bool Interactive => Command.Display == Display.Full;

    /// <summary>
    /// The language of an installed Muelle when there is one (uninstall, repair), otherwise the language of
    /// Windows if it is Spanish, otherwise English. The user can change it in the window at any time.
    /// </summary>
    private static UiLanguage InitialLanguage()
    {
        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"Software\Muelle");
            if (key?.GetValue("Language") is string code && code.Length > 0) return Loc.FromCode(code);
        }
        catch { }
        return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? UiLanguage.Es : UiLanguage.En;
    }

    // ---------------- Muelle's own updates ----------------
    /// <summary>Reads /muellewaitpid=&lt;pid&gt; and /muellerelaunch=1 (also with "-" instead of "/").</summary>
    private void ReadUpdateSwitches()
    {
        var args = new List<string>();
        try { args.AddRange(Command.ParseCommandLine().UnknownCommandLineArgs ?? Array.Empty<string>()); } catch { }
        if (args.Count == 0 && !string.IsNullOrEmpty(Command.CommandLine)) args.AddRange(Command.CommandLine.Split(' '));
        foreach (var raw in args)
        {
            string a = raw.Trim().Trim('"').TrimStart('/', '-').ToLowerInvariant();
            if (a.StartsWith("muellewaitpid=")) int.TryParse(a.Substring("muellewaitpid=".Length), out waitPid);
            else if (a == "muellerelaunch=1") relaunch = true;
        }
        if (waitPid > 0 || relaunch) engine.Log(LogLevel.Standard, $"Muelle setup: update started by Muelle (pid {waitPid}, relaunch {relaunch}).");
    }

    /// <summary>Waits (up to a minute) for the Muelle that started the update to finish closing.</summary>
    private void WaitForMuelle()
    {
        if (waitPid <= 0) return;
        try
        {
            using var p = Process.GetProcessById(waitPid);
            if (!p.WaitForExit(60000)) engine.Log(LogLevel.Standard, "Muelle setup: Muelle did not close in 60 s; going on.");
        }
        catch (ArgumentException) { } // already gone
        catch (Exception ex) { engine.Log(LogLevel.Standard, "Muelle setup: could not wait for Muelle: " + ex.Message); }
    }

    /// <summary>Starts the installed Muelle again after an update it asked for, saying how it went.</summary>
    private void RelaunchMuelle(int status)
    {
        if (!relaunch || relaunched) return;
        relaunched = true;
        try
        {
            string exe = Path.Combine(InstallFolder, "Muelle.exe");
            if (!File.Exists(exe)) { engine.Log(LogLevel.Standard, "Muelle setup: no Muelle.exe to start again."); return; }
            string arg = status >= 0 ? "--updated" : "--update-failed=0x" + status.ToString("X8");
            Process.Start(new ProcessStartInfo(exe, arg) { UseShellExecute = false, WorkingDirectory = InstallFolder });
            engine.Log(LogLevel.Standard, "Muelle setup: started Muelle again (" + arg + ").");
        }
        catch (Exception ex) { engine.Log(LogLevel.Standard, "Muelle setup: could not start Muelle again: " + ex.Message); }
    }

    // ---------------- Detect ----------------
    private void OnDetectRelatedBundle(object sender, DetectRelatedBundleEventArgs e)
    {
        if (e.RelationType != RelationType.Upgrade) return;
        int cmp = engine.CompareVersions(e.Version, Version);
        if (cmp > 0) NewerVersion = e.Version;
        else if (cmp < 0) OlderVersion = e.Version;
    }

    private void OnDetectPackageComplete(object sender, DetectPackageCompleteEventArgs e)
    {
        if (e.PackageId == PackageId && e.State == PackageState.Present) Installed = true;
    }

    private void OnDetectComplete(object sender, DetectCompleteEventArgs e)
    {
        if (e.Status < 0)
        {
            Finish(e.Status);
            return;
        }
        // Without a full interface the requested action goes ahead at once (e.g. an upgrade removing this version).
        if (!Interactive) { Plan(Command.Action); return; }
        SetupState state =
            Command.Action == LaunchAction.Uninstall ? SetupState.Uninstall
            : NewerVersion != null ? SetupState.Newer
            : Installed ? SetupState.Maintenance
            : OlderVersion != null ? SetupState.Upgrade
            : SetupState.Install;
        dispatcher.BeginInvoke(new Action(() => window.ShowStart(state)));
    }

    // ---------------- Plan and apply ----------------
    /// <summary>Starts an install, repair or uninstall; the chosen language goes to the MSI (see Package.wxs).</summary>
    public void Plan(LaunchAction action)
    {
        Planned = action;
        engine.SetVariableString("MuelleLanguage", Loc.Code(Loc.Current), false);
        if (HasWindow) dispatcher.BeginInvoke(new Action(() => window.ShowProgress(action)));
        // Closing Muelle can take a few seconds: it is done off the window's thread.
        System.Threading.Tasks.Task.Run(() =>
        {
            CloseRunningMuelle();
            try { engine.Plan(action); }
            catch (Exception ex)
            {
                engine.Log(LogLevel.Error, "Muelle setup: plan could not start: " + ex.Message);
                LastError = ex.Message;
                Finish(ex.HResult < 0 ? ex.HResult : unchecked((int)0x80004005));
            }
        });
    }

    // ---------------- Closing a running Muelle ----------------
    private const uint WM_CLOSE = 0x0010;
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// Closes the Muelle of this user that runs from the install folder, before its files are replaced or
    /// removed. The MSI also closes it (util:CloseApplication), but only after removing files, which on an
    /// uninstall would leave the executable in use until a restart. Muelle quits when one of its windows
    /// is asked to close; a version that does not do so is ended after a few seconds.
    /// </summary>
    private void CloseRunningMuelle()
    {
        string folder;
        try { folder = Path.GetFullPath(InstallFolder).TrimEnd('\\') + "\\"; } catch { return; }
        foreach (var p in Process.GetProcessesByName("Muelle"))
        {
            using (p)
            {
                try
                {
                    string exe = p.MainModule?.FileName;
                    if (exe == null || !exe.StartsWith(folder, StringComparison.OrdinalIgnoreCase)) continue;
                    EnumWindows((h, l) =>
                    {
                        GetWindowThreadProcessId(h, out uint pid);
                        if (pid == (uint)p.Id) PostMessage(h, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                        return true;
                    }, IntPtr.Zero);
                    if (!p.WaitForExit(8000))
                    {
                        engine.Log(LogLevel.Standard, "Muelle setup: Muelle did not close in 8 s; ending it.");
                        p.Kill();
                        p.WaitForExit(3000);
                    }
                    else engine.Log(LogLevel.Standard, "Muelle setup: closed the running Muelle.");
                }
                catch (Exception ex) { engine.Log(LogLevel.Standard, "Muelle setup: could not close Muelle: " + ex.Message); }
            }
        }
    }

    private void OnPlanComplete(object sender, PlanCompleteEventArgs e)
    {
        if (e.Status < 0) { Finish(e.Status); return; }
        try { engine.Apply(windowHandle); }
        catch (Exception ex)
        {
            // Without this, a failed call would leave the setup waiting forever.
            engine.Log(LogLevel.Error, "Muelle setup: apply could not start: " + ex.Message);
            LastError = ex.Message;
            Finish(ex.HResult < 0 ? ex.HResult : unchecked((int)0x80004005));
        }
    }

    private void Report(int percent)
    {
        if (HasWindow) dispatcher.BeginInvoke(new Action(() => window.SetProgress(percent)));
    }

    private void OnError(object sender, WixToolset.BootstrapperApplicationApi.ErrorEventArgs e)
    {
        // The message is shown on the final page; the engine's recommended answer is kept.
        if (e.ErrorCode != 1602 && !string.IsNullOrWhiteSpace(e.ErrorMessage)) LastError = e.ErrorMessage.Trim();
        engine.Log(LogLevel.Standard, "Muelle setup: error " + e.ErrorCode + ": " + e.ErrorMessage);
    }

    private void OnApplyComplete(object sender, ApplyCompleteEventArgs e)
    {
        Finish(e.Status, e.Restart == ApplyRestart.RestartRequired);
    }

    /// <summary>Ends the operation: shows the result page or, without a full interface, closes.</summary>
    private void Finish(int status, bool restart = false)
    {
        exitCode = status;
        bool canceled = Canceled || status == UserCanceled;
        RelaunchMuelle(status);
        if (!Interactive)
        {
            dispatcher.BeginInvoke(new Action(() => dispatcher.InvokeShutdown()));
            return;
        }
        dispatcher.BeginInvoke(new Action(() => window.ShowResult(status >= 0, canceled, restart)));
    }

    /// <summary>Called when the user closes the window before anything was done.</summary>
    public void CancelBeforeStart() => exitCode = UserCanceled;

    /// <summary>Path of the setup log, for the "View log" button.</summary>
    public string LogPath => engine.ContainsVariable("WixBundleLog") ? engine.GetVariableString("WixBundleLog") : null;

    /// <summary>Where Muelle is (or will be) installed.</summary>
    public string InstallFolder => engine.FormatString(@"[ProgramFiles64Folder]Muelle");
}
