# Muelle - Dock for Windows
# Copyright (C) 2026 M. Gabriel Castiglia. Created 2026-11-06.
# SPDX-License-Identifier: GPL-3.0-or-later
#
# Builds the Muelle installers into Installer\Output:
#   muelle.msi        MSI package (silent install: msiexec /i muelle.msi /qn)
#   muelle-setup.exe  setup wizard that contains the MSI
# Requires the .NET 8 SDK; WiX Toolset is restored automatically as a NuGet package.
param([string]$Version = '1.0.0')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $PSScriptRoot 'Output'
$publish = Join-Path $PSScriptRoot 'obj\publish\'
New-Item -ItemType Directory -Force $out | Out-Null

# 1) Self-contained app in a single Muelle.exe (no .NET install required).
dotnet publish "$root\Muelle.csproj" -c Release -o $publish -p:Version=$Version
if ($LASTEXITCODE) { throw 'App publish failed.' }

# 2) MSI.
dotnet build "$PSScriptRoot\Msi\Muelle.Msi.wixproj" -c Release -o $out -p:PublishDir=$publish -p:Version=$Version
if ($LASTEXITCODE) { throw 'MSI build failed.' }

# 3) Setup .exe with the MSI inside.
dotnet build "$PSScriptRoot\Setup\Muelle.Setup.wixproj" -c Release -o $out -p:MsiPath="$out\muelle.msi" -p:Version=$Version
if ($LASTEXITCODE) { throw 'Setup .exe build failed.' }

Get-ChildItem $out -Include *.msi, *.exe -Recurse | Select-Object Name, Length
