"""Encode recorded Unity PNG frames with their observed game-time intervals."""
import csv
import json
import sys
from pathlib import Path

from PIL import Image

source = Path(sys.argv[1]).resolve()
output = Path(__file__).resolve().parent
with (source / "frames.csv").open(encoding="utf-8-sig", newline="") as stream:
    rows = list(csv.DictReader(stream))
assert len(rows) > 2, "No recorded animation frames"
paths = [source / f"frame-{int(row['frame']):04d}.png" for row in rows]
times = [float(row["game_time"]) for row in rows]
assert all(b > a for a, b in zip(times, times[1:])), "Nonmonotonic frame times"
durations = [max(10, round((b - a) * 100) * 10) for a, b in zip(times, times[1:])]
durations.append(500)  # Hold the final empty frame before the preview loops.
frames = []
for path in paths:
    with Image.open(path) as image:
        frames.append(image.convert("RGB").quantize(colors=256))
target = output / "draw-expiry-preview.gif"
frames[0].save(target, save_all=True, append_images=frames[1:], duration=durations,
               loop=0, optimize=False, disposal=2)
with Image.open(target) as image:
    assert image.n_frames > 2 and image.size == frames[0].size
with (output / "frames.csv").open("w", encoding="utf-8", newline="") as stream:
    writer = csv.DictWriter(stream, fieldnames=rows[0].keys())
    writer.writeheader()
    writer.writerows(rows)
(output / "preview-info.json").write_text(json.dumps({
    "source_frames": len(rows), "size": frames[0].size,
    "observed_game_seconds": times[-1] - times[0],
    "encoded_milliseconds": sum(durations),
    "final_empty_preview_hold_milliseconds": 500,
    "line_lifetime_test_seconds": 2.4, "warning_fraction_test": 0.9,
    "warning_period_test_seconds": [0.4, 0.08], "expiry_seconds": 0.65,
}, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps({"frames": len(rows), "duration_ms": sum(durations),
                  "gif": str(target), "bytes": target.stat().st_size}))
