#!/usr/bin/env bash
# manual.sh
# Builds the PDF manuals and the plain-text READMEs from the Markdown READMEs.
#
# Copyright (C) 2026 Gabriel Castiglia. Licensed under the GNU GPL, version 2 only.
# Date: November 8, 2026 (also in Git Bash on Windows: November 20, 2026; cover page, contents and figures:
# November 27, 2026)
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
# The centered header of the README (icon, name, phrase, picture), the line with the author and the date, the
# links to the other languages and to the PDF itself are for GitHub: the cover page carries them instead.
# A screenshot alone in its paragraph becomes a numbered figure, with its alternative text as the caption.
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
function Para(el)
  local text = pandoc.utils.stringify(el)
  if text:match("^Gabriel Castiglia · ") then return {} end
  local drop = false
  pandoc.walk_block(el, { Link = function(l)
    if l.target:match("/README[%.%a]*%.md$") or l.target:match("Muelle%-manual%-%a+%.pdf$") then drop = true end
  end })
  if drop then return {} end
  if #el.content == 1 and el.content[1].t == "Image" then
    local img = el.content[1]
    -- The settings window is large and full of small text: it takes the whole width of the page (not the single
    -- card of the locked dock, which is small).
    local classes = (img.src:match("settings") and not img.src:match("locked")) and { "wide" } or {}
    return pandoc.Figure({ pandoc.Plain({ img }) }, { pandoc.Plain(img.caption) }, pandoc.Attr("", classes))
  end
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
    local source=$1 lang=$2 manual=$3 contents=$4 output=$5
    # From the README: the phrase under the title and the line with the author, the date, the version and the
    # licence, for the cover page.
    local tagline byline
    tagline=$(sed -n 's|.*<p align="center"><b>\(.*\)</b></p>.*|\1|p' "$source" | head -1)
    byline=$(grep -m1 '^Gabriel Castiglia · ' "$source")
    cat > "$TEMP/cover-$lang.html" <<HTML
<div class="cover">
  <div class="band">
    <img src="docs/images/icon.png" alt="">
    <div>
      <p class="name">Muelle</p>
      <p class="kind">$manual</p>
    </div>
  </div>
  <p class="tagline">$tagline</p>
  <div class="shot"><img src="docs/images/dock-$lang.png" alt=""></div>
  <div class="meta">
    <p class="version">$byline</p>
    <p><a href="$REPO">${REPO#https://}</a></p>
  </div>
</div>
HTML
    # The name of the manual and its version in the bottom margin of every page but the cover.
    cat > "$TEMP/footer-$lang.html" <<HTML
<style>@page { @bottom-left { content: "Muelle $VERSION · $manual"; } }</style>
HTML
    # Self-contained HTML: stylesheet and images embedded, no references to local files. The sections of the
    # README (##) become the numbered chapters of the manual, listed on the contents page.
    pandoc "$source" --from gfm --to html5 --standalone --embed-resources \
        --lua-filter "$TEMP/links.lua" \
        --shift-heading-level-by=-1 --number-sections --toc --toc-depth=1 \
        --css docs/manual.css \
        --include-in-header "$TEMP/footer-$lang.html" \
        --include-before-body "$TEMP/cover-$lang.html" \
        --metadata pagetitle="Muelle $VERSION · $manual" \
        --metadata toc-title="$contents" \
        --metadata lang="$lang" \
        --output "$TEMP/manual-$lang.html"
    # No header or footer of Chrome's own: it would print the local path of the HTML file in them. A profile of
    # its own, so a Chrome that is already open is left alone.
    "$CHROME" --headless --disable-gpu --no-pdf-header-footer --user-data-dir="$(native "$TEMP/chrome")" \
        --print-to-pdf="$(native "$ROOT/$output")" "file:///$(native "$TEMP/manual-$lang.html" | sed 's|^/||')" 2>/dev/null
    echo "Manual: $output"
}

build README.md    en "User manual"       "Contents"  docs/Muelle-manual-en.pdf
build README.es.md es "Manual de usuario" "Contenido" docs/Muelle-manual-es.pdf
build README.ja.md ja "ユーザーマニュアル" "目次"      docs/Muelle-manual-ja.pdf

text README.md    README.txt
text README.es.md README.es.txt
text README.ja.md README.ja.txt
