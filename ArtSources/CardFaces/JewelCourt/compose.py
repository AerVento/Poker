"""Prepare reusable Jewel Court sprites and compose previews, never AI-generate 52 cards.

Requires Pillow and numpy. Run with Python 3.10+ on Windows, or pass --font and
--suit-font on another system. Font files are used locally and are not bundled.
All outputs stay in this art-source directory; nothing is installed in Unity.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageOps


ROOT = Path(__file__).resolve().parent
ELEMENTS = ROOT / "Elements"
PREVIEWS = ROOT / "Previews"
SUITS = ("Spade", "Heart", "Diamond", "Club")
SYMBOLS = dict(zip(SUITS, ("\u2660", "\u2665", "\u2666", "\u2663")))
RANKS = ("A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K")
COURTS = ("J", "Q", "K")
SIZE = (816, 1152)
INK = {s: "#B20B20" if s in ("Heart", "Diamond") else "#151B20" for s in SUITS}
# Pixel rectangles: left, top, right, bottom, measured from the card's top left.
LAYOUT = {
    "cardSize": list(SIZE),
    "rankBox": [66, 70, 354, 310],
    "suitBox": [144, 338, 276, 470],
    "rankBoxOverrides": {"10": [66, 70, 534, 310]},
    "jewelBox": [398, 626, 744, 1068],
    "portraitBox": [346, 600, 754, 1070],
    "bottomRightIndex": False,
    "frontOnly": True,
}


def chroma_key(source: Image.Image) -> Image.Image:
    """Key the generated magenta plate, preserving opaque interiors and edge colors.

    Restrict edge unmixing to a 3-pixel band adjoining confidently keyed pixels.
    This keeps red gemstone facets, white gems and black linework intact.
    """
    rgb = np.asarray(source.convert("RGB"), dtype=np.float32)
    red, green, blue = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    background = (red > 218) & (blue > 218) & (green < 38)
    expanded = np.asarray(Image.fromarray(background.astype(np.uint8) * 255)
                          .filter(ImageFilter.MaxFilter(7))) > 0
    alpha = np.ones(red.shape, dtype=np.float32)
    alpha[background] = 0
    chroma = np.minimum(red, blue) - green
    edge = expanded & ~background & (chroma > 30)

    # Find an adjacent solid foreground sample for each anti-aliased edge pixel.
    solid = ~background & (chroma < 45)
    known = solid.copy()
    foreground = rgb.copy()
    height, width = red.shape
    for _ in range(6):
        previous = known.copy()
        previous_colors = foreground.copy()
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1),
                       (-1, -1), (-1, 1), (1, -1), (1, 1)):
            shifted = np.roll(previous, (dy, dx), axis=(0, 1))
            if dy < 0:
                shifted[height + dy:, :] = False
            elif dy > 0:
                shifted[:dy, :] = False
            if dx < 0:
                shifted[:, width + dx:] = False
            elif dx > 0:
                shifted[:, :dx] = False
            take = ~known & shifted
            colors = np.roll(previous_colors, (dy, dx), axis=(0, 1))
            foreground[take] = colors[take]
            known[take] = True

    key = np.array([255.0, 0.0, 255.0], dtype=np.float32)
    direction = foreground - key
    denominator = np.maximum(np.sum(direction * direction, axis=2), 1)
    coverage = np.clip(np.sum((rgb - key) * direction, axis=2) / denominator, 0, 1)
    alpha[edge & known] = coverage[edge & known]
    fallback = edge & ~known
    alpha[fallback] = np.clip(1 - chroma[fallback] / 255, 0, 1)
    # Recover foreground RGB after removing the composited key contribution.
    partial = edge & (alpha > 0.025)
    rgb[partial] = np.clip((rgb[partial] - (1 - alpha[partial, None]) * key)
                           / alpha[partial, None], 0, 255)
    residual_key = ((rgb[:, :, 0] > 200) & (rgb[:, :, 2] > 200)
                    & (rgb[:, :, 1] < 50))
    alpha[residual_key] = 0
    alpha[alpha < 0.025] = 0
    rgb[alpha == 0] = 0
    return Image.fromarray(np.dstack((rgb, alpha * 255)).astype(np.uint8), "RGBA")


def trim(image: Image.Image, padding: int = 8) -> Image.Image:
    bounds = image.getchannel("A").point(lambda value: 255 if value > 20 else 0).getbbox()
    if bounds is None:
        raise ValueError("Empty sprite")
    return ImageOps.expand(image.crop(bounds), border=padding, fill=(0, 0, 0, 0))


def extract_art() -> dict:
    records = {}
    for folder in ("Jewels", "Court", "Ranks", "Suits"):
        (ELEMENTS / folder).mkdir(parents=True, exist_ok=True)
    source = Image.open(ROOT / "Sources/Jewels.png")
    width, height = source.size
    for index, suit in enumerate(SUITS):
        x, y = index % 2, index // 2
        box = (x * width // 2, y * height // 2,
               (x + 1) * width // 2, (y + 1) * height // 2)
        sprite = trim(chroma_key(source.crop(box)))
        path = ELEMENTS / "Jewels" / f"{suit}.png"
        sprite.save(path)
        records[f"jewel/{suit}"] = {"path": path.relative_to(ROOT).as_posix(),
                                     "size": list(sprite.size), "sourceBox": box}

    for suit in SUITS:
        source = Image.open(ROOT / "Sources" / f"Court{suit}.png")
        keyed = chroma_key(source)
        occupancy = (np.asarray(keyed.getchannel("A")) > 32).sum(axis=0)
        width, height = source.size
        boundaries = [0]
        # Locate actual empty gutters: generated figures need not honor exact thirds.
        for index in (1, 2):
            center = width * index // 3
            radius = width // 18
            values = occupancy[center - radius:center + radius]
            candidates = np.flatnonzero(values == values.min()) + center - radius
            boundaries.append(int(candidates[len(candidates) // 2]))
            if values.min() > 0:
                raise ValueError(f"{suit}: portraits overlap near column {index}")
        boundaries.append(width)
        for index, rank in enumerate(COURTS):
            box = (boundaries[index], 0, boundaries[index + 1], height)
            sprite = trim(keyed.crop(box))
            path = ELEMENTS / "Court" / f"{suit}_{rank}.png"
            sprite.save(path)
            records[f"court/{suit}/{rank}"] = {
                "path": path.relative_to(ROOT).as_posix(), "size": list(sprite.size),
                "sourceBox": box,
            }
    return records


def glyph(label: str, font_path: str, size: int) -> Image.Image:
    font = ImageFont.truetype(font_path, size)
    bounds = font.getbbox(label)
    mask = Image.new("L", (bounds[2] - bounds[0] + 8, bounds[3] - bounds[1] + 8))
    ImageDraw.Draw(mask).text((4 - bounds[0], 4 - bounds[1]), label, font=font, fill=255)
    result = Image.new("RGBA", mask.size, (255, 255, 255, 0))
    result.putalpha(mask)
    return result


def tint(image: Image.Image, color: str) -> Image.Image:
    result = Image.new("RGBA", image.size, color)
    result.putalpha(image.getchannel("A"))
    return result


def place(card: Image.Image, art: Image.Image, box: list, align: str) -> tuple:
    art = trim(art, 0)
    left, top, right, bottom = box
    art = ImageOps.contain(art, (right - left, bottom - top), Image.Resampling.LANCZOS)
    x = right - art.width if align == "bottom-right" else left + (right - left - art.width) // 2
    y = bottom - art.height if align == "bottom-right" else top
    card.alpha_composite(art, (x, y))
    return x, y, x + art.width, y + art.height


def create_base() -> Image.Image:
    scale = 2
    base = Image.new("RGBA", (SIZE[0] * scale, SIZE[1] * scale))
    draw = ImageDraw.Draw(base)
    draw.rounded_rectangle((0, 0, base.width - 1, base.height - 1),
                           radius=42 * scale, fill="#FFFEFC")
    draw.rounded_rectangle((25 * scale, 25 * scale,
                            base.width - 26 * scale, base.height - 26 * scale),
                           radius=27 * scale, outline="#C9CAC6", width=2 * scale)
    return base.resize(SIZE, Image.Resampling.LANCZOS)


def compose(suit: str, rank: str, base: Image.Image) -> tuple:
    card = base.copy()
    rank_art = tint(Image.open(ELEMENTS / "Ranks" / f"{rank}.png"), INK[suit])
    rank_box = LAYOUT["rankBoxOverrides"].get(rank, LAYOUT["rankBox"])
    rank_bounds = place(card, rank_art, rank_box, "top-center")
    suit_art = tint(Image.open(ELEMENTS / "Suits" / f"{suit}.png"), INK[suit])
    # Suit is aligned to the same index center as the rank, including two-digit 10.
    suit_box = LAYOUT["suitBox"].copy()
    center = (rank_box[0] + rank_box[2]) // 2
    suit_box[0], suit_box[2] = center - 66, center + 66
    suit_bounds = place(card, suit_art, suit_box, "top-center")
    is_court = rank in COURTS
    path = ELEMENTS / "Court" / f"{suit}_{rank}.png" if is_court else ELEMENTS / "Jewels" / f"{suit}.png"
    art_bounds = place(card, Image.open(path),
                       LAYOUT["portraitBox" if is_court else "jewelBox"], "bottom-right")
    return card, {"rank": rank_bounds, "suit": suit_bounds, "art": art_bounds}


def sheet(cards: list, columns: int, cell_size: tuple, gap: int = 18) -> Image.Image:
    rows = (len(cards) + columns - 1) // columns
    width, height = cell_size
    result = Image.new("RGB", (columns * width + (columns + 1) * gap,
                                rows * height + (rows + 1) * gap), "#163E32")
    for index, card in enumerate(cards):
        image = card.resize(cell_size, Image.Resampling.LANCZOS)
        x = gap + index % columns * (width + gap)
        y = gap + index // columns * (height + gap)
        result.paste(image, (x, y), image)
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    font_dir = Path(os.environ.get("WINDIR", "C:/Windows")) / "Fonts"
    parser.add_argument("--font", default=str(font_dir / "georgiab.ttf"))
    parser.add_argument("--suit-font", default=str(font_dir / "seguisym.ttf"))
    parser.add_argument("--export-cards", action="store_true",
                        help="Optionally bake 52 individual PNGs; default produces elements and sheets only.")
    args = parser.parse_args()
    PREVIEWS.mkdir(parents=True, exist_ok=True)
    records = extract_art()
    for rank in RANKS:
        glyph(rank, args.font, 360).save(ELEMENTS / "Ranks" / f"{rank}.png")
    for suit in SUITS:
        glyph(SYMBOLS[suit], args.suit_font, 280).save(ELEMENTS / "Suits" / f"{suit}.png")
    base = create_base()
    base.save(ELEMENTS / "Base.png")
    cards, checks = {}, {}
    for suit in SUITS:
        for rank in RANKS:
            card, bounds = compose(suit, rank, base)
            cards[suit, rank] = card
            checks[f"{suit}_{rank}"] = bounds
            assert bounds["rank"][3] < bounds["suit"][1], (suit, rank, "index overlap")
            assert bounds["rank"][3] - bounds["rank"][1] >= 220, (suit, rank, "rank too small")
            assert bounds["art"][0] > SIZE[0] * 0.4, (suit, rank, "art too far left")
            assert bounds["art"][1] >= SIZE[1] / 2, (suit, rank, "art too high")
            assert card.getpixel((0, 0))[3] == 0
            if args.export_cards:
                folder = ROOT / "Baked"
                folder.mkdir(exist_ok=True)
                card.save(folder / f"{suit}_{rank}.png")
    sheet([cards[s, "7"] for s in SUITS], 4, (306, 432)).save(PREVIEWS / "Numbers.png")
    sheet([cards[s, r] for s in SUITS for r in COURTS], 3, (204, 288)).save(PREVIEWS / "Courts.png")
    hero = [cards[s, "7"] for s in SUITS]
    hero += [cards["Spade", "J"], cards["Heart", "Q"], cards["Diamond", "K"], cards["Club", "K"]]
    sheet(hero, 4, (306, 432)).save(PREVIEWS / "Overview.png")
    sheet(list(cards.values()), 13, (102, 144), 10).save(PREVIEWS / "All52-102x144.png")
    sheet([cards[s, r] for s in SUITS for r in ("10", "J", "Q", "K")],
          4, (102, 144), 12).save(PREVIEWS / "IndexCheck-102x144.png")
    # Store one simple asset mapping plus the shared layout; no 52-image dependency.
    manifest = {"layout": LAYOUT, "rankInk": INK, "artwork": records,
                "ranks": list(RANKS), "suits": list(SUITS)}
    manifest["rankBoxUnits"] = "pixels, top-left origin"
    manifest["base"] = "Elements/Base.png"
    manifest["rankTemplate"] = "Elements/Ranks/{rank}.png"
    manifest["suitTemplate"] = "Elements/Suits/{suit}.png"
    (ROOT / "layout.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    source_hashes = {p.name: hashlib.sha256(p.read_bytes()).hexdigest()
                     for p in sorted((ROOT / "Sources").glob("*.png"))}
    magenta_remnants = {}
    for path in ELEMENTS.rglob("*.png"):
        pixels = np.asarray(Image.open(path).convert("RGBA"))
        magenta = ((pixels[:, :, 0] > 200) & (pixels[:, :, 2] > 200)
                   & (pixels[:, :, 1] < 50) & (pixels[:, :, 3] > 128))
        if magenta.any():
            magenta_remnants[path.relative_to(ROOT).as_posix()] = int(magenta.sum())
    assert not magenta_remnants, magenta_remnants
    report = {"combinations": len(cards), "cardSize": SIZE, "artSprites": 16,
              "rankGlyphs": 13, "suitGlyphs": 4, "baseImages": 1,
              "allSpritesRGBA": all(Image.open(p).mode == "RGBA" for p in ELEMENTS.rglob("*.png")),
              "checks": checks, "sourceHashes": source_hashes,
              "opaqueMagentaRemnants": magenta_remnants,
              "unityPlayMode": "Not run; artwork and offline composition only."}
    (ROOT / "validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"Prepared 16 artwork sprites + 13 rank glyphs + 4 suit glyphs + base in {ELEMENTS}")
    print(f"Composed and checked {len(cards)} combinations. Previews: {PREVIEWS}")


if __name__ == "__main__":
    main()
