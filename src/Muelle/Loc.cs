//
//  Loc.cs
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

namespace Muelle;

public enum UiLanguage { En, Es }

/// <summary>
/// Interface language. English is the default on first launch; the choice is stored in the configuration.
/// Every visible text goes through <see cref="tr"/>, which receives both versions side by side.
/// </summary>
internal static class Loc
{
    private static UiLanguage current = UiLanguage.En;

    /// <summary>Raised after the language changes, so open windows can rebuild their texts.</summary>
    public static event Action Changed;

    public static UiLanguage Current
    {
        get => current;
        set
        {
            if (current == value) return;
            current = value;
            Changed?.Invoke();
        }
    }

    /// <summary>Configuration code of a language ("en" or "es").</summary>
    public static string Code(UiLanguage l) => l == UiLanguage.Es ? "es" : "en";

    /// <summary>Language for a configuration code; anything unknown means English.</summary>
    public static UiLanguage FromCode(string code) =>
        string.Equals(code, "es", StringComparison.OrdinalIgnoreCase) ? UiLanguage.Es : UiLanguage.En;

    /// <summary>Returns the text for the current interface language.</summary>
    public static string tr(string es, string en) => current == UiLanguage.Es ? es : en;
}
