//
//  Updates.cs
//  Muelle: a dock for Windows.
//
//  Copyright (C) 2026 Gabriel Castiglia. Licensed under the GNU GPL, version 2 only.
//  Date: November 11, 2026 (updates that install themselves: November 20, 2026)
//  SPDX-License-Identifier: GPL-2.0-only
//
//  This program is free software: you can redistribute it and/or modify it under the terms of
//  the GNU General Public License, version 2, as published by the Free Software Foundation.
//  It is distributed WITHOUT ANY WARRANTY. See the LICENSE file for details.
//

using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using static Muelle.Loc;

namespace Muelle;

/// <summary>A release on GitHub: its version, its page, and where its setup and its hashes are (null if it lacks them).</summary>
internal sealed class Release
{
    public string Tag = "";
    public string Page = Updates.Page;
    public string SetupUrl, SumsUrl;
    public bool CanInstall => SetupUrl != null && SumsUrl != null;
}

/// <summary>
/// Updates that install themselves. Once a day (if the option is on) and on request (dock menu, About Muelle…),
/// Muelle asks GitHub for its latest release. When it is newer, one question: update now, see what is new, or later.
/// Then everything happens alone: the setup of the release is downloaded and checked against the release's
/// SHA256SUMS.txt (a file that does not match is deleted and never run), Muelle notes how it was started and quits,
/// and the setup installs in silence, keeping the install options (it reads the language from the installed copy),
/// waits for this Muelle to be gone, closes any other copy that runs from the install folder, and starts Muelle again
/// with the same arguments. If something fails, Muelle keeps going (or comes back) and says what happened.
/// Nothing is sent but the requests themselves, with "Muelle/&lt;version&gt;" as the program name.
/// </summary>
internal static class Updates
{
    private const string Api = "https://api.github.com/repos/Gabriel-Castiglia/muelle/releases/latest";
    public const string Page = AboutWindow.Repository + "/releases/latest";
    /// <summary>The asset that is downloaded and run, and the list of hashes it is checked against.</summary>
    public const string SetupName = "muelle-setup.exe", SumsName = "SHA256SUMS.txt";

    /// <summary>Where the setup is downloaded and how Muelle was started is noted (%TEMP%\MuelleUpdate).</summary>
    public static string UpdateDir => Path.Combine(Path.GetTempPath(), "MuelleUpdate");
    private static string RelaunchFile => Path.Combine(UpdateDir, "relaunch.json");

    /// <summary>The running version, as major.minor.patch.</summary>
    public static Version Current
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        }
    }

    private static bool busy;

    // ---------------- Pure functions (the same in every app of the author) ----------------
    internal static bool IsNewer(string tag, string mine) =>
        System.Version.TryParse(tag.TrimStart('v', 'V'), out var a) && System.Version.TryParse(mine, out var b) && a > b;

    /// <summary>The hash of one file in a SHA256SUMS.txt ("hash  name" or "hash *name" per line), or null.</summary>
    internal static string HashFor(string sums, string fileName)
    {
        foreach (var raw in sums.Split('\n'))
        {
            var line = raw.Trim();
            int sp = line.IndexOfAny(new[] { ' ', '\t' });
            if (sp != 64) continue;
            var name = line[sp..].Trim().TrimStart('*');
            if (string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase)) return line[..64].ToLowerInvariant();
        }
        return null;
    }

    /// <summary>The release described by the JSON of GitHub's "latest release" answer.</summary>
    internal static Release Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var r = new Release { Tag = (root.GetProperty("tag_name").GetString() ?? "").TrimStart('v', 'V') };
        if (root.TryGetProperty("html_url", out var hu) && hu.GetString() is string page) r.Page = page;
        if (root.TryGetProperty("assets", out var assets))
            foreach (var a in assets.EnumerateArray())
            {
                var name = a.GetProperty("name").GetString() ?? "";
                var url = a.GetProperty("browser_download_url").GetString();
                if (name.Equals(SetupName, StringComparison.OrdinalIgnoreCase)) r.SetupUrl = url;
                else if (name.Equals(SumsName, StringComparison.OrdinalIgnoreCase)) r.SumsUrl = url;
            }
        return r;
    }

    // ---------------- Checking ----------------
    private static HttpClient NewHttp(TimeSpan timeout)
    {
        var http = new HttpClient { Timeout = timeout };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Muelle/" + Current);
        return http;
    }

    /// <summary>The latest release; throws if GitHub cannot be reached.</summary>
    public static async Task<Release> LatestAsync()
    {
        using var http = NewHttp(TimeSpan.FromSeconds(15));
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return Parse(await http.GetStringAsync(Api));
    }

    /// <summary>
    /// Asks GitHub and, if there is a newer version, asks the user what to do. Returns the text for a status line
    /// (About Muelle…). <paramref name="interactive"/>: on request, so "up to date" and errors are said too.
    /// </summary>
    public static async Task<string> CheckAsync(DockManager manager, bool interactive)
    {
        if (busy) return tr("Ya se está actualizando.", "An update is already under way.");
        try
        {
            var latest = await LatestAsync();
            if (!IsNewer(latest.Tag, Current.ToString()))
            {
                string same = tr($"Tenés la última versión ({Current}).", $"You have the latest version ({Current}).");
                if (interactive) MessageBox.Show(same, "Muelle", MessageBoxButton.OK, MessageBoxImage.Information);
                return same;
            }
            UpdateWindow.Ask(manager, latest);
            return tr($"Está la versión {latest.Tag}.", $"Version {latest.Tag} is out.");
        }
        catch (Exception ex)
        {
            Program.Log("Could not check for a new version: " + ex.Message);
            string failed = tr("No se pudo consultar GitHub.", "GitHub could not be reached.");
            if (interactive) MessageBox.Show(failed + "\n\n" + ex.Message, "Muelle", MessageBoxButton.OK, MessageBoxImage.Warning);
            return failed;
        }
    }

    public static void OpenPage(string page = Page)
    {
        try { Process.Start(new ProcessStartInfo(page) { UseShellExecute = true }); } catch { }
    }

    /// <summary>
    /// The install folder of Muelle (HKLM\Software\Muelle\InstallFolder) when the running copy is the installed one;
    /// null for a copy run from elsewhere, which the setup would not replace.
    /// </summary>
    public static string InstalledFolder()
    {
        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"Software\Muelle");
            if (key?.GetValue("InstallFolder") is not string folder || folder.Length == 0) return null;
            string mine = Path.GetDirectoryName(Environment.ProcessPath) ?? "";
            return string.Equals(Path.GetFullPath(folder).TrimEnd('\\'), Path.GetFullPath(mine).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
                ? folder : null;
        }
        catch { return null; }
    }

    // ---------------- Installing ----------------
    /// <summary>
    /// Downloads and checks the setup, then hands over to it and quits. <paramref name="progress"/> receives texts to
    /// show while it works. Throws (with a message for the user) if anything fails before the setup starts.
    /// </summary>
    public static async Task InstallAsync(DockManager manager, Release release, Action<string> progress)
    {
        if (busy) return;
        busy = true;
        try
        {
            Directory.CreateDirectory(UpdateDir);
            var file = Path.Combine(UpdateDir, SetupName);
            using (var http = NewHttp(TimeSpan.FromMinutes(15)))
            {
                progress(tr("Comprobando la versión nueva…", "Checking the new version…"));
                var expected = HashFor(await http.GetStringAsync(release.SumsUrl), SetupName)
                               ?? throw new InvalidOperationException(tr($"El release no tiene el hash de {SetupName}.", $"The release has no hash for {SetupName}."));
                using (var resp = await http.GetAsync(release.SetupUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    resp.EnsureSuccessStatusCode();
                    long total = resp.Content.Headers.ContentLength ?? -1, done = 0;
                    await using var input = await resp.Content.ReadAsStreamAsync();
                    await using var output = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None);
                    var buf = new byte[81920];
                    int n, lastPct = -2;
                    while ((n = await input.ReadAsync(buf)) > 0)
                    {
                        await output.WriteAsync(buf.AsMemory(0, n));
                        done += n;
                        int pct = total > 0 ? (int)(done * 100 / total) : -1;
                        if (pct != lastPct)
                        {
                            lastPct = pct;
                            string amount = pct >= 0 ? pct + " %" : (done >> 20) + " MB";
                            progress(tr($"Descargando Muelle {release.Tag}… {amount}", $"Downloading Muelle {release.Tag}… {amount}"));
                        }
                    }
                }
                if (!await VerifyAsync(file, expected))
                    throw new InvalidOperationException(tr("El instalador descargado no coincide con su hash: se borró sin ejecutarlo.",
                                                           "The downloaded setup does not match its hash: it was deleted without running it."));
            }

            // How Muelle was started, so it comes back the same way; then the setup takes over.
            progress(tr("Instalando… Muelle se va a cerrar y volver a abrir.", "Installing… Muelle will close and open again."));
            WriteRelaunch(RelaunchFile, Environment.GetCommandLineArgs().Skip(1));
            var psi = new ProcessStartInfo(file) { UseShellExecute = true, WorkingDirectory = UpdateDir };
            foreach (var a in SetupArguments(Environment.ProcessId)) psi.ArgumentList.Add(a);
            Process.Start(psi);
            manager.Exit();
        }
        catch (Exception ex)
        {
            busy = false;
            Program.Log("The update could not be installed: " + ex.Message);
            throw;
        }
    }

    /// <summary>
    /// The silent command line of the setup: no window, no restart, wait for this process to end before touching
    /// any file, then start Muelle again (see installer\SetupUI\SetupApplication.cs).
    /// </summary>
    internal static string[] SetupArguments(int pid) => new[] { "/quiet", "/norestart", "/muellewaitpid=" + pid, "/muellerelaunch=1" };

    /// <summary>true if the file has the expected SHA-256; if it does not, it is deleted.</summary>
    internal static async Task<bool> VerifyAsync(string file, string expected)
    {
        string actual;
        await using (var fs = File.OpenRead(file)) actual = Convert.ToHexString(await SHA256.HashDataAsync(fs)).ToLowerInvariant();
        if (actual == expected) return true;
        try { File.Delete(file); } catch { }
        return false;
    }

    // ---------------- Coming back ----------------
    /// <summary>Notes the arguments Muelle was started with (without the ones the update itself adds).</summary>
    internal static void WriteRelaunch(string file, IEnumerable<string> args)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, JsonSerializer.Serialize(args.Where(a => !IsUpdateArg(a)).ToList()));
    }

    /// <summary>Reads (and deletes) the arguments noted before the update; empty if there are none.</summary>
    internal static List<string> ReadRelaunch(string file)
    {
        try
        {
            if (!File.Exists(file)) return new();
            var args = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(file)) ?? new();
            File.Delete(file);
            return args;
        }
        catch { return new(); }
    }

    private static bool IsUpdateArg(string a) => a == "--updated" || a.StartsWith("--update-failed", StringComparison.Ordinal);

    /// <summary>
    /// Started by the setup after an update ("--updated", or "--update-failed=&lt;code&gt;" if it did not install):
    /// the arguments Muelle had before, plus the ones given now. Anything else is returned as it is.
    /// </summary>
    public static string[] AfterUpdateArgs(string[] args, out bool updated, out string failure)
    {
        updated = args.Contains("--updated");
        failure = args.FirstOrDefault(a => a.StartsWith("--update-failed", StringComparison.Ordinal));
        if (!updated && failure == null) return args;
        busy = false;
        var before = ReadRelaunch(RelaunchFile);
        return before.Concat(args.Where(a => !IsUpdateArg(a) && !before.Contains(a))).ToArray();
    }

    /// <summary>The message for a setup that did not install, from the code it ended with.</summary>
    public static string FailureText(string failure)
    {
        string code = failure.Contains('=') ? failure[(failure.IndexOf('=') + 1)..] : "";
        // 0x800704C7: the administrator permission was refused; 0x80070642: the setup was canceled.
        if (code.Equals("0x800704C7", StringComparison.OrdinalIgnoreCase) || code.Equals("0x80070642", StringComparison.OrdinalIgnoreCase))
            return tr("La actualización se canceló (no se dio el permiso de administrador). Muelle sigue en la versión " + Current + ".",
                      "The update was canceled (the administrator permission was not given). Muelle stays at version " + Current + ".");
        return tr($"La actualización no se pudo instalar (código {code}). Muelle sigue en la versión {Current}; el detalle está en el registro de instalación, en %TEMP%.",
                  $"The update could not be installed (code {code}). Muelle stays at version {Current}; the details are in the setup log, in %TEMP%.");
    }
}

/// <summary>
/// The window that says a newer version is out, with the one question (update now, see what is new, later), and
/// that then shows the progress of the update.
/// </summary>
internal sealed class UpdateWindow : Window
{
    private static UpdateWindow open;
    private readonly DockManager manager;
    private readonly Release release;
    private TextBlock status;
    private StackPanel buttons;
    private bool working;

    /// <summary>Shows the question (only one at a time).</summary>
    public static void Ask(DockManager manager, Release release)
    {
        if (open != null) { open.Activate(); return; }
        open = new UpdateWindow(manager, release);
        open.Closed += (s, e) => open = null;
        open.Show();
        open.Activate();
    }

    /// <summary>After an update: a short note that Muelle now has the new version, or why it does not.</summary>
    public static void ShowResult(bool updated, string failure)
    {
        if (updated)
        {
            var w = new UpdateWindow(null, null) { done = true };
            w.Build();
            w.Show();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            timer.Tick += (s, e) => { timer.Stop(); w.Close(); };
            timer.Start();
        }
        else if (failure != null)
            MessageBox.Show(Updates.FailureText(failure), "Muelle", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private bool done;

    public UpdateWindow(DockManager manager, Release release)
    {
        this.manager = manager;
        this.release = release;
        Width = 460;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontSize = 13;
        Topmost = true;
        Theme.Attach(this);
        Build();
        Loc.Changed += Build;
        Closed += (s, e) => Loc.Changed -= Build;
    }

    private void Build()
    {
        if (working) return;
        Title = "Muelle";
        var root = new StackPanel { Margin = new Thickness(24, 20, 24, 18) };
        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        var icon = new Image { Width = 48, Height = 48, Margin = new Thickness(0, 0, 14, 0) };
        icon.Source = Theme.Icon(null, 256);
        RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
        header.Children.Add(icon);
        header.Children.Add(new TextBlock
        {
            Text = done ? tr($"Muelle se actualizó a la versión {Updates.Current}", $"Muelle was updated to version {Updates.Current}")
                        : tr("Hay una versión nueva de Muelle", "A new version of Muelle is out"),
            FontSize = 17, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap,
        });
        root.Children.Add(header);
        buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        if (done)
        {
            root.Children.Add(Text(tr("Lo que cambió está en la página del release, en GitHub.", "What changed is on the release page, on GitHub.")));
            buttons.Children.Add(Button(tr("Ver novedades", "What is new"), false, () => { Updates.OpenPage(); Close(); }));
            buttons.Children.Add(Button(tr("Cerrar", "Close"), true, Close));
        }
        else
        {
            string tag = release?.Tag ?? "";
            bool auto = release != null && release.CanInstall && Updates.InstalledFolder() != null;
            root.Children.Add(Text(auto
                ? tr($"Muelle {tag} está en GitHub; tenés la {Updates.Current}. Se puede instalar ahora: se descarga, se comprueba, y Muelle se cierra y vuelve a abrir solo.",
                     $"Muelle {tag} is on GitHub; you have {Updates.Current}. It can be installed now: it is downloaded, checked, and Muelle closes and opens again by itself.")
                : tr($"Muelle {tag} está en GitHub; tenés la {Updates.Current}. Esta copia no es la instalada, así que la versión nueva se baja desde su página.",
                     $"Muelle {tag} is on GitHub; you have {Updates.Current}. This copy is not the installed one, so the new version is downloaded from its page.")));
            status = Text("");
            status.Visibility = Visibility.Collapsed;
            status.SetResourceReference(TextBlock.ForegroundProperty, "Mu.TextMuted");
            root.Children.Add(status);
            buttons.Children.Add(Button(tr("Más tarde", "Later"), false, Close, cancel: true));
            buttons.Children.Add(Button(tr("Ver novedades", "What is new"), false, () => { Updates.OpenPage(release?.Page ?? Updates.Page); Close(); }));
            if (auto) buttons.Children.Add(Button(tr("Actualizar ahora", "Update now"), true, Install));
        }
        root.Children.Add(buttons);
        Content = root;
    }

    private async void Install()
    {
        working = true;
        buttons.IsEnabled = false;
        status.Visibility = Visibility.Visible;
        try
        {
            await Updates.InstallAsync(manager, release, text => status.Text = text);
        }
        catch (Exception ex)
        {
            working = false;
            buttons.IsEnabled = true;
            status.Text = tr("No se pudo actualizar: ", "Could not update: ") + ex.Message;
            MessageBox.Show(this, status.Text, "Muelle", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static TextBlock Text(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14) };

    private static Button Button(string text, bool isDefault, Action onClick, bool cancel = false)
    {
        var b = new Button { Content = text, IsDefault = isDefault, IsCancel = cancel, Margin = new Thickness(8, 0, 0, 0) };
        b.Click += (s, e) => onClick();
        return b;
    }
}
