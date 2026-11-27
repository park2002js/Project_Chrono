"""최종 보스의 Aseprite 원본을 Unity용 PNG와 목록 파일로 내보낸다.

원본을 고친 뒤 이 스크립트를 실행하고 Unity 메뉴 Project Chrono > Rebuild Last Boss Assets를 실행한다.
Aseprite 실행 파일은 환경 변수 CHRONO_ASEPRITE 또는 아래 기본 경로에서 찾는다. 내용이 같은 PNG는 다시 쓰지 않는다.
"""
import io
import json
import os
import re
import shutil
import subprocess
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
SOURCES = HERE.parent
ROOT = HERE.parents[4]
DUMP = ROOT / "art/work/last-boss-unity/dump"
ASSETS = ROOT / "project/Assets"
ART = "Art/Enemy/LastBoss"
MAP_ART = "Art/Environment/Wasteland"
REF_TEX = ROOT / "docs/reference/sephiria/assets/Assets/Texture2D"
FILES = {
    "motion": "last-motion.aseprite", "vfx": "askard-vfx.aseprite", "amethyst": "pattern-02-amethyst-crystal/amethyst-crystal.aseprite", "oval": "portal-oval.aseprite",
    "pillar": "pillar.aseprite", "drop": "shock-pillar-drop/pillar-drop.aseprite", "droppreview": "shock-pillar-drop/pillar-drop-preview.aseprite",
    "rise": "pattern-05-crystal-rise/crystal-rise.aseprite", "shatter": "pattern-05-crystal-shatter/crystal-shatter.aseprite",
    "explosion": "pattern-05-crystal-shatter/crystal-impact-explosion.aseprite", "bullet": "pattern-01-homing-bullet/homing-bullet.aseprite",
    "vanish": "vanish-lines/vanish-lines.aseprite",
}


def dump_sources():
    """원본마다 레이어·프레임별 전체 캔버스 PNG와 정보 파일을 작업 폴더에 다시 풀어 놓는다."""
    candidates = [os.environ.get("CHRONO_ASEPRITE", ""), "D:/02_Games/01_Steam/Library/steamapps/common/Aseprite/Aseprite.exe",
                  "D:/02_Games/01_Steam/SteamClient/steamapps/common/Aseprite/Aseprite.exe"]
    aseprite = next((path for path in candidates if path and Path(path).is_file()), None)
    if aseprite is None:
        raise SystemExit("Aseprite 실행 파일을 찾지 못했습니다. CHRONO_ASEPRITE를 지정하세요.")
    for name, relative in FILES.items():
        target = DUMP / name
        if target.exists():
            shutil.rmtree(target)
        target.mkdir(parents=True)
        subprocess.run([aseprite, "-b", "--script-param", f"f={SOURCES / relative}", "--script-param", f"o={target}",
                        "--script", str(HERE / "dump.lua")], check=True)
        if not (target / "info.json").is_file():
            raise SystemExit(f"{relative}: 풀어 놓기에 실패했습니다.")


dump_sources()

textures = {}  # Assets 기준 경로 → (pivotX, pivotY)
clips = []
written = 0


class Source:
    """레이어·프레임별 PNG로 풀어 둔 원본 한 개"""

    def __init__(self, name):
        self.dir = DUMP / name
        info = json.loads((self.dir / "info.json").read_text(encoding="utf8"))
        self.size = (info["width"], info["height"])
        self.layers = info["layers"]
        self.durations = info["durations"]
        self.tags = {t["name"]: (t["from"], t["to"]) for t in info["tags"]}
        self.cels = {tuple(c) for c in info["cels"]}
        self.cache = {}

    def cel(self, layer, frame):
        if (layer, frame) not in self.cels:
            return None
        key = (layer, frame)
        if key not in self.cache:
            self.cache[key] = Image.open(self.dir / f"L{layer:02d}_F{frame:03d}.png").convert("RGBA")
        return self.cache[key]

    def merged(self, frame, layers=None):
        """지정한 레이어(1부터)를 아래에서 위로 합친 전체 캔버스"""
        canvas = Image.new("RGBA", self.size)
        for layer in layers or range(1, len(self.layers) + 1):
            image = self.cel(layer, frame)
            if image is not None:
                canvas.alpha_composite(image)
        return canvas


def save(path, image, pivot):
    """내용이 달라졌을 때만 PNG를 쓰고 기준점을 목록에 기록 (pivot은 왼쪽 위 기준 픽셀)"""
    global written
    full = ASSETS / path
    full.parent.mkdir(parents=True, exist_ok=True)
    buffer = io.BytesIO()
    image.save(buffer, "PNG")
    data = buffer.getvalue()
    if not full.exists() or full.read_bytes() != data:
        full.write_bytes(data)
        written += 1
    textures[path] = (pivot[0] / image.width, 1 - pivot[1] / image.height)


def add_clip(name, frames, durations, loop=False, loop_start=0):
    clips.append({"name": name, "path": f"{ART}/Clips/{name}.asset", "frames": frames,
                  "durations": [d / 1000 for d in durations], "loop": loop, "loopStart": loop_start})


def strip(name, folder, images, durations, pivot, loop=False, pad=0, trim=True):
    """같은 크기로 잘라 낸 프레임 묶음을 저장하고 클립 하나로 등록"""
    boxes = [image.getbbox() for image in images if image is not None and image.getbbox()]
    if not boxes:
        return False
    if trim:
        box = (min(b[0] for b in boxes) - pad, min(b[1] for b in boxes) - pad, max(b[2] for b in boxes) + pad, max(b[3] for b in boxes) + pad)
    else:
        box = (0, 0, *images[0].size)
    frames = []
    for index, image in enumerate(images):
        if image is None or not image.getbbox():
            frames.append("")
            continue
        canvas = Image.new("RGBA", (box[2] - box[0], box[3] - box[1]))
        canvas.alpha_composite(image, (-box[0], -box[1])) if box[0] >= 0 and box[1] >= 0 else canvas.paste(image, (-box[0], -box[1]))
        path = f"{ART}/{folder}/{name}-{index:02d}.png"
        save(path, canvas, (pivot[0] - box[0], pivot[1] - box[1]))
        frames.append(path)
    add_clip(name, frames, durations, loop)
    return True


def slug(text):
    return re.sub(r"[^a-z0-9]+", "-", text.lower()).strip("-")


# ---------------------------------------------------------------- 몸체
motion = Source("motion")
BODY_PIVOT = (30, 83)
body = {}
for frame in range(1, 109):
    path = f"{ART}/Body/body-{frame:03d}.png"
    save(path, motion.merged(frame), BODY_PIVOT)
    body[frame] = path


def body_clip(name, first, last, durations=None, loop=False):
    frames = [body[f] for f in range(first, last + 1)]
    add_clip(name, frames, durations or [motion.durations[f - 1] for f in range(first, last + 1)], loop)


body_clip("body-idle", 52, 63, loop=True)
body_clip("body-wave-begin", 2, 36, loop=True)
body_clip("body-attack-ready", 37, 44)
body_clip("body-attack-ready-loop", 39, 44, loop=True)
# 첫 장은 스미어이므로 원본의 500ms를 쓰지 않고 한 장 분량만 표시
body_clip("body-attack-doing", 45, 50, durations=[50] * 6)
body_clip("body-attack-end", 51, 51)
body_clip("body-wave-fall", 64, 64)
body_clip("body-landing-in", 65, 67, durations=[50, 50, 50])
body_clip("body-landing-out", 68, 70)
body_clip("body-groggy", 76, 77, durations=[100, 100], loop=True)
add_clip("body-die", [body[f] for f in list(range(71, 76)) + list(range(80, 109))],
         [motion.durations[f - 1] for f in list(range(71, 76)) + list(range(80, 109))])

# ---------------------------------------------------------------- 가로선 소실
vanish = Source("vanish")
for tag, name in (("Vanish - attack ready 6", "vanish-ready6"), ("Appear - attack ready 8", "appear-ready8"),
                  ("Vanish - attack ready 8", "vanish-ready8"), ("Vanish - landing 3", "vanish-landing3")):
    first, last = vanish.tags[tag]
    frames = [f for f in range(first, last + 1) if vanish.cel(3, f) is not None]
    strip(name, "Vanish", [vanish.cel(3, f) for f in frames], [50] * len(frames), BODY_PIVOT, trim=False)

# ---------------------------------------------------------------- 손끝·차원문·착지 효과
vfx = Source("vfx")
VFX_PIVOT = (96, 144)
SUFFIX = {1: "back", 2: "ground", 3: "stamp", 4: "rift", 5: "cast", 6: "front", 7: "burst"}
NAMES = {"P1 - Orange ignition": ("p1-ignition", False), "P1 - Arrival": ("p1-arrival", False),
         "Portal - Open": ("rift-open", False), "Portal - Hold": ("rift-hold", True), "Portal - Release": ("rift-release", False),
         "P2 - Amethyst cast": ("p2-cast", True), "P5 - Crystal eruption": ("p5-eruption", False),
         "P3 - Lightning": ("p3-lightning", True), "P3 - Pillar detonation": ("p3-detonation", False),
         "P5 - Crystal impact": ("p5-impact", False), "P6 - Sky cast": ("p6-sky", True),
         "P7 - Landing impact": ("p7-impact", False), "Counter - Gold contact": ("counter-gold", False)}
for tag, (name, loop) in NAMES.items():
    first, last = vfx.tags[tag]
    for layer, suffix in SUFFIX.items():
        strip(f"{name}-{suffix}", f"Vfx/{name}", [vfx.cel(layer, f) for f in range(first, last + 1)],
              vfx.durations[first - 1:last], VFX_PIVOT, loop)

oval = Source("oval")
for tag, name, loop in (("Portal - Open", "oval-open", False), ("Portal - Hold", "oval-hold", True), ("Portal - Release", "oval-release", False)):
    first, last = oval.tags[tag]
    strip(name, "Portal", [oval.merged(f) for f in range(first, last + 1)], oval.durations[first - 1:last], (96, 112), loop)

# ---------------------------------------------------------------- 자수정·기둥·수정
# 패턴 2의 솟는 자수정: 바닥 균열·차원문(1번 레이어)과 수정·파편(2·3번 레이어)을 따로 내보낸다
amethyst = Source("amethyst")
for index in (1, 2, 3):
    first, last = amethyst.tags[f"P2 - Amethyst crystal {index}"]
    times = amethyst.durations[first - 1:last]
    strip(f"amethyst-{index}-ground", "Objects/amethyst", [amethyst.cel(1, f) for f in range(first, last + 1)], times, (32, 45))
    strip(f"amethyst-{index}-body", "Objects/amethyst", [amethyst.merged(f, (2, 3)) for f in range(first, last + 1)], times, (32, 45))

pillar = Source("pillar")
image = Image.new("RGBA", (13, 25))
image.alpha_composite(pillar.merged(1), (1, 1))
save(f"{ART}/Objects/pillar/pillar.png", image, (6.5, 24))  # 금색 테두리를 그릴 1px 여백 포함
drop = Source("drop")
strip("pillar-drop-body", "Objects/pillar", [drop.merged(f, (2, 3, 4)) for f in range(1, 8)], drop.durations, (6.5, 64), pad=1)
strip("pillar-drop-shadow", "Objects/pillar", [drop.cel(1, f) for f in range(1, 8)], drop.durations, (6.5, 64))
save(f"{ART}/Objects/pillar/pillar-shadow.png", drop.cel(1, 7).crop(drop.cel(1, 7).getbbox()),
     (6.5 - drop.cel(1, 7).getbbox()[0], 64 - drop.cel(1, 7).getbbox()[1]))
preview = Source("droppreview")
first, last = preview.tags["LAND"]
for layer_name, name in (("summon_ground", "pillar-land-ground"), ("summon_back", "pillar-land-back"), ("summon_front", "pillar-land-front")):
    layer = preview.layers.index(layer_name) + 1
    strip(name, "Objects/pillar", [preview.cel(layer, f) for f in range(first, last + 1)], preview.durations[first - 1:last], (186.5, 136))

rise = Source("rise")
CRYSTAL_PIVOT = (6.5, 15)
strip("crystal-rise", "Objects/crystal", [rise.merged(f, (2, 3)) for f in range(1, 7)], rise.durations[:6], CRYSTAL_PIVOT, pad=1)
strip("crystal-idle", "Objects/crystal", [rise.merged(f, (2, 3)) for f in range(7, 13)], rise.durations[6:12], CRYSTAL_PIVOT, loop=True, pad=1)
strip("crystal-shadow-rise", "Objects/crystal", [rise.cel(1, f) for f in range(1, 7)], rise.durations[:6], CRYSTAL_PIVOT)
shadow = rise.cel(1, 7)
save(f"{ART}/Objects/crystal/crystal-shadow.png", shadow.crop(shadow.getbbox()), (6.5 - shadow.getbbox()[0], 15 - shadow.getbbox()[1]))
shatter = Source("shatter")
strip("crystal-shatter", "Objects/crystal", [shatter.merged(f) for f in range(1, 13)], shatter.durations, (19.5, 24))
explosion = Source("explosion")
strip("crystal-explosion", "Objects/crystal", [explosion.merged(f) for f in range(1, 11)], explosion.durations, (20.5, 36))

# ---------------------------------------------------------------- 유도탄
bullet = Source("bullet")
for tag, name, loop in (("Body", "homing-body", True), ("Bright", "homing-bright", True), ("Trail A", "homing-trail-a", False),
                        ("Trail B", "homing-trail-b", False), ("Fire - Open", "homing-fire-open", False),
                        ("Fire - Release", "homing-fire-release", False), ("Explosion", "homing-explosion", False)):
    first, last = bullet.tags[tag]
    strip(name, "Bullet", [bullet.merged(f) for f in range(first, last + 1)], bullet.durations[first - 1:last], (32, 24), loop)

# ---------------------------------------------------------------- 석대 맵
# 맵 그림과 월드 배치는 warped-facility-map/compose.py가 만든다
layout = json.loads((SOURCES / "warped-facility-map/map-layout.json").read_text(encoding="utf8"))


def map_texture(path, source, pivot):
    """원본 그림이 로컬에 있으면 다시 쓰고 없으면 이미 내보낸 그림으로 기준점만 기록"""
    if source is not None and Path(source).is_file():
        save(path, Image.open(source).convert("RGBA"), pivot)
        return
    with Image.open(ASSETS / path) as existing:
        textures[path] = (pivot[0] / existing.width, 1 - pivot[1] / existing.height)


map_texture(f"{MAP_ART}/Textures/stone-platform.png", SOURCES / "warped-facility-map/warped-facility.png", tuple(layout["origin"]))
# 다리 프레임: 기준점은 프레임의 위쪽 가운데
bridge_frames = []
for frame in sorted((SOURCES / "warped-facility-map/bridge-frames").glob("bridge-*.png")):
    with Image.open(frame) as image:
        save(f"{MAP_ART}/Textures/{frame.name}", image.convert("RGBA"), (image.width // 2, 0))
    bridge_frames.append(f"{MAP_ART}/Textures/{frame.name}")
save(f"{MAP_ART}/Textures/backdrop.png", Image.new("RGBA", (4, 4), (22, 20, 29, 255)), (2, 2))
# 처치 뒤에 올라오는 출구 다리 프레임: 기준점은 프레임의 위쪽 가운데
exit_bridge_frames = []
for frame in sorted((SOURCES / "warped-facility-map/exit-bridge-frames").glob("exit-bridge-*.png")):
    with Image.open(frame) as image:
        save(f"{MAP_ART}/Textures/{frame.name}", image.convert("RGBA"), (image.width // 2, 0))
    exit_bridge_frames.append(f"{MAP_ART}/Textures/{frame.name}")
# 빛의 문: 문 그림의 기준점은 문턱 가운데(아래 가운데), 빛 번짐은 위쪽 가운데. 원본은 environment/ancient-gate/gate.py가 만든다
GATE_ART = "Art/Environment/AncientGate"
GATE_SOURCE = ROOT / "art/archive/environment/ancient-gate"
with Image.open(GATE_SOURCE / "gate-light.png") as image:
    save(f"{GATE_ART}/gate-light.png", image.convert("RGBA"), (image.width / 2, image.height))
with Image.open(GATE_SOURCE / "gate-light-spill.png") as image:
    save(f"{GATE_ART}/gate-light-spill.png", image.convert("RGBA"), (image.width / 2, 0))
shapes = layout["walls"]

# ---------------------------------------------------------------- 전투 배경음
SFX = ROOT / "art/archive/sfx"
MUSIC = "Audio/Enemy/last-boss-bgm.mp3"
SOUNDS = {  # 빌더가 찾을 이름 → (프로젝트에 둘 파일, 원본)
    "vanish": ("last-boss-vanish.wav", SFX / "last boss/텔레포트 시작(가로선).wav"),
    "appear": ("last-boss-appear.wav", SFX / "last boss/텔레포트 도착(가로선).wav"),
    "rift-open": ("last-boss-rift-open.mp3", SFX / "last boss/차원문 열림.mp3"),
    "rift-close": ("last-boss-rift-close.mp3", SFX / "last boss/차원문 닫힘.mp3"),
    "counter": ("last-boss-counter.wav", SFX / "last boss/카운터 가능 알람.wav"),
    "fall": ("last-boss-fall.wav", SFX / "last boss/낙하 시작.wav"),
    "land": ("last-boss-land.mp3", SFX / "last boss/패턴 5,7 낙하 착지.mp3"),
    "crystal-burst": ("last-boss-crystal-burst.mp3", SFX / "last boss/패턴 5 자수정 폭발.mp3"),
    "amethyst-cast": ("last-boss-amethyst-cast.wav", SFX / "last boss/패턴2 시전.wav"),
    "amethyst-rise": ("last-boss-amethyst-rise.wav", SFX / "last boss/패턴2 자수정 솟음.wav"),
    "homing-explosion": ("last-boss-homing-explosion.mp3", SFX / "last boss/유도탄 폭발.mp3"),
    "hit": ("enemy-hit.wav", SFX / "모든적 공통 피격음.wav"),
    "groggy": ("enemy-groggy.mp3", SFX / "모든적 공통 기절음.mp3"),
}


def copy_audio(source, relative):
    """원본이 있으면 내용이 달라졌을 때만 프로젝트로 복사하고, 프로젝트에 파일이 있는지 반환"""
    target = ASSETS / relative
    if source.is_file() and (not target.exists() or target.read_bytes() != source.read_bytes()):
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
    return target.exists()


has_music = copy_audio(SFX / "last boss/bgm.mp3", MUSIC)
audio = [{"name": name, "path": f"Audio/Enemy/{file}"} for name, (file, source) in SOUNDS.items() if copy_audio(source, f"Audio/Enemy/{file}")]

manifest = {
    "ppu": 16,
    "textures": [{"path": path, "pivotX": round(p[0], 6), "pivotY": round(p[1], 6)} for path, p in sorted(textures.items())],
    "clips": clips,
    "mapTexture": f"{MAP_ART}/Textures/stone-platform.png",
    "backdropTexture": f"{MAP_ART}/Textures/backdrop.png",
    "walls": shapes,
    **{key: layout[key] for key in ("cameraVerticalLimits", "entranceSpawn", "entranceStop", "checkpoint", "respawn", "crystal", "bossSpawn", "encounter")},
    "bridge": {**layout["bridge"], "frames": bridge_frames},
    "exitBridge": {**layout["exitBridge"], "frames": exit_bridge_frames},
    "lightGate": {"texture": f"{GATE_ART}/gate-light.png", "spillTexture": f"{GATE_ART}/gate-light-spill.png", "position": layout["lightGate"]["position"],
                  "trigger": layout["lightGate"]["trigger"]},
    "battleMusic": MUSIC if has_music else "",
    "audio": audio,
}
out = ASSETS / "Editor/LastBossAssetManifest.json"
text = json.dumps(manifest, ensure_ascii=False, indent=1)
if not out.exists() or out.read_text(encoding="utf8") != text:
    out.write_text(text, encoding="utf8", newline="\n")
print(f"textures={len(textures)} written={written} clips={len(clips)} walls={len(shapes)}")
print("clip names:", " ".join(c["name"] for c in clips))
