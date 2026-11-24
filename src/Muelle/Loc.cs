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

/// <summary>Interface languages, in the order the language menus list them.</summary>
public enum UiLanguage { En, Es, Ja }

/// <summary>
/// Interface language. English is the default on first launch; the choice is stored in the configuration.
/// Every visible text goes through <see cref="tr"/>, which receives the versions side by side (Spanish, English,
/// Japanese).
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

    /// <summary>The languages with their own names, in menu order: English, Español, 日本語.</summary>
    public static readonly (UiLanguage Lang, string Name)[] All =
        { (UiLanguage.En, "English"), (UiLanguage.Es, "Español"), (UiLanguage.Ja, "日本語") };

    /// <summary>Configuration code of a language ("en", "es" or "ja").</summary>
    public static string Code(UiLanguage l) => l switch { UiLanguage.Es => "es", UiLanguage.Ja => "ja", _ => "en" };

    /// <summary>Language for a configuration code; anything unknown means English.</summary>
    public static UiLanguage FromCode(string code) => code?.Trim().ToLowerInvariant() switch
    {
        "es" => UiLanguage.Es,
        "ja" => UiLanguage.Ja,
        _ => UiLanguage.En,
    };

    /// <summary>Returns the text for the current interface language.</summary>
    public static string tr(string es, string en, string ja) => current switch
    {
        UiLanguage.Es => es,
        UiLanguage.Ja => ja,
        _ => en,
    };
}
