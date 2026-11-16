"""Encode unmodified Unity camera captures at their recorded game times."""
import json
import shutil
from pathlib import Path

from PIL import Image


def main():
    repo = Path(__file__).resolve().parents[4]
    output = Path(__file__).resolve().parent
    logs = repo / "project/Temp/GuardVfxVerification/Logs"
    report = json.loads((logs / "GuardLaserVerification-result.json").read_text(encoding="utf-8-sig"))
    if not report["success"]:
        raise RuntimeError(report["error"])
    frames = sorted((logs / "GuardLaserVerification/sequence").glob("frame-*.png"))
    times = report["sequenceFrameTimes"]
    if len(frames) != len(times) or len(frames) < 2:
        raise RuntimeError("Captured frames and game-time samples must match")

    images = [Image.open(frame).convert("RGB") for frame in frames]
    if any(im.size != (960, 720) for im in images):
        raise RuntimeError("Expected the actual 960x720 camera captures")
    palette_source = Image.new("RGB", (240 * 4, 180 * 4))
    for i in range(16):
        sample = images[round((len(images) - 1) * i / 15)]
        palette_source.paste(sample.resize((240, 180)), ((i % 4) * 240, (i // 4) * 180))
    palette = palette_source.quantize(colors=256)
    encoded = [im.quantize(palette=palette, dither=Image.Dither.NONE) for im in images]
    boundaries = [round((time - times[0]) * 100) for time in times]
    durations = [max(10, (b - a) * 10) for a, b in zip(boundaries, boundaries[1:])]
    durations.append(round((times[-1] - times[-2]) * 100) * 10)
    gif_path = output / "guard-eye-laser.gif"
    encoded[0].save(gif_path, save_all=True, append_images=encoded[1:], duration=durations,
                    loop=0, optimize=False, disposal=2)

    shutil.copy2(logs / "GuardLaserVerification-result.json", output / "unity-validation.json")
    for name in ("charging-player-camera.png", "firing-full-boss-inspection.png", "wall-clipped-player-camera.png"):
        shutil.copy2(logs / "GuardLaserVerification" / name, output / name)
    shutil.copy2(repo / "project/Temp/GuardVfxVerification/Assets/Art/Enemy/Guard/guard-laser-glow.png",
                 output / "guard-laser-glow.png")
    with Image.open(gif_path) as decoded:
        duration = 0
        for index in range(decoded.n_frames):
            decoded.seek(index)
            duration += decoded.info["duration"]
        validation = {
            "raw_frames": len(frames), "encoded_frames": decoded.n_frames,
            "size": list(decoded.size), "duration_ms": duration, "file_bytes": gif_path.stat().st_size,
            "source_game_seconds": times[-1] - times[0],
            "camera": "Actual Player-following GameCamera; controlled Player position fixture; unchanged camera settings",
            "inspection": "firing-full-boss-inspection.png temporarily uses a named review anchor",
            "image_processing": "Only shared-palette GIF encoding; no compositing, recoloring or frame interpolation",
            "maximum_aim_error": max(item["error"] for item in report["samples"]),
            "maximum_irregular_aim_error": max(item["error"] for item in report["irregularSamples"]),
        }
    (output / "preview-validation.json").write_text(json.dumps(validation, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(validation, ensure_ascii=False))


if __name__ == "__main__":
    main()
