#!/bin/zsh
# manual.sh
# Builds the PDF manuals from the READMEs.
#
# Copyright (C) 2026 Gabriel Castiglia. Licensed under the GNU GPL, version 3 or later.
# Date: November 8, 2026
# SPDX-License-Identifier: GPL-3.0-or-later
#
# This program is free software: you can redistribute it and/or modify it under the terms of
# the GNU General Public License as published by the Free Software Foundation, either version 3
# of the License, or (at your option) any later version.
# It is distributed WITHOUT ANY WARRANTY. See the LICENSE file for details.

# Builds the PDF manuals from the READMEs:
#   README.md     -> docs/Muelle-manual-en.pdf
#   README.es.md  -> docs/Muelle-manual-es.pdf
# Requires pandoc (brew install pandoc) and Google Chrome, which prints the HTML to PDF.
# Run it again after every change to either README or to the images in docs/images.

set -euo pipefail
ROOT="${0:A:h:h}"
cd "$ROOT"

CHROME="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
command -v pandoc >/dev/null || { echo "pandoc is missing: brew install pandoc" >&2; exit 1; }
[[ -x "$CHROME" ]] || { echo "Google Chrome is missing from /Applications" >&2; exit 1; }
VERSION=$(sed -n 's|.*<Version>\(.*\)</Version>.*|\1|p' src/Muelle/Muelle.csproj | head -1)

TEMP="$(mktemp -d)"
trap 'rm -rf "$TEMP"' EXIT

# Links to other files of the repository have no target inside a PDF: they are kept as plain text.
cat > "$TEMP/links.lua" <<'LUA'
function Link(el)
  if not el.target:match("^%a[%w+.-]*:") and not el.target:match("^#") then
    return el.content
  end
  return el
end
LUA

build() {
    local source=$1 lang=$2 title=$3 subtitle=$4 output=$5
    # Cover block with the icon, the name and the version.
    cat > "$TEMP/cover-$lang.html" <<HTML
<div class="cover">
  <img src="docs/images/icon.png" alt="">
  <div>
    <p class="name">Muelle</p>
    <p class="subtitle">$subtitle $VERSION</p>
  </div>
</div>
HTML
    # Self-contained HTML: stylesheet and images embedded, no references to local files.
    pandoc "$source" --from gfm --to html5 --standalone --embed-resources \
        --lua-filter "$TEMP/links.lua" \
        --css docs/manual.css \
        --include-before-body "$TEMP/cover-$lang.html" \
        --metadata pagetitle="$title" \
        --metadata lang="$lang" \
        --output "$TEMP/manual-$lang.html"
    # No header or footer: Chrome would print the local path of the HTML file in them.
    "$CHROME" --headless --disable-gpu --no-pdf-header-footer \
        --print-to-pdf="$ROOT/$output" "file://$TEMP/manual-$lang.html" 2>/dev/null
    echo "Manual: $output"
}

build README.md    en "Muelle · Manual"            "A dock for Windows · version"   docs/Muelle-manual-en.pdf
build README.es.md es "Muelle · Manual de usuario" "Dock para Windows · versión"     docs/Muelle-manual-es.pdf
