"""
Muelle -- draws the application icon: a ship under sail, cut out of a 16th-century engraving.
Copyright (C) 2026 Gabriel Castiglia. Licensed under the GNU GPL, version 2 only.
Date: November 11, 2026
SPDX-License-Identifier: GPL-2.0-only

This program is free software: you can redistribute it and/or modify it under the terms of
the GNU General Public License, version 2, as published by the Free Software Foundation.
It is distributed WITHOUT ANY WARRANTY. See the LICENSE file for details.

The ship is the armed three-master of Frans Huys's engraving after Pieter Bruegel the Elder
(The Sailing Vessels, c. 1561-65; The Metropolitan Museum of Art, CC0). assets/ keeps the part
of the scan with the ship. The outline below, traced by hand on a 1000 px wide view of that
part, cuts the ship out of the sky and the sea; inside it the engraving is redrawn in two
colours, cream paper and ink, and laid on a flat rounded tile of the accent colour, with the
hull running off the bottom edge.

Writes:
  src/Muelle/muelle.ico             the program's icon, on the default (blue) tile, 16-256 px
  src/Muelle/icons/muelle_<key>.ico the same on the tile of each accent colour, for the windows
                                    of Muelle when that colour is chosen
  docs/images/icon.png         the icon, 256 px, for the documents
  build/icon_sheet.png         a look at every accent and size

Run:  python scripts/make_icon.py      (needs numpy, opencv-python and Pillow)
"""
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "assets" / "huys_bruegel_three_master_met_cc0.jpg"
S = 1024
CREAM = np.array([246, 239, 222], np.float32)
INK = np.array([24, 26, 34], np.float32)

# ------------------------------------------------------------------ the outline
# Points on the 1000 px wide view of the scan in assets/ (multiply by VIEW to reach the scan).
PARTS = {
    # mizzen yard and its lateen sail
    "lateen": [(44, 236), (56, 242), (110, 318), (160, 384), (195, 438), (213, 478), (222, 520), (226, 560),
               (222, 600), (205, 628), (165, 600), (158, 507), (130, 420), (100, 350), (75, 290)],
    # main mast above the topsail: flag, mast and the small top
    "main mast": [(370, 16), (418, 18), (418, 80), (390, 82), (390, 175), (398, 178), (398, 200), (390, 205),
                  (390, 236), (372, 236), (372, 205), (345, 200), (345, 177), (372, 175), (372, 82)],
    "main topsail": [(268, 262), (330, 248), (420, 232), (480, 236), (530, 250), (565, 278), (590, 315),
                     (598, 352), (560, 350), (520, 336), (480, 346), (422, 345), (300, 345), (270, 340)],
    # the main top and the shrouds under it
    "main top": [(298, 342), (422, 342), (424, 405), (430, 450), (300, 450), (296, 405)],
    # fore mast: flag, topsail and top
    "fore": [(620, 130), (662, 134), (660, 190), (616, 196), (612, 212), (575, 226), (610, 232), (660, 232),
             (690, 248), (700, 290), (698, 330), (670, 320), (640, 345), (618, 395), (634, 398), (634, 426),
             (562, 426), (562, 398), (600, 395), (600, 345), (578, 330), (572, 240)],
    # courses, bowsprit, spritsail and hull, the keel running off the bottom
    "hull": [(113, 983), (113, 700), (120, 640), (160, 624), (205, 628), (226, 600), (250, 540), (275, 480),
             (300, 450), (430, 450), (480, 444), (540, 440), (600, 428), (640, 428), (700, 446), (760, 450),
             (802, 470), (832, 512), (852, 560), (866, 600), (884, 572), (904, 538), (912, 518), (928, 524),
             (922, 548), (902, 580), (880, 612), (872, 650), (867, 664), (962, 677), (980, 739), (971, 802),
             (952, 840), (924, 860), (886, 866), (876, 840), (868, 764), (860, 714), (810, 739), (753, 758),
             (708, 783), (702, 821), (690, 866), (690, 983)],
    # the pole out of the stern
    "pole": [(4, 676), (16, 676), (158, 740), (156, 752), (4, 690)],
}

g = cv2.imread(str(SRC), cv2.IMREAD_GRAYSCALE)
VIEW = g.shape[1] / 1000
# Paper tone, then each pixel as a share of it: 1 is bare paper, 0 is full ink.
paper = cv2.GaussianBlur(cv2.dilate(g, np.ones((15, 15), np.uint8)), (0, 0), 25)
norm = np.clip(g.astype(np.float32) / np.maximum(paper.astype(np.float32), 1), 0, 1)

mask = np.zeros(g.shape, np.uint8)
for part in PARTS.values():
    cv2.fillPoly(mask, [np.array([(x * VIEW, y * VIEW) for x, y in part], np.int32)], 255)

ys, xs = np.nonzero(mask)
x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
norm_c = norm[y0:y1, x0:x1]
mask_c = mask[y0:y1, x0:x1].astype(np.float32) / 255

# ------------------------------------------------------------------ placement
SW = 960                                        # ship width on the 1024 tile
sc = SW / norm_c.shape[1]
SH = int(round(norm_c.shape[0] * sc))
d_norm = cv2.resize(norm_c, (SW, SH), interpolation=cv2.INTER_AREA)
d_sil = cv2.GaussianBlur(cv2.resize(mask_c, (SW, SH), interpolation=cv2.INTER_AREA), (0, 0), 0.8)
SX, SY = (S - SW) // 2 + 4, S - SH + 64         # the keel runs off the bottom edge
cut = SY + SH - S                               # rows below the tile
if cut > 0:
    d_norm, d_sil, SH = d_norm[:-cut], d_sil[:-cut], SH - cut


def compose(bg, detail=1.0):
    """The whole drawing over a plain background of colour bg (float RGB). A lower detail lightens the
    hatching, so that at 16 to 32 px the ship reads as one cream shape instead of grey noise."""
    img = np.empty((S, S, 3), np.float32)
    img[:] = bg
    sh = np.zeros((S, S), np.float32)
    sh[SY:SY + SH, SX:SX + SW] = d_sil
    sh = np.pad(sh, ((12, 0), (10, 0)))[:S, :S]          # the shadow falls down and right
    sh = cv2.GaussianBlur(sh, (0, 0), 12) * 0.35
    img *= (1 - sh[..., None])
    reg = img[SY:SY + SH, SX:SX + SW]
    tone = 1 - (1 - np.clip((d_norm - 0.18) / 0.45, 0, 1)[..., None]) * detail
    col = INK + (CREAM - INK) * tone
    a = d_sil[..., None]
    reg[:] = reg * (1 - a) + col * a
    return img


m = Image.new("L", (S * 4, S * 4), 0)
ImageDraw.Draw(m).rounded_rectangle((0, 0, S * 4 - 1, S * 4 - 1), radius=200 * 4, fill=255)
tile = np.array(m.resize((S, S), Image.LANCZOS), np.float32) / 255

def layers(detail):
    """The drawing (premultiplied) and how much of the tile shows through it, from drawing it on black and on white."""
    on0, on1 = compose(np.zeros(3, np.float32), detail), compose(np.full(3, 255, np.float32), detail)
    return np.clip(on0, 0, 255), np.clip((on1 - on0).mean(axis=2) / 255, 0, 1)


fg, k = layers(1.0)
fg_small, k_small = layers(0.45)


def icon(rgb, size=S):
    """The icon on a tile of colour rgb, as an RGBA image of the given size."""
    f, kk = (fg, k) if size > 32 else (fg_small, k_small)
    pre = (f + np.array(rgb, np.float32) * kk[..., None]) * tile[..., None]
    rgba = np.dstack([pre, tile * 255])
    if size != S:
        rgba = cv2.resize(rgba, (size, size), interpolation=cv2.INTER_AREA)
    a = rgba[..., 3:4]
    rgb_ = np.where(a > 0, rgba[..., :3] * 255 / np.maximum(a, 1e-6), 0)
    return Image.fromarray(np.clip(np.dstack([rgb_, a]), 0, 255).round().astype(np.uint8), "RGBA")


# The tile colours of the twelve accents: the light tone of each one in Accents (Theme.cs), the default first.
TILES = {"blue": "1F5FD1", "gold": "9A6F00", "purple": "5B2A86", "sky": "1A8CD8", "ember": "D9652F", "red": "C8102E",
         "yellow": "B88A00", "green": "1E8A43", "teal": "00796B", "pink": "C2185B", "black": "1D1D1F", "white": "8E8E93"}
TILES = {key: tuple(int(v[i:i + 2], 16) for i in (0, 2, 4)) for key, v in TILES.items()}
(ROOT / "build").mkdir(exist_ok=True)
(ROOT / "src" / "Muelle" / "icons").mkdir(exist_ok=True)
sizes = [256, 64, 48, 40, 32, 24, 20, 16]


def write_ico(path, rgb):
    imgs = [icon(rgb, z) for z in sizes]
    imgs[0].save(path, sizes=[(z, z) for z in sizes], append_images=imgs[1:])
    return imgs[0]


for key, rgb in TILES.items():
    write_ico(ROOT / "src" / "Muelle" / "icons" / f"muelle_{key}.ico", rgb)
write_ico(ROOT / "src" / "Muelle" / "muelle.ico", TILES["blue"]).save(ROOT / "docs" / "images" / "icon.png")

sheet = Image.new("RGB", (6 * 170 + 20, 2 * 250 + 20), (246, 246, 248))
for i, t in enumerate(TILES.values()):
    x, y = 20 + (i % 6) * 170, 20 + (i // 6) * 250
    big = icon(t, 150)
    sheet.paste(big, (x, y), big)
    sx = x
    for z in (48, 32, 16):
        im = icon(t, z)
        sheet.paste(im, (sx, y + 170 + (48 - z) // 2), im)
        sx += z + 10
sheet.save(ROOT / "build" / "icon_sheet.png")
icon(TILES["blue"], 1024).save(ROOT / "build" / "icon_1024.png")
print("icon: muelle.ico, icons/muelle_<key>.ico, icon.png written")
