# make-icon.ps1
# Draws the Muelle icon (muelle.ico).
#
# Copyright (C) 2026 Gabriel Castiglia. Licensed under the GNU GPL, version 3 or later.
# Date: November 6, 2026
# SPDX-License-Identifier: GPL-3.0-or-later
#
# This program is free software: you can redistribute it and/or modify it under the terms of
# the GNU General Public License as published by the Free Software Foundation, either version 3
# of the License, or (at your option) any later version.
# It is distributed WITHOUT ANY WARRANTY. See the LICENSE file for details.

# Draws the original app icon: a blue rounded square with a glass column, three white dots (the docked apps)
# and a small dot (an open app). Writes an .ico with PNG images at 16, 24, 32, 48, 64 and 256 px.

param([string]$Out = (Join-Path $PSScriptRoot "..\src\Muelle\muelle.ico"))
Add-Type -AssemblyName System.Drawing

function New-IconPng([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $m = [single]($s * 0.06); $w = [single]($s - 2 * $m); $r = [single]($s * 0.22)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($m, $m, 2 * $r, 2 * $r, 180, 90)
    $path.AddArc($m + $w - 2 * $r, $m, 2 * $r, 2 * $r, 270, 90)
    $path.AddArc($m + $w - 2 * $r, $m + $w - 2 * $r, 2 * $r, 2 * $r, 0, 90)
    $path.AddArc($m, $m + $w - 2 * $r, 2 * $r, 2 * $r, 90, 90)
    $path.CloseFigure()
    $rect = New-Object System.Drawing.RectangleF $m, $m, $w, $w
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 56, 189, 248)), ([System.Drawing.Color]::FromArgb(255, 79, 70, 229)), 45
    $g.FillPath($brush, $path)
    # Glass column on the left.
    $col = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(70, 255, 255, 255))
    $g.FillRectangle($col, [single]($m + $w * 0.14), [single]($m + $w * 0.12), [single]($w * 0.30), [single]($w * 0.76))
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $sz = [single]($w * 0.20)
    foreach ($i in 0..2) {
        $y = [single]($m + $w * 0.19 + $i * $w * 0.22)
        $g.FillEllipse($white, [single]($m + $w * 0.19), $y, $sz, $sz)
    }
    # "Open app" dot.
    $g.FillEllipse($white, [single]($m + $w * 0.55), [single]($m + $w * 0.43), [single]($w * 0.12), [single]($w * 0.12))
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return ,$ms.ToArray()
}

# ICO file: header, one directory entry per size, then the PNG images.
$sizes = 16, 24, 32, 48, 64, 256
$pngs = foreach ($s in $sizes) { ,(New-IconPng $s) }
$fs = [System.IO.File]::Create($Out)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $d = $pngs[$i]
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$d.Length); $bw.Write([uint32]$offset)
    $offset += $d.Length
}
foreach ($d in $pngs) { $bw.Write($d) }
$bw.Close()
Write-Output "Icon written: $Out"
