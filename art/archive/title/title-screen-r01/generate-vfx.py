"""Generate the separate Title overlay; never read or modify the logo pixels."""
from pathlib import Path
import math
import struct
import zlib

SIZE = 256
rows = bytearray()
for y in range(SIZE):
    rows.append(0)
    for x in range(SIZE):
        dx = (x + 0.5 - SIZE / 2) / (SIZE / 2)
        dy = (y + 0.5 - SIZE / 2) / (SIZE / 2)
        radius = math.hypot(dx, dy)
        horizontal = math.exp(-abs(dy) * 75) * max(0, 1 - abs(dx)) ** 1.2
        vertical = math.exp(-abs(dx) * 75) * max(0, 1 - abs(dy)) ** 1.2
        core = math.exp(-radius * radius * 600)
        halo = 0.32 * math.exp(-radius * radius * 19)
        intensity = min(1, max(horizontal, vertical) * 1.65 + core + halo)
        white = min(1, max(horizontal, vertical) * 3 + core)
        alpha = round(255 * intensity * max(0, min(1, (1 - radius) * 10)))
        rows.extend((round(110 + 145 * white), round(210 + 45 * white), 255, alpha))

def chunk(kind, data):
    return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data))

png = b'\x89PNG\r\n\x1a\n'
png += chunk(b'IHDR', struct.pack('>IIBBBBB', SIZE, SIZE, 8, 6, 0, 0, 0))
png += chunk(b'IDAT', zlib.compress(rows, 9)) + chunk(b'IEND', b'')
Path(__file__).with_name('title-blade-glint-vfx.png').write_bytes(png)
