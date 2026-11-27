"""Check the pixel constraints of a generated sprite, independent of its visual quality."""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.ndimage import binary_erosion


def check(path, size, limit, silhouette, palette):
    with Image.open(path) as im:
        if im.format != "PNG" or im.mode != "RGBA" or im.size != size:
            raise ValueError(f"Expected RGBA PNG {size}, got {im.format} {im.mode} {im.size}")
        pixels = np.asarray(im)
    alpha = pixels[:, :, 3]
    if not np.array_equal(np.unique(alpha), [0, 255]):
        raise ValueError("Expected both opaque and transparent pixels, with no partial alpha.")
    inside = alpha == 255
    border = inside & ~binary_erosion(inside)
    if pixels[border, :3].any():
        raise ValueError("Exposed border pixels must be black.")
    actual = {tuple(c) for c in np.unique(pixels[inside, :3], axis=0)}
    if len(actual) > limit:
        raise ValueError(f"{len(actual)} opaque colors exceed limit {limit}.")
    if silhouette:
        with Image.open(silhouette) as mask:
            if not np.array_equal(inside, np.asarray(mask.convert("L")) >= 128):
                raise ValueError("Alpha differs from the supplied silhouette.")
    if palette:
        with Image.open(palette) as swatches:
            allowed = {tuple(c) for c in np.asarray(swatches.convert("RGB")).reshape(-1, 3)}
        if actual - (allowed | {(0, 0, 0)}):
            raise ValueError("Sprite contains a color outside the supplied palette.")
    return f"{path.name}: {size[0]}x{size[1]}, {len(actual)} colors, {inside.sum()} opaque, {border.sum()} black boundary pixels"


if __name__ == "__main__":
    cli = argparse.ArgumentParser(description=__doc__)
    cli.add_argument("png", type=Path, nargs="+")
    cli.add_argument("--size", nargs=2, required=True, type=int, metavar=("W", "H"))
    cli.add_argument("--colors", type=int, default=18)
    cli.add_argument("--silhouette", type=Path)
    cli.add_argument("--palette", type=Path)
    args = cli.parse_args()
    for image in args.png:
        print(check(image, tuple(args.size), args.colors, args.silhouette, args.palette))
