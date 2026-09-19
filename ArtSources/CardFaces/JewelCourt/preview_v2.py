"""Compose one ornate-base preview using the existing rank, suit and artwork sprites."""

import sys

sys.dont_write_bytecode = True

from PIL import Image
from compose import ROOT, ELEMENTS, PREVIEWS, SIZE, create_base, compose, sheet


base = Image.open(ROOT / "Sources/Base-v2.png").convert("RGBA")
base = base.resize(SIZE, Image.Resampling.LANCZOS)
base.putalpha(create_base().getchannel("A"))
base.save(ELEMENTS / "Base-v2.png")

examples = (("Spade", "7"), ("Diamond", "10"), ("Heart", "Q"), ("Club", "K"))
cards = []
for suit, rank in examples:
    card, bounds = compose(suit, rank, base)
    assert bounds["rank"][3] < bounds["suit"][1]
    assert card.getpixel((0, 0))[3] == 0
    cards.append(card)

destination = PREVIEWS / "OrnateBase-v2.png"
sheet(cards, 4, (408, 576), 24).save(destination)
print(destination)
