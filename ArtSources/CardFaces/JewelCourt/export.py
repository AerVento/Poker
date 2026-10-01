"""Bake the approved ornate v2 design into 52 Unity-ready face PNGs."""

import sys
sys.dont_write_bytecode = True

import hashlib
import json
from PIL import Image
from compose import ROOT, PREVIEWS, SUITS, RANKS, SIZE, LAYOUT, create_base, compose, sheet


destination = ROOT.parents[2] / "Assets/Sprites/CardFaces/JewelCourt"
if not destination.is_dir():
    raise SystemExit("Create Assets/Sprites/CardFaces/JewelCourt in Unity Editor first.")
base = Image.open(ROOT / "Sources/Base-v2.png").convert("RGBA").resize(SIZE, Image.Resampling.LANCZOS)
base.putalpha(create_base().getchannel("A"))
cards, records = [], {}
for suit in SUITS:
    for rank in RANKS:
        card, bounds = compose(suit, rank, base)
        path = destination / f"{suit.lower()}-{rank.lower()}.png"
        card.save(path)
        records[path.name] = {"sha256": hashlib.sha256(path.read_bytes()).hexdigest(), "bounds": bounds}
        cards.append(card)
assert len(records) == 52
assert len({record["sha256"] for record in records.values()}) == 52
sheet(cards, 13, (102, 144), 10).save(PREVIEWS / "Released52-v2.png")
sheet([cards[s * 13 + RANKS.index(r)] for s in range(4) for r in ("9", "10", "J", "Q", "K")],
      5, (204, 288), 14).save(PREVIEWS / "UnifiedLayout.png")
(ROOT / "layout.json").write_text(json.dumps({
    **json.loads((ROOT / "layout.json").read_text(encoding="utf-8")),
    "layout": LAYOUT,
}, indent=2) + "\n", encoding="utf-8")
(ROOT / "export-validation.json").write_text(json.dumps({
    "count": 52, "dimensions": SIZE, "base": "Sources/Base-v2.png",
    "output": "Assets/Sprites/CardFaces/JewelCourt", "layout": LAYOUT, "faces": records,
}, indent=2) + "\n", encoding="utf-8")
print(f"Exported {len(records)} unique ornate v2 faces to {destination}")
