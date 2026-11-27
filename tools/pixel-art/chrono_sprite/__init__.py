import numpy as np
import torch
from PIL import Image, ImageOps
from scipy import ndimage


NEAREST = Image.Resampling.NEAREST


def rgb_image(tensor):
    data = tensor.detach().cpu().numpy().clip(0, 1)
    return Image.fromarray(np.rint(data[..., :3] * 255).astype(np.uint8))


def tensor_image(image):
    return torch.from_numpy(np.asarray(image).astype(np.float32) / 255)


class ChronoReferenceCanvas:
    @classmethod
    def INPUT_TYPES(cls):
        return {"required": {"image": ("IMAGE",)}, "optional": {"transparent": ("MASK",)}}

    RETURN_TYPES = ("IMAGE",)
    FUNCTION = "prepare"
    CATEGORY = "Chrono/Sprites"

    def prepare(self, image, transparent=None):
        canvases = []
        for index, frame in enumerate(image):
            source = rgb_image(frame)
            if transparent is not None:
                values = transparent[min(index, len(transparent) - 1)].cpu().numpy()
                mask = Image.fromarray(np.rint(values.clip(0, 1) * 255).astype(np.uint8))
                source.paste("white", (0, 0, *source.size), mask.resize(source.size, NEAREST))
            canvases.append(tensor_image(ImageOps.pad(source, (768, 768), NEAREST, color="white")))
        return (torch.stack(canvases),)


def foreground_on_flat_background(image, tolerance):
    # Infer the backdrop from all four corners and remove only connected exterior pixels.
    values = np.asarray(image, dtype=np.int16)
    patch = max(1, min(image.size) // 40)
    corners = [values[:patch, :patch], values[-patch:, :patch],
               values[:patch, -patch:], values[-patch:, -patch:]]
    background = np.median(np.concatenate([c.reshape(-1, 3) for c in corners]), axis=0)
    near = np.max(np.abs(values - background), axis=2) <= tolerance
    border = np.zeros(near.shape, dtype=bool)
    border[[0, -1], :] = near[[0, -1], :]
    border[:, [0, -1]] = near[:, [0, -1]]
    if not border.any():
        raise ValueError("Use a flat background or connect a black-and-white silhouette.")
    foreground = ~ndimage.binary_propagation(border, mask=near)
    return Image.fromarray(foreground.astype(np.uint8) * 255)


def grid_from_generated(image, size, tolerance):
    mask = foreground_on_flat_background(image, tolerance)
    # Subpixel dust far from the sprite can move its bounding box by half a canvas.
    labels, count = ndimage.label(np.asarray(mask) > 0)
    if count:
        areas = np.bincount(labels.ravel())
        minimum = max(1, int(image.width * image.height / (size[0] * size[1]) / 12))
        keep = areas >= min(minimum, int(areas[1:].max()))
        keep[0] = False
        mask = Image.fromarray(keep[labels].astype(np.uint8) * 255)
    box = mask.getbbox()
    if box is None:
        raise ValueError("Empty sprite. Lower background_tolerance or supply a silhouette.")
    crop = image.crop(box)
    max_size = (size[0] - 2, size[1] - 2)
    scale = min(max_size[0] / crop.width, max_size[1] / crop.height)
    fitted = crop.resize((max(1, round(crop.width * scale)), max(1, round(crop.height * scale))), NEAREST)
    origin = ((size[0] - fitted.width) // 2, (size[1] - fitted.height) // 2)
    colors = Image.new("RGB", size)
    alpha = Image.new("L", size)
    colors.paste(fitted, origin)
    alpha.paste(mask.crop(box).resize(fitted.size, NEAREST), origin)
    return colors, np.asarray(alpha) > 0


def sprite_palette(samples, limit, swatches):
    if swatches is not None:
        swatch_image = rgb_image(swatches[0])
        colors = np.unique(np.asarray(swatch_image).reshape(-1, 3), axis=0)
        colors = colors[np.any(colors != 0, axis=1)]
        if len(colors) >= limit:
            raise ValueError(f"Palette requires {len(colors) + 1} colors including black. Raise max_colors or reduce swatches.")
    else:
        strip = Image.fromarray(samples.reshape(1, -1, 3))
        reduced = strip.quantize(colors=limit - 1, dither=Image.Dither.NONE)
        colors = np.unique(np.asarray(reduced.convert("RGB")).reshape(-1, 3), axis=0)
    return np.vstack([np.zeros((1, 3), dtype=np.uint8), colors])


class ChronoSpriteGrid:
    @classmethod
    def INPUT_TYPES(cls):
        return {
            "required": {
                "image": ("IMAGE",),
                "width": ("INT", {"default": 48, "min": 4, "max": 256}),
                "height": ("INT", {"default": 48, "min": 4, "max": 256}),
                "max_colors": ("INT", {"default": 18, "min": 2, "max": 64}),
                "background_tolerance": ("INT", {"default": 104, "min": 1, "max": 160}),
            },
            "optional": {"silhouette": ("IMAGE",), "palette": ("IMAGE",)},
        }

    RETURN_TYPES = ("IMAGE",)
    FUNCTION = "render"
    CATEGORY = "Chrono/Sprites"

    def render(self, image, width, height, max_colors, background_tolerance,
               silhouette=None, palette=None):
        result = []
        for index, frame in enumerate(image):
            source = rgb_image(frame)
            if silhouette is None:
                colors, inside = grid_from_generated(source, (width, height), background_tolerance)
            else:
                guide = rgb_image(silhouette[min(index, len(silhouette) - 1)])
                if guide.size != (width, height):
                    raise ValueError(f"Provide a white-on-black silhouette of exactly {width} x {height} pixels.")
                inside = np.asarray(guide.convert("L")) >= 128
                colors = ImageOps.fit(source, guide.size, NEAREST)
            if not inside.any():
                raise ValueError("Empty silhouette at target resolution.")
            values = np.asarray(colors)
            samples = values[inside]
            allowed = sprite_palette(samples, max_colors, palette)
            distances = samples.astype(np.int32)[:, None, :] - allowed.astype(np.int32)
            rgb = np.zeros((height, width, 4), dtype=np.uint8)
            rgb[inside, :3] = allowed[np.sum(distances * distances, axis=2).argmin(axis=1)]
            rgb[inside, 3] = 255
            edge = inside & ~ndimage.binary_erosion(inside)
            rgb[edge, :3] = 0
            result.append(tensor_image(Image.fromarray(rgb)))
        return (torch.stack(result),)


NODE_CLASS_MAPPINGS = {"ChronoSpriteGrid": ChronoSpriteGrid, "ChronoReferenceCanvas": ChronoReferenceCanvas}
NODE_DISPLAY_NAME_MAPPINGS = {"ChronoSpriteGrid": "Chrono · Pixel PNG", "ChronoReferenceCanvas": "Chrono · Reference Canvas"}
