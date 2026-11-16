"""Generate original Slash sound drafts with deterministic procedural synthesis."""

import math
from pathlib import Path
import random
import shutil
import struct
import wave

RATE = 48000
ROOT = Path(__file__).resolve().parent
UNITY = ROOT.parents[3] / "project" / "Assets" / "Audio" / "Slash"


def write_sound(name, samples):
    peak = max(abs(sample) for sample in samples)
    pcm = [round(max(-1.0, min(1.0, sample / max(peak, 1e-12))) * 28000) for sample in samples]
    output = ROOT / name
    with wave.open(str(output), "wb") as writer:
        writer.setnchannels(1)
        writer.setsampwidth(2)
        writer.setframerate(RATE)
        writer.writeframes(struct.pack("<" + "h" * len(pcm), *pcm))
    UNITY.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(output, UNITY / name)
    assert (UNITY / name).read_bytes() == output.read_bytes()
    assert max(abs(value) for value in pcm) == 28000
    print(f"{name}: {len(pcm)} samples, {len(pcm) / RATE:.3f}s, mono PCM16 at {RATE}Hz, peak 28000")


def sheath_clack():
    rng = random.Random(617)
    output = []
    previous = 0.0
    for index in range(round(RATE * 0.14)):
        t = index / RATE
        noise = rng.uniform(-1.0, 1.0)
        high = noise - previous
        previous = noise
        metal = sum(weight * math.sin(2 * math.pi * frequency * t) * math.exp(-t / decay)
                    for frequency, weight, decay in ((2700, 0.42, 0.019), (4630, 0.32, 0.011), (7820, 0.18, 0.008)))
        contact = high * math.exp(-t / 0.0035) * 0.36
        wood = math.sin(2 * math.pi * 430 * t) * math.exp(-t / 0.009) * 0.26
        attack = min(1.0, t / 0.00025)
        release = min(1.0, (0.14 - t) / 0.012)
        output.append((metal + contact + wood) * attack * release)
    return output


def space_slash():
    rng = random.Random(20261116)
    output = []
    low = 0.0
    phase = 0.0
    for index in range(round(RATE * 0.42)):
        t = index / RATE
        noise = rng.uniform(-1.0, 1.0)
        low += 0.14 * (noise - low)
        high = noise - low
        phase += 2 * math.pi * (350 + 3400 * math.exp(-t / 0.032)) / RATE
        transient = high * math.exp(-t / 0.009) * 0.65
        air = high * math.exp(-t / 0.065) * 0.35
        ring = math.sin(phase) * math.exp(-t / 0.08) * 0.21
        impact = math.sin(2 * math.pi * 120 * t) * math.exp(-t / 0.027) * 0.24
        attack = min(1.0, t / 0.0003)
        release = min(1.0, (0.42 - t) / 0.025)
        output.append((transient + air + ring + impact) * attack * release)
    return output


if __name__ == "__main__":
    write_sound("sheath-clack-r01.wav", sheath_clack())
    write_sound("space-slash-r01.wav", space_slash())
