# build.ps1
# Builds Muelle and its installers.
#
# Copyright (C) 2026 Gabriel Castiglia. Licensed under the GNU GPL, version 2 only.
# Date: November 6, 2026
# SPDX-License-Identifier: GPL-2.0-only
#
# This program is free software: you can redistribute it and/or modify it under the terms of
# the GNU General Public License, version 2, as published by the Free Software Foundation.
# It is distributed WITHOUT ANY WARRANTY. See the LICENSE file for details.

# Builds Muelle and its installers into dist\:
#   Muelle.exe        self-contained single-file executable (runs without installation)
#   LICENSE.txt       the license, next to the executable
#   muelle.msi        MSI package (silent install: msiexec /i muelle.msi /qn)
#   muelle-setup.exe  setup program in English and Spanish that contains the MSI
# Requires the .NET 10 SDK; the WiX Toolset is restored automatically as a NuGet package.
# The version is read from src\Muelle\Muelle.csproj, the only place where it is set.

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'src\Muelle\Muelle.csproj'
$version = ([xml](Get-Content $project -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw 'Muelle.csproj has no <Version>.' }
$dist = Join-Path $root 'dist'
$publish = Join-Path $root 'build\publish\'
foreach ($dir in $dist, (Join-Path $root 'build')) {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
    New-Item -ItemType Directory -Force $dir | Out-Null
}
Write-Host "Muelle $version"

# 1) Self-contained app in a single Muelle.exe (no .NET install required).
dotnet publish $project -c Release -o $publish
if ($LASTEXITCODE) { throw 'App publish failed.' }

# 2) MSI.
dotnet build (Join-Path $root 'installer\Msi\Muelle.Msi.wixproj') -c Release -o $dist -p:PublishDir=$publish -p:Version=$version
if ($LASTEXITCODE) { throw 'MSI build failed.' }

# 3) The setup window (English and Spanish), then the setup .exe that carries it and the MSI.
$setupUi = Join-Path $root 'build\setupui\'
dotnet build (Join-Path $root 'installer\SetupUI\MuelleSetup.csproj') -c Release -o $setupUi
if ($LASTEXITCODE) { throw 'Setup window build failed.' }
dotnet build (Join-Path $root 'installer\Setup\Muelle.Setup.wixproj') -c Release -o $dist -p:MsiPath="$dist\muelle.msi" -p:SetupUiDir=$setupUi -p:Version=$version
if ($LASTEXITCODE) { throw 'Setup .exe build failed.' }

# 4) The stand-alone executable and its license, next to the installers.
Copy-Item (Join-Path $publish 'Muelle.exe'), (Join-Path $publish 'LICENSE.txt') $dist
Get-ChildItem $dist -Include *.exe, *.msi, *.txt -Recurse | Select-Object Name, Length
