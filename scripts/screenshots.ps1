# screenshots.ps1
# Renders the windows of Muelle to images for the manuals.
#
# Copyright (C) 2026 Gabriel Castiglia. Licensed under the GNU GPL, version 2 only.
# Date: November 8, 2026 (light and dark: November 11, 2026)
# SPDX-License-Identifier: GPL-2.0-only
#
# This program is free software: you can redistribute it and/or modify it under the terms of
# the GNU General Public License, version 2, as published by the Free Software Foundation.
# It is distributed WITHOUT ANY WARRANTY. See the LICENSE file for details.

# Renders the dock, the settings window and the About window, light and dark, and two pages of the setup
# window, in English and Spanish, to docs\images\ for the manuals. It uses the test option --screenshot of
# each program: nothing is shown on screen, no configuration file is created or changed, nothing is
# installed, and the programs exit by themselves. The dock shows the apps of the configuration of the
# user who runs it. Requires the .NET 10 SDK, and a desktop session: run from SSH, the dock image comes
# out empty (the other windows render anyway).

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$app = Join-Path $root 'build\screenshots\app'
$setup = Join-Path $root 'build\screenshots\setup'
dotnet build (Join-Path $root 'src\Muelle\Muelle.csproj') -c Release -o $app
if ($LASTEXITCODE) { throw 'Muelle build failed.' }
dotnet build (Join-Path $root 'installer\SetupUI\MuelleSetup.csproj') -c Release -o $setup
if ($LASTEXITCODE) { throw 'Setup window build failed.' }
$images = Join-Path $root 'docs\images'
New-Item -ItemType Directory -Force $images | Out-Null

function Render([string]$exe, [string]$png, [string[]]$arguments) {
    $p = Start-Process $exe -PassThru -ArgumentList (@('--screenshot', "`"$png`"") + $arguments)
    if (-not $p.WaitForExit(60000)) { $p.Kill(); throw "Timed out: $png" }
    if (-not (Test-Path $png)) { throw "No image: $png" }
    Write-Host "Image: docs\images\$(Split-Path $png -Leaf)"
}

foreach ($language in 'en', 'es') {
    foreach ($window in 'dock', 'settings', 'about') {
        foreach ($theme in 'light', 'dark') {
            $name = if ($theme -eq 'light') { "$window-$language.png" } else { "$window-dark-$language.png" }
            Render (Join-Path $app 'Muelle.exe') (Join-Path $images $name) @('--window', $window, '--language', $language, '--theme', $theme, '--accent', 'blue')
        }
    }
    foreach ($page in 'install', 'uninstall') {
        Render (Join-Path $setup 'MuelleSetup.exe') (Join-Path $images "setup-$page-$language.png") @('--page', $page, '--language', $language)
    }
}
