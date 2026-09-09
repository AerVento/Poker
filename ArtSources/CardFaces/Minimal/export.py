"""Adapt Bad Hand Studio's selected SVG deck; never synthesizes card artwork.
Requires: resvg-py 0.5.0, fonttools; Arial Bold supplied by Windows.
Run from the repository root. Original downloads and permissions are beside this script.
"""
from pathlib import Path
import copy, io, os, xml.etree.ElementTree as ET
import resvg_py
from fontTools.ttLib import TTFont
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.boundsPen import BoundsPen
from fontTools.pens.transformPen import TransformPen
from fontTools.svgLib.path import parse_path

ROOT = Path(__file__).resolve().parents[3]
SOURCE = Path(__file__).resolve().parent
OUTPUT = ROOT / 'Assets/Sprites/CardFaces/Minimal'
SVG = '{http://www.w3.org/2000/svg}'
INK = '{http://www.inkscape.org/namespaces/inkscape}'
ET.register_namespace('', SVG[1:-1])
font = TTFont(Path(os.environ['WINDIR']) / 'Fonts/arialbd.ttf')
glyphs, cmap = font.getGlyphSet(), font.getBestCmap()
source = ET.parse(SOURCE / 'SimplifiedPlayingCards.svg').getroot()

def path_and_bounds(label):
    pen, bounds, advance = SVGPathPen(glyphs), BoundsPen(glyphs), 0
    for char in label:
        glyph = glyphs[cmap[ord(char)]]
        transform = (1, 0, 0, -1, advance, 0)
        glyph.draw(TransformPen(pen, transform))
        glyph.draw(TransformPen(bounds, transform))
        advance += glyph.width
    return pen.getCommands(), bounds.bounds

def placed_path(parent, data, bounds, x, y, width, height, color):
    left, top, right, bottom = bounds
    scale = min(width / (right-left), height / (bottom-top))
    ET.SubElement(parent, SVG+'path', {'d': data, 'fill': color,
        'transform': f'translate({x-left*scale:.6f} {y-top*scale:.6f}) scale({scale:.8f})'})

OUTPUT.mkdir(parents=True, exist_ok=True)
for row, suit in enumerate(['Club', 'Heart', 'Spade', 'Diamond']):
    pip = next(e for e in source.iter(SVG+'path') if e.get(INK+'label') == suit)
    pen = BoundsPen(None); parse_path(pip.get('d'), pen)
    texts = [e for e in source.iter(SVG+'text') if int(float(e.get('y')) // 89) == row]
    for text in texts:
        label = ''.join(text.itertext()).strip()
        face = ET.Element(SVG+'svg', {'width':'408','height':'576','viewBox':'0 0 408 576'})
        ET.SubElement(face, SVG+'rect', {'width':'408','height':'576','rx':'12','fill':'white'})
        color = '#ff0000' if suit in ('Heart','Diamond') else '#000000'
        data, bounds = path_and_bounds(label)
        placed_path(face, data, bounds, 40, 40, 230, 140, color)
        left,top,right,bottom = pen.bounds
        scale = min(148/(right-left),148/(bottom-top))
        placed_path(face, pip.get('d'), pen.bounds, 368-(right-left)*scale,
                    536-(bottom-top)*scale, 148,148,color)
        (OUTPUT / f'{suit}_{label}.png').write_bytes(resvg_py.svg_to_bytes(svg_string=ET.tostring(face,encoding='unicode')))

# Third original back: red geometric linework. Crop its SVG viewport, preserving artwork.
back = ET.parse(SOURCE / 'CardBacks.svg').getroot()
back.set('viewBox', '135.46 0 67.73 94.19')
back.set('width','408'); back.set('height','576'); back.set('preserveAspectRatio','none')
(OUTPUT / 'Back.png').write_bytes(resvg_py.svg_to_bytes(svg_string=ET.tostring(back,encoding='unicode')))
print('Exported 52 card fronts and the selected original back to', OUTPUT)
