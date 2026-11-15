"""Reproduce exported greatsword transform curves; analysis only, not game code."""
import csv
import hashlib
import json
import math
import re
from pathlib import Path

ROOT = next(path for path in Path(__file__).resolve().parents if (path / "AGENTS.md").is_file())
SOURCE = ROOT / "docs/reference/sephiria/assets/ExportedProject/Assets/AnimationClip"
OUTPUT = Path(__file__).resolve().parent


def vector(text):
    return {axis: float(value) for axis, value in re.findall(r"([xyz]): ([^,}]+)", text)}


def parse_curve(text, name):
    section = re.search(r"^  " + name + r":\n(.*?)(?=^  m_)", text, re.M | re.S).group(1)
    path = re.search(r"^    path: (.*)$", section, re.M).group(1)
    keys = []
    for block in re.findall(r"      - serializedVersion: 3\n(.*?)(?=      - serializedVersion:|      m_PreInfinity)", section, re.S):
        key = {"time": float(re.search(r"time: (.+)", block).group(1))}
        for field in ("value", "inSlope", "outSlope"):
            key[field] = vector(re.search(field + r": (.+)", block).group(1))
        key["weightedMode"] = int(re.search(r"weightedMode: (\d+)", block).group(1))
        assert key["weightedMode"] == 0, "Weighted curve requires a different evaluator"
        keys.append(key)
    return {"path": path, "keys": keys}


def evaluate(keys, axis, time):
    # Snap samples on authored frame boundaries to their serialized key values.
    for key in keys:
        if abs(time - key["time"]) < 1e-7:
            return key["value"][axis]
    if time <= keys[0]["time"]:
        return keys[0]["value"][axis]
    if time >= keys[-1]["time"]:
        return keys[-1]["value"][axis]
    for a, b in zip(keys, keys[1:]):
        if a["time"] < time < b["time"]:
            duration = b["time"] - a["time"]
            u = (time - a["time"]) / duration
            v0, v1 = a["value"][axis], b["value"][axis]
            m0, m1 = a["outSlope"][axis], b["inSlope"][axis]
            if not math.isfinite(m0) or not math.isfinite(m1):
                return v0
            return ((2*u**3 - 3*u**2 + 1)*v0 + (u**3 - 2*u**2 + u)*duration*m0
                    + (-2*u**3 + 3*u**2)*v1 + (u**3 - u**2)*duration*m1)
    raise AssertionError(time)


def json_clean(value):
    if isinstance(value, float) and not math.isfinite(value):
        return str(value)
    if isinstance(value, dict):
        return {k: json_clean(v) for k, v in value.items()}
    if isinstance(value, list):
        return [json_clean(v) for v in value]
    return value


def main():
    clips = []
    verification = []
    for attack in (1, 2):
        file = SOURCE / f"GreatSword_Attack{attack}.anim"
        text = file.read_text(encoding="utf-8-sig")
        clip = {
            "file": file.relative_to(ROOT).as_posix(),
            "sha256": hashlib.sha256(file.read_bytes()).hexdigest(),
            "sampleRate": float(re.search(r"m_SampleRate: (.+)", text).group(1)),
            "stopTime": float(re.search(r"m_StopTime: (.+)", text).group(1)),
            "euler": parse_curve(text, "m_EulerCurves"),
            "position": parse_curve(text, "m_PositionCurves"),
            "events": [],
        }
        for event in re.findall(r"  - time: (.*?)(?=  - time:|\Z)", text.split("  m_Events:\n")[1], re.S):
            time = float(event.splitlines()[0])
            clip["events"].append({"time": time, "frame30": round(time*30),
                "function": re.search(r"functionName: (.*)", event).group(1),
                "data": re.search(r"data:(.*)", event).group(1).strip(),
                "intParameter": int(re.search(r"intParameter: (.*)", event).group(1))})
        for curve in (clip["position"], clip["euler"]):
            for key in curve["keys"]:
                for axis in "xyz":
                    assert abs(evaluate(curve["keys"], axis, key["time"]) - key["value"][axis]) < 1e-8
        verification.append({"attack": attack, "all_key_endpoint_checks": True,
            "curve_paths_match": clip["position"]["path"] == clip["euler"]["path"],
            "no_scale_or_sprite_curves": "  m_ScaleCurves: []" in text and "  m_PPtrCurves: []" in text})
        for rate in (30, 60):
            rows = []
            previous = None
            for frame in range(round(clip["stopTime"]*rate)+1):
                time = frame/rate
                x = evaluate(clip["position"]["keys"], "x", time)
                y = evaluate(clip["position"]["keys"], "y", time)
                angle = evaluate(clip["euler"]["keys"], "z", time)
                up_x, up_y = 0.15625+x, 0.5+y
                top_x = up_x - 1.375*math.sin(math.radians(angle))
                top_y = up_y + 1.375*math.cos(math.radians(angle))
                tip_x = up_x - 1.28125*math.sin(math.radians(angle))
                tip_y = up_y + 1.28125*math.cos(math.radians(angle))
                rows.append({"frame": frame, "clip_time_seconds": round(time, 9),
                    "local_x": round(x, 9), "local_y": round(y, 9), "local_z": 0,
                    "euler_z_degrees": round(angle, 9),
                    "angle_from_first_degrees": round(angle-clip["euler"]["keys"][0]["value"]["z"], 9),
                    "angle_from_previous_degrees": round(0 if previous is None else angle-previous, 9),
                    "pivot_distance_from_shoulder": round(math.hypot(x,y), 9),
                    "pivot_bearing_from_shoulder_degrees": round(math.degrees(math.atan2(y,x)), 9),
                    "up_aim_player_relative_pivot_x": round(up_x, 9),
                    "up_aim_player_relative_pivot_y": round(up_y, 9),
                    "up_aim_player_relative_sprite_top_center_x": round(top_x, 9),
                    "up_aim_player_relative_sprite_top_center_y": round(top_y, 9),
                    "up_aim_player_relative_opaque_tip_center_x": round(tip_x, 9),
                    "up_aim_player_relative_opaque_tip_center_y": round(tip_y, 9),
                    "is_authored_position_key": any(abs(time-k["time"])<1e-7 for k in clip["position"]["keys"]),
                    "is_authored_euler_key": any(abs(time-k["time"])<1e-7 for k in clip["euler"]["keys"]),
                    "events": "; ".join(f"{e['function']}({e['data'] or e['intParameter']})" for e in clip["events"] if abs(time-e["time"])<1e-7)})
                previous = angle
            with (OUTPUT/f"motion_attack{attack}_{rate}fps.csv").open("w", encoding="utf-8-sig", newline="") as stream:
                writer = csv.DictWriter(stream, rows[0].keys())
                writer.writeheader()
                writer.writerows(rows)
            clip[f"frames{rate}"] = rows
        clips.append(clip)
    result = {"scope": "Static reconstruction of GreatSword_Attack1/2 used by AnimationWrapper layer2; no runtime recording",
        "units": "Position in Shoulder-local Unity units; Z rotation in unwrapped degrees; frame0 starts each clip",
        "interpolation": "Unweighted cubic Hermite; infinite slope segment holds preceding value; key snapping tolerance1e-7 seconds",
        "canonical_player_relative_pose": "Grounded neutral BodyWrapper/AnimationWrapper, root scale(+1,+1,+1), facing up, Shoulder local(0.15625,0.5), Shoulder world Z=0. Sprite top center is rectangle boundary point(0,1.375) from Sprite pivot, not a measured opaque blade tip. PPU16, rect11x28, pivot pixels(5.5,6).",
        "opaque_tip_center": "GreatSword_Tier1.png topmost nonzero-alpha pixel is (5,1) in top-left coordinates. Pixel center is (5.5,26.5) bottom-left; relative to pivot(5.5,6), u=(0,20.5/16)=(0,1.28125).",
        "not_included": "Runtime parent transforms, mirrored facing, Animator transitions, attack-speed changes, hitstop, dynamic avatar movement",
        "clips": clips, "verification": verification}
    (OUTPUT/"motion_curves_and_frames.json").write_text(json.dumps(json_clean(result), ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"clips": [{"file": c["file"], "frames30": len(c["frames30"]), "frames60": len(c["frames60"]), "stopTime": c["stopTime"]} for c in clips], "verification": verification}, indent=2))


if __name__ == "__main__":
    main()
