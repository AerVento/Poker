"""Check the released deck's pixels, layout invariants and Unity asset identities."""

import sys
sys.dont_write_bytecode = True

import hashlib
import json
from PIL import Image, ImageChops
from compose import ROOT, SIZE, SUITS, RANKS, COURTS, LAYOUT, create_base


destination = ROOT.parents[2] / "Assets/Sprites/CardFaces/JewelCourt"
report = json.loads((ROOT / "export-validation.json").read_text(encoding="utf-8"))
base = Image.open(ROOT / "Sources/Base-v2.png").convert("RGBA").resize(SIZE, Image.Resampling.LANCZOS)
base.putalpha(create_base().getchannel("A"))
guids, hashes = set(), set()
art_heights, rank_areas = [], []
for suit in SUITS:
    reference_jewel = None
    reference_suit = None
    for rank in RANKS:
        path = destination / f"{suit.lower()}-{rank.lower()}.png"
        card = Image.open(path)
        assert card.mode == "RGBA" and card.size == SIZE, path
        assert ImageChops.difference(card.getchannel("A"), base.getchannel("A")).getbbox() is None, path
        record = report["faces"][path.name]
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        assert digest == record["sha256"], path
        hashes.add(digest)
        guid = next(line for line in path.with_suffix(".png.meta").read_text().splitlines() if line.startswith("guid:"))
        assert guid not in guids, path
        guids.add(guid)
        bounds = record["bounds"]
        rx, ry, rr, rb = bounds["rank"]
        rank_areas.append((rr - rx) * (rb - ry))
        left, top, right, bottom = LAYOUT["rankBox"]
        assert left <= rx < rr <= right and top <= ry < rb <= bottom, path
        assert abs((rx + rr) - (left + right)) <= 1, path
        assert abs((ry + rb) - (top + bottom)) <= 1, path
        assert abs(rank_areas[-1] / LAYOUT["rankArea"] - 1) < .01, path
        assert rb < bounds["suit"][1], path
        ax, ay, ar, ab = bounds["art"]
        assert ay == 648 and ab == 1068 and ar == 744, path
        assert ab - ay == 420, path
        # Measure visible ink in the actual exported picture, not just placement metadata.
        art_difference = ImageChops.difference(card.convert("RGB"), base.convert("RGB")).crop(LAYOUT["artBox"])
        visible = art_difference.convert("L").point(lambda v: 255 if v > 30 else 0).getbbox()
        assert visible is not None, path
        art_heights.append(visible[3] - visible[1])
        assert 418 <= art_heights[-1] <= 420, (path, visible)
        lower = card.crop((0, 600, SIZE[0], SIZE[1])).tobytes()
        if rank not in COURTS:
            if reference_jewel is None:
                reference_jewel = lower
            assert lower == reference_jewel, (path, "A through 10 must reuse identical artwork")
        suit_pixels = card.crop(LAYOUT["suitBox"]).tobytes()
        if reference_suit is None:
            reference_suit = suit_pixels
        assert suit_pixels == reference_suit, (path, "Suit position drift")
        # All pixels outside the three reserved regions must remain the common base.
        background = card.copy()
        for box in (LAYOUT["rankBox"], LAYOUT["suitBox"], LAYOUT["artBox"]):
            background.paste(base.crop(box), box[:2])
        assert background.tobytes() == base.tobytes(), (path, "Decoration changed")

assert len(guids) == len(hashes) == len(report["faces"]) == 52
print(f"PASS: 52 RGBA faces and unique GUIDs/hashes; common background/alpha; "
      f"A-10 jewels identical; suit position fixed; actual art heights {min(art_heights)}-{max(art_heights)} px; "
      f"rank areas {min(rank_areas)}-{max(rank_areas)} square pixels in one shared slot.")
