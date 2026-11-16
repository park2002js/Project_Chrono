"""Verify the edited native master, unchanged old layers, and separate glow support."""
from pathlib import Path
import hashlib
import json
import math
import struct
import zlib
from PIL import Image, ImageChops

ROOT = Path(__file__).resolve().parent
REPOSITORY = ROOT.parents[3]


def read_native(path):
    data = path.read_bytes()
    offset, names, images, metadata = 144, [], {}, {}
    for _ in range(struct.unpack_from('<H', data, 134)[0]):
        length, kind = struct.unpack_from('<IH', data, offset)
        if kind == 0x2004:
            size = struct.unpack_from('<H', data, offset + 22)[0]
            name = data[offset + 24:offset + 24 + size].decode()
            names.append(name)
            metadata[name] = data[offset + 6:offset + 22].hex()
        elif kind == 0x2005:
            layer, x, y, opacity, cel_type = struct.unpack_from('<HhhBH', data, offset + 6)
            assert cel_type == 2 and opacity == 255
            width, height = struct.unpack_from('<HH', data, offset + 22)
            cel = Image.frombytes('RGBA', (width, height), zlib.decompress(data[offset + 26:offset + length]))
            canvas = Image.new('RGBA', (200, 80))
            canvas.paste(cel, (x, y))
            images[layer] = canvas
        offset += length
    return data, names, images, metadata


old, old_names, old_images, old_metadata = read_native(ROOT / 'before-title-logo-17-natural-cut.aseprite')
current, names, images, metadata = read_native(ROOT / 'title-logo-17-natural-cut.aseprite')
assert struct.unpack_from('<HHHH', old, 6) == struct.unpack_from('<HHHH', current, 6) == (1, 200, 80, 32)
assert struct.unpack_from('<H', old, 136) == struct.unpack_from('<H', current, 136)
assert len(names) == 11 and names[-1] == old_names[-1]
for index, name in enumerate(old_names):
    assert images[names.index(name)].tobytes() == old_images[index].tobytes(), name
    assert metadata[name] == old_metadata[name], name

mask = {(x, y) for layer in (5, 8) for y in range(80) for x in range(200) if old_images[layer].getpixel((x, y))[3]}
outline = images[names.index('11 White grip and saya outline 1px')]
white = {(x, y) for y in range(80) for x in range(200) if outline.getpixel((x, y))[3]}
assert len(white) == 96 and not white.intersection(mask)
for point in white:
    x, y = point
    assert outline.getpixel(point) == (255, 255, 255, 255)
    assert any((x + dx, y + dy) in mask for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))

composite = Image.new('RGBA', (200, 80))
for index in range(len(names)):
    composite = Image.alpha_composite(composite, images[index])
export = Image.open(ROOT / 'title-logo-17-natural-cut.png').convert('RGBA')
before_export = Image.open(ROOT / 'before-title-logo-17-natural-cut.png').convert('RGBA')
assert composite.tobytes() == export.tobytes()
changed = {(x, y) for y in range(80) for x in range(200) if export.getpixel((x, y)) != before_export.getpixel((x, y))}
assert changed == white
master = REPOSITORY / 'art/archive/title/logo_17_natural_cut_r06'
assert (master / 'title-logo-17-natural-cut.aseprite').read_bytes() == (ROOT / 'title-logo-17-natural-cut.aseprite').read_bytes()
assert (master / 'title-logo-17-natural-cut.png').read_bytes() == (ROOT / 'title-logo-17-natural-cut.png').read_bytes()
assert (REPOSITORY / 'project/Assets/Art/UI/Title/title-logo.png').read_bytes() == (ROOT / 'title-logo-17-natural-cut.png').read_bytes()

vx, vy = 193, -26
length = math.hypot(vx, vy)
phases = []
for index in range(3):
    image = Image.open(ROOT / f'title-fissure-glow-{index}.png').convert('RGBA')
    assert image.size == (200, 80)
    distances = []
    for y in range(80):
        for x in range(200):
            r, g, b, alpha = image.getpixel((x, y))
            if not alpha:
                continue
            distance = abs((x - 3) * vy - (y - 48) * vx) / length
            along = ((x - 3) * vx + (y - 48) * vy) / length
            assert distance < 3 and 0 < along < length
            assert b == 255 and r < g < b
            distances.append(distance)
    phases.append({'phase': index, 'alphaBounds': image.getbbox(), 'alphaPixelCount': len(distances),
                   'maximumPerpendicularDistance': max(distances)})
for index in range(3):
    assert ImageChops.difference(Image.open(ROOT / f'title-fissure-glow-{index}.png'),
                                Image.open(ROOT / f'title-fissure-glow-{(index + 1) % 3}.png')).getbbox()

files = [ROOT / 'title-logo-17-natural-cut.aseprite', ROOT / 'title-logo-17-natural-cut.png']
files += [ROOT / f'title-fissure-glow-{i}.png' for i in range(3)]
result = {'allChecksPassed': True, 'originalLayerCount': len(old_names), 'originalLayerPixelsAndMetadataPreserved': True,
          'originalCrossRemainsTopmost': True, 'newNativeLayerCount': len(names), 'outlineWhitePixelCount': len(white),
          'changedCompositePixelCount': len(changed), 'changedCompositeBounds': ImageChops.difference(export, before_export).getbbox(),
          'frameDurationMilliseconds': struct.unpack_from('<H', current, 136)[0], 'sizeFramesDepth': [1, 200, 80, 32],
          'sourceAndUnityPngIdentical': True, 'phases': phases,
          'sha256': {path.name: hashlib.sha256(path.read_bytes()).hexdigest() for path in files}}
(ROOT / 'asset-validation.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps(result, indent=2))
