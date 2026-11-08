//
//  Program.cs
//  Muelle: a dock for Windows.
//
//  Copyright (C) 2026 Gabriel Castiglia. Licensed under the GNU GPL, version 3 or later.
//  Date: November 8, 2026
//  SPDX-License-Identifier: GPL-3.0-or-later
//
//  This program is free software: you can redistribute it and/or modify it under the terms of
//  the GNU General Public License as published by the Free Software Foundation, either version 3
//  of the License, or (at your option) any later version.
//  It is distributed WITHOUT ANY WARRANTY. See the LICENSE file for details.
//

using WixToolset.BootstrapperApplicationApi;

namespace Muelle.Setup;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Test option (not for users): "--screenshot <file.png> --page <page> --language en|es" renders one
        // page of the setup window to a PNG, without the setup engine, and exits.
        int shot = Array.IndexOf(args, "--screenshot");
        if (shot >= 0 && shot + 1 < args.Length) return Preview.Render(args);

        // Kept minimal, as WiX requires: the engine waits for this process to connect.
        ManagedBootstrapperApplication.Run(new SetupApplication());
        return 0;
    }
}
