#!/usr/bin/env bash
# manual.sh
# Builds the PDF manuals and the plain-text READMEs from the Markdown READMEs.
#
# Copyright (C) 2026 Gabriel Castiglia. Licensed under the GNU GPL, version 2 only.
# Date: November 8, 2026 (also in Git Bash on Windows: November 20, 2026)
# SPDX-License-Identifier: GPL-2.0-only
#
# This program is free software: you can redistribute it and/or modify it under the terms of
# the GNU General Public License, version 2, as published by the Free Software Foundation.
# It is distributed WITHOUT ANY WARRANTY. See the LICENSE file for details.

# Builds the PDF manuals and the plain-text READMEs from the Markdown READMEs:
#   README.md     -> docs/Muelle-manual-en.pdf and README.txt
#   README.es.md  -> docs/Muelle-manual-es.pdf and README.es.txt
#   README.ja.md  -> docs/Muelle-manual-ja.pdf and README.ja.txt
# The .txt copies are for reading on Windows without a Markdown viewer; they are never edited by hand.
# Requires pandoc (brew install pandoc, or pandoc.org) and Google Chrome, which prints the HTML to PDF.
# Runs on macOS and in Git Bash on Windows; CHROME may name another Chrome (or Edge) executable.
# Run it again after every change to either README or to the images in docs/images.

set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

if [[ -z "${CHROME:-}" ]]; then
    CHROME="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
    [[ -x "$CHROME" ]] || CHROME="/c/Program Files/Google/Chrome/Application/chrome.exe"
fi
command -v pandoc >/dev/null || { echo "pandoc is missing: brew install pandoc (or pandoc.org)" >&2; exit 1; }
[[ -x "$CHROME" ]] || { echo "Google Chrome is missing (set CHROME to its executable)" >&2; exit 1; }
# Windows programs (Chrome) want Windows paths; elsewhere a path stays as it is.
native() { if command -v cygpath >/dev/null; then cygpath -m "$1"; else printf '%s' "$1"; fi; }
REPO="https://github.com/Gabriel-Castiglia/muelle"
VERSION=$(sed -n 's|.*<Version>\(.*\)</Version>.*|\1|p' src/Muelle/Muelle.csproj | head -1)
# The line under the title of each README names the version; 2.3.2 to 2.4.1 went out with "version 2.3.1" there.
for f in README.md:version README.es.md:versión README.ja.md:バージョン; do
    grep -q "· ${f#*:} $VERSION ·" "${f%%:*}" || { echo "${f%%:*} does not say \"${f#*:} $VERSION\" under its title" >&2; exit 1; }
done

TEMP="$(mktemp -d)"
trap 'rm -rf "$TEMP"' EXIT

# Relative links (LICENSE, CHANGELOG.md, docs/…) point to the repository in the PDF; otherwise they
# would become paths inside the temporary folder of whoever builds it.
# The centered header of the README (icon, name, phrase, picture) is for GitHub; the cover has the icon and the name.
cat > "$TEMP/links.lua" <<LUA
function RawBlock(el)
  if el.format == "html" and el.text:match('align="center"') then return {} end
end
function Link(el)
  if not el.target:match("^%a[%w+.-]*:") and not el.target:match("^#") then
    el.target = "$REPO/blob/main/" .. el.target
  end
  return el
end
LUA

# Plain text: no images, titles underlined, links to the other README point to its .txt copy, and web
# links keep their address after the text.
cat > "$TEMP/text.lua" <<'LUA'
function Image(el) return {} end
-- The centered GitHub header: only its title stays, underlined like the other titles.
function RawBlock(el)
  local title = el.text:match("<h1[^>]*>(.-)</h1>")
  if not title then return {} end
  return pandoc.Plain({pandoc.Str(title), pandoc.LineBreak(), pandoc.Str(string.rep("=", utf8.len(title)))})
end
function Header(el)
  local text = pandoc.utils.stringify(el.content)
  local rule = string.rep(el.level == 1 and "=" or "-", utf8.len(text))
  return pandoc.Plain({pandoc.Str(text), pandoc.LineBreak(), pandoc.Str(rule)})
end
function Link(el)
  local target = el.target:gsub("^README%.es%.md$", "README.es.txt"):gsub("^README%.ja%.md$", "README.ja.txt"):gsub("^README%.md$", "README.txt")
  local text = pandoc.utils.stringify(el.content)
  if text == target then return pandoc.Str(target) end
  if not target:match("^https?:") then return pandoc.Str(text .. " (" .. target .. ")") end
  return pandoc.Str(text .. " <" .. target .. ">")
end
LUA

text() {
    local source=$1 output=$2
    pandoc "$source" --from gfm --to plain --columns 100 --lua-filter "$TEMP/text.lua" --output "$output"
    echo "Text: $output"
}

build() {
    local source=$1 lang=$2 title=$3 subtitle=$4 output=$5
    # Cover block with the icon, the name and the version.
    cat > "$TEMP/cover-$lang.html" <<HTML
<div class="cover">
  <img src="docs/images/icon.png" alt="">
  <div>
    <p class="name">Muelle</p>
    <p class="subtitle">$subtitle $VERSION · <a href="$REPO">${REPO#https://}</a></p>
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
    # No header or footer: Chrome would print the local path of the HTML file in them. A profile of its own,
    # so a Chrome that is already open is left alone.
    "$CHROME" --headless --disable-gpu --no-pdf-header-footer --user-data-dir="$(native "$TEMP/chrome")" \
        --print-to-pdf="$(native "$ROOT/$output")" "file:///$(native "$TEMP/manual-$lang.html" | sed 's|^/||')" 2>/dev/null
    echo "Manual: $output"
}

build README.md    en "Muelle · Manual"            "A dock for Windows · version"   docs/Muelle-manual-en.pdf
build README.es.md es "Muelle · Manual de usuario" "Dock para Windows · versión"     docs/Muelle-manual-es.pdf
build README.ja.md ja "Muelle · ユーザーマニュアル"   "Windows 用ドック · バージョン"     docs/Muelle-manual-ja.pdf

text README.md    README.txt
text README.es.md README.es.txt
text README.ja.md README.ja.txt
