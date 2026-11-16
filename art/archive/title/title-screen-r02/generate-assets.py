"""Derive a 1px editable outline and three separate fissure-glow phases.

The fixed before-master is the input; this script never overwrites the master.
Use apply-outline.lua in Aseprite to add the generated outline as a native layer.
"""
from pathlib import Path
import json
import math
import struct
import zlib
from PIL import Image

ROOT = Path(__file__).resolve().parent
SIZE = (200, 80)


def native_layers(path):
    data = path.read_bytes()
    assert struct.unpack_from('<HHHH', data, 6) == (1, 200, 80, 32)
    offset = 144
    names, cels = [], {}
    for _ in range(struct.unpack_from('<H', data, 134)[0]):
        length, kind = struct.unpack_from('<IH', data, offset)
        if kind == 0x2004:
            count = struct.unpack_from('<H', data, offset + 22)[0]
            names.append(data[offset + 24:offset + 24 + count].decode('utf-8'))
        elif kind == 0x2005:
            layer, x, y, opacity, cel_type = struct.unpack_from('<HhhBH', data, offset + 6)
            assert cel_type == 2 and opacity == 255
            width, height = struct.unpack_from('<HH', data, offset + 22)
            cel = Image.frombytes('RGBA', (width, height), zlib.decompress(data[offset + 26:offset + length]))
            canvas = Image.new('RGBA', SIZE)
            canvas.paste(cel, (x, y))
            cels[layer] = canvas
        offset += length
    return names, cels


names, layers = native_layers(ROOT / 'before-title-logo-17-natural-cut.aseprite')
assert names[5] == '06 Longer plain grip' and names[8] == '09 Long tapered black-blue saya'
mask = {(x, y) for layer in (5, 8) for y in range(SIZE[1]) for x in range(SIZE[0]) if layers[layer].getpixel((x, y))[3]}
outline = {(x + dx, y + dy) for x, y in mask for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))
           if 0 <= x + dx < SIZE[0] and 0 <= y + dy < SIZE[1]} - mask
outline = {point for point in outline if not layers[6].getpixel(point)[3] and not layers[7].getpixel(point)[3]}
outline_image = Image.new('RGBA', SIZE)
for point in outline:
    outline_image.putpixel(point, (255, 255, 255, 255))
outline_image.save(ROOT / 'grip-saya-outline-1px.png')

# Match the actual diagonal cut and fade to transparent within three native pixels.
start = (3.5, 48.5)
end = (196.5, 22.5)
vx, vy = end[0] - start[0], end[1] - start[1]
length = math.hypot(vx, vy)
ux, uy = vx / length, vy / length
blade_distance = (130.5 - start[0]) * ux + (31.5 - start[1]) * uy
validation = {'size': SIZE, 'centerlinePixelCenters': [start, end], 'halfWidthNativePixels': 3,
              'endFadeNativePixels': 7, 'bladePixelCenter': [130.5, 31.5], 'phases': []}

for phase in range(3):
    image = Image.new('RGBA', SIZE)
    occupied = []
    for y in range(SIZE[1]):
        for x in range(SIZE[0]):
            dx, dy = x + 0.5 - start[0], y + 0.5 - start[1]
            along = dx * ux + dy * uy
            across = abs(dx * uy - dy * ux)
            if along <= 0 or along >= length or across >= 3:
                continue
            width_fade = math.cos(across / 3 * math.pi / 2) ** 2
            end_fade = min(1, along / 7, (length - along) / 7)
            distance = abs(along - blade_distance)
            near_blade = math.exp(-distance / 48)
            wave = max(0, math.cos(2 * math.pi * (distance / 140 - phase / 3))) ** 4
            alpha = round(255 * width_fade * end_fade * min(0.98, 0.23 + 0.2 * near_blade + 0.55 * wave))
            core = math.exp(-(across / 0.75) ** 2)
            if alpha:
                image.putpixel((x, y), (round(24 + 80 * core), round(95 + 118 * core), 255, alpha))
                occupied.append((x, y, across))
    image.save(ROOT / f'title-fissure-glow-{phase}.png')
    validation['phases'].append({'index': phase, 'alphaBounds': image.getbbox(), 'alphaPixelCount': len(occupied),
                                 'maxDistanceNativePixels': max(p[2] for p in occupied),
                                 'alphaMax': max(pixel[3] for pixel in image.getdata())})

(ROOT / 'glow-generation-validation.json').write_text(json.dumps(validation, indent=2), encoding='utf-8')
print(json.dumps(validation, indent=2))
