//
//  Program.cs
//  Muelle: a dock for Windows.
//
//  Copyright (C) 2026 Gabriel Castiglia. Licensed under the GNU GPL, version 2 only.
//  Date: November 8, 2026
//  SPDX-License-Identifier: GPL-2.0-only
//
//  This program is free software: you can redistribute it and/or modify it under the terms of
//  the GNU General Public License, version 2, as published by the Free Software Foundation.
//  It is distributed WITHOUT ANY WARRANTY. See the LICENSE file for details.
//

using WixToolset.BootstrapperApplicationApi;

namespace Muelle.Setup;

internal static class Program
{
    // No [STAThread] here: the WiX library calls back from its own threads and creates the window's
    // thread itself (see SetupApplication.Run). An STA main thread makes BootstrapperApplicationRun fail.
    private static int Main(string[] args)
    {
        // Test option (not for users): "--screenshot <file.png> --page <page> --language en|es" renders one
        // page of the setup window to a PNG, without the setup engine, and exits. WPF needs an STA thread.
        int shot = Array.IndexOf(args, "--screenshot");
        if (shot >= 0 && shot + 1 < args.Length)
        {
            int rc = 1;
            var thread = new Thread(() => rc = Preview.Render(args));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return rc;
        }

        // Kept minimal, as WiX requires: the engine waits for this process to connect.
        ManagedBootstrapperApplication.Run(new SetupApplication());
        return 0;
    }
}
