# -*- coding: utf-8 -*-
"""연구 시설의 지하로 내려가는 경비 맵 한 장을 만든다. 아래에서 위로 갈수록 깊어진다.

구성(아래 → 위)
- 시작 통로: 경비실 입구 아래의 암반 통로. Player가 깨어나는 자리와 수정이 있고, 아래쪽 끝은 네 단계로 어두워지며 화면 밖으로 이어진다.
- 경비실과 출구 통로 아랫부분: 석실 미리보기의 실험실 색 판(stone-room-lab.png)을 한 픽셀도 바꾸지 않고 쓴다.
- 심층 통로: 같은 통로를 위로 이어 붙이되, 실험실 색이 얼룩으로 벗겨져 원래 암반색이 드러나고 끝에서는 석대와 같은 보랏빛 돌이 된다.
- 어둠의 문: 통로 끝을 막아선 석조 문(../ancient-gate/gate.py). 문은 따로 놓는 객체이며 이 그림에는 굽지 않는다.
- 문 둘레의 검은 바깥에는 최종 보스 맵의 허공과 같은 먼지 점을 문에 가까울수록 촘촘하게 찍는다. 소품은 두지 않는다.
- 맵 전체를 석실 그림 한 가지 양식으로만 짠다. 타일로 그린 실험실 방은 석실과 어울리지 않아 쓰지 않는다.

좌표(T): 경비실 바닥의 왼쪽 위 모서리가 (0, 0), 아래와 오른쪽이 +. 1px = 원본 1px, 16px = 1유닛.
결과: 이 폴더의 lab-descent.aseprite(층 분리 원본), lab-descent.png(문을 뺀 합친 그림), map-layout.json(월드 배치), preview.png(문을 얹은 전체 모습)
Unity 프로젝트에는 Assets/Art/Environment/LabDescent/lab-descent.png, Assets/Art/Environment/AncientGate/gate-dark.png,
Assets/Editor/GuardMapManifest.json을 넣는다. 실행한 뒤 Unity 메뉴 Project Chrono > Rebuild Guard Map으로 맵 프리팹에 반영한다.
"""
import json
import random
import shutil
import sys
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
STONE = ROOT / "art/archive/environment/stone-room-preview"
TEX = ROOT / "docs/reference/sephiria/assets/Assets/Texture2D"
ASSETS = ROOT / "project/Assets"
MAP_TEXTURE = "Art/Environment/LabDescent/lab-descent.png"   # Unity에 넣을 맵 그림(Assets 기준)
GATE_TEXTURE = "Art/Environment/AncientGate/gate-dark.png"   # Unity에 넣을 어둠의 문 그림(Assets 기준)
LEFTOVERS = ["LaboratoryLayout", "MilitaryRobot", "WorldCollision"]  # 이전 구성에서 남아 맵 프리팹에서 지울 최상위 객체
sys.path.insert(0, str(ROOT / "art/work/chrono_askard_vfx_work"))
sys.path.insert(0, str(ROOT / "art/archive/environment/ancient-gate"))

LEFT, RIGHT, TOP, BOTTOM = -168, 488, -424, 368   # 캔버스 범위(T)
W, H = RIGHT - LEFT, BOTTOM - TOP
STONE_AT = (-168, -104)        # 석실 그림의 왼쪽 위(T)
STRIP_X0, STRIP_X1 = 125, 195  # 암반 통로의 가장자리 돌까지 포함한 좌우 끝(오른쪽은 다음 열)
FLOOR_X0, FLOOR_X1 = 132, 188  # 암반 통로 바닥의 좌우 끝
ROOM_W = 320                   # 경비실 바닥 폭
CENTER_X = ROOM_W // 2         # 통로와 문의 가운데 열

EXIT_KEEP_TOP = -104           # 기존 출구 통로가 끝나는 줄(이 위로 새로 잇는다)
PEEL = (-104, -188)            # 실험실 색이 벗겨져 암반색으로 바뀌는 구간(시작 줄, 끝 줄)
STAIN = (-200, -276)           # 암반색이 보랏빛 돌로 바뀌는 구간
GATE_BASE = -285               # 어둠의 문의 문턱 줄
GATE_STOP = 12                 # 문턱에서 문 속으로 더 걸어 들어갈 수 있는 거리(px)

ENTRY_KEEP_BOTTOM = 288        # 기존 입구 통로가 끝나는 줄(이 아래로 새로 잇는다)
START_END = 320                # 시작 통로에서 더 내려갈 수 없는 줄이며 이 줄부터 어두워진다
START_FADE = (0.8, 0.6, 0.4, 0.2)   # 그 아래 12줄마다의 밝기
WAKE = (CENTER_X, 248)         # Player가 깨어나는 자리
WALK_FROM = (CENTER_X, 308)    # 깨어나는 연출 전까지 쓰는 입장 걸음의 출발 자리
RESPAWN = (CENTER_X, 218)      # 수정을 켠 뒤 재도전할 때 서는 자리
CRYSTAL = (CENTER_X + 16, 205) # 체크포인트 수정의 밑동: 입구 문 2.5유닛 아래의 통로 오른편

BLACK = (0, 0, 0, 255)
DUST = ((0x3b, 0x38, 0x4e), (0x2c, 0x29, 0x3d), (0x57, 0x4c, 0x6f))  # 최종 보스 맵의 허공 먼지와 같은 색
# 암반색 → 석대와 같은 보랏빛 돌(어두운 틈 → 빛 받는 돌 순서)
PURPLE = {
    (33, 32, 52): (0x23, 0x21, 0x33), (35, 36, 55): (0x2a, 0x26, 0x38), (38, 41, 59): (0x34, 0x2f, 0x3c),
    (46, 57, 72): (0x3c, 0x35, 0x48), (49, 65, 78): (0x49, 0x40, 0x52), (60, 80, 97): (0x57, 0x4c, 0x5f),
    (70, 94, 115): (0x66, 0x5a, 0x70), (70, 94, 114): (0x66, 0x5a, 0x70), (79, 107, 131): (0x77, 0x6a, 0x82),
}
RIFT = ((0xb9, 0xa8, 0xee), (0x75, 0x63, 0xc2), (0x3f, 0x34, 0x70))  # 차원 균열의 밝은 심 → 테 → 어두운 끝


class Noise:
    """격자점의 난수를 부드럽게 이은 값. 문턱값으로 자르면 둥근 얼룩이 된다."""

    def __init__(self, seed, cell):
        self.rng = random.Random(seed)
        self.cell = cell
        self.points = {}

    def point(self, gx, gy):
        if (gx, gy) not in self.points:
            self.points[(gx, gy)] = self.rng.random()
        return self.points[(gx, gy)]

    def at(self, x, y):
        fx, fy = x / self.cell, y / self.cell
        gx, gy = int(fx // 1), int(fy // 1)
        tx, ty = fx - gx, fy - gy
        tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
        top = self.point(gx, gy) * (1 - tx) + self.point(gx + 1, gy) * tx
        bottom = self.point(gx, gy + 1) * (1 - tx) + self.point(gx + 1, gy + 1) * tx
        return top * (1 - ty) + bottom * ty


def build():
    rng = random.Random(20261127)
    stone_lab = Image.open(STONE / "stone-room-lab.png").convert("RGBA")
    stone_raw = Image.open(STONE / "stone-room-original.png").convert("RGBA")
    sl, sr = stone_lab.load(), stone_raw.load()
    names = ("void", "stone", "deep-corridor", "cracks")
    layers = {name: Image.new("RGBA", (W, H)) for name in names}
    px = {name: layers[name].load() for name in names}

    def canvas(tx, ty):
        return tx - LEFT, ty - TOP

    # ── 경비실과 그 위아래 통로: 석실 그림을 그대로 옮긴다
    layers["stone"].alpha_composite(stone_lab, canvas(*STONE_AT))
    sp = px["stone"]
    for y in range(stone_lab.height):     # 석실 그림의 검은 바깥은 투명하게 돌려 다른 층을 가리지 않게 한다
        for x in range(stone_lab.width):
            cx, cy = canvas(STONE_AT[0] + x, STONE_AT[1] + y)
            if sp[cx, cy][:3] == (0, 0, 0):
                sp[cx, cy] = (0, 0, 0, 0)

    def strip_row(ty):
        """통로 한 줄이 석실 그림에서 대응하는 줄: 위 통로와 아래 통로 모두 32줄마다 같은 돌 배열이 반복된다"""
        if ty < EXIT_KEEP_TOP:
            return (ty - STONE_AT[1]) % 32
        return 312 + ((ty - 208) % 32)

    # ── 시작 통로: 입구 통로를 아래로 잇고, 더 내려갈 수 없는 줄부터 12줄마다 한 단계씩 어둡게 한다
    shade_noise, shade_fine = Noise(71, 7.0), Noise(73, 3.0)
    for ty in range(ENTRY_KEEP_BOTTOM, BOTTOM):
        row = strip_row(ty)
        for tx in range(STRIP_X0, STRIP_X1):
            color = sl[tx - STONE_AT[0], row]
            if color[:3] == (0, 0, 0):
                continue
            # 단계의 경계가 가로 한 줄로 서지 않도록 얼룩 값만큼 위아래로 흔든다
            wobble = (shade_noise.at(tx, ty) - 0.5) * 1.6 + (shade_fine.at(tx, ty) - 0.5) * 0.5
            band = int((ty - START_END) / 12.0 + wobble + 1) - 1 if ty >= START_END - 10 else -1
            light = START_FADE[min(band, len(START_FADE) - 1)] if band >= 0 else 1.0
            sp[canvas(tx, ty)] = tuple(round(v * light) for v in color[:3]) + (255,)

    # ── 심층 통로: 같은 통로를 위로 잇고 줄이 올라갈수록 색을 바꾼다
    peel_noise, stain_noise, fine, mid = Noise(11, 9.0), Noise(57, 13.0), Noise(5, 3.0), Noise(31, 5.0)
    dp = px["deep-corridor"]
    for ty in range(GATE_BASE - 16, EXIT_KEEP_TOP):
        row = strip_row(ty)
        peel = (ty - PEEL[0]) / (PEEL[1] - PEEL[0])
        stain = (ty - STAIN[0]) / (STAIN[1] - STAIN[0])
        for tx in range(STRIP_X0, STRIP_X1):
            sx, sy = tx - STONE_AT[0], row
            if FLOOR_X0 <= tx < FLOOR_X1:
                # 바닥은 32줄 반복 대신 경비실 바닥의 세로로 긴 두 구간을 가져와 가로 줄무늬가 생기지 않게 한다(16px 위상 유지)
                # 벽 그늘이 있는 바닥 첫 줄들을 피해 8..119줄을 112줄씩 쓴다
                chunk = (EXIT_KEEP_TOP - 1 - ty) // 112
                sx += 96 if chunk % 2 else -112
                sy = (ty + 224 + 112 * chunk) - STONE_AT[1]
            coated, bare = sl[sx, sy], sr[sx, sy]
            if coated[:3] == (0, 0, 0):
                continue
            # 값을 자르지 않고 문턱 쪽을 값의 전체 범위(-0.5..1.5)만큼 움직여, 넓은 면이 한 줄에서 한꺼번에 바뀌지 않게 한다
            n = (peel_noise.at(tx, ty) - 0.25) / 0.5 + (mid.at(tx, ty) - 0.5) * 0.6 + (fine.at(tx, ty) - 0.5) * 0.3
            m = (stain_noise.at(tx, ty) - 0.25) / 0.5 + (mid.at(tx + 70, ty) - 0.5) * 0.6 + (fine.at(tx + 40, ty) - 0.5) * 0.3
            if m < stain * 2.4 - 0.7:
                color = PURPLE.get(bare[:3], bare[:3])
            elif n < peel * 2.4 - 0.7:
                color = bare[:3]
            else:
                color = coated[:3]
            dp[canvas(tx, ty)] = color + (255,)

    # ── 차원 균열: 보랏빛 돌 구간의 바닥에 닫힌 금 몇 줄
    crack_map = {}

    def crack(name, turn=0, flip=False):
        image = Image.open(TEX / name).convert("RGBA")
        ip = image.load()
        for y in range(image.height):
            for x in range(image.width):
                r, g, b, a = ip[x, y]
                if not a:
                    continue
                key = (r, g, b)
                if key not in crack_map:
                    crack_map[key] = RIFT[0] if r + g + b > 660 else RIFT[1] if r + g + b > 380 else RIFT[2]
                ip[x, y] = crack_map[key] + (a,)
        if flip:
            image = image.transpose(Image.FLIP_LEFT_RIGHT)
        return image.rotate(90 * turn)

    for name, tx, ty, turn, flip in (
            ("DimensionCrack_Idle_Closed_00.png", 134, GATE_BASE - 6, 0, False),
            ("DimensionCrack_Idle_Closed_04.png", 150, GATE_BASE + 30, 1, True),
            ("DimensionCrack_Idle_Closed_02.png", 122, GATE_BASE + 66, 2, False)):
        piece = crack(name, turn, flip)
        mask = Image.new("RGBA", piece.size)
        mp, pp = mask.load(), piece.load()
        for y in range(piece.height):     # 금은 통로의 돌 위에만 남긴다
            for x in range(piece.width):
                cx, cy = canvas(tx + x, ty + y)
                if pp[x, y][3] and dp[cx, cy][3] and ty + y > GATE_BASE:
                    mp[x, y] = pp[x, y]
        layers["cracks"].alpha_composite(mask, canvas(tx, ty))

    # ── 어둠의 문 둘레의 허공 먼지: 문에 가까울수록 촘촘하게
    vp = px["void"]
    gate_center = (CENTER_X, GATE_BASE - 30)
    for _ in range(900):
        tx, ty = rng.randrange(LEFT, RIGHT), rng.randrange(TOP, GATE_BASE + 90)
        distance = ((tx - gate_center[0]) ** 2 + (ty - gate_center[1]) ** 2) ** 0.5
        if rng.random() < min(distance / 150.0, 1.0) ** 0.7:
            continue
        if STRIP_X0 - 2 <= tx < STRIP_X1 + 2 and ty > GATE_BASE - 62:
            continue
        vp[canvas(tx, ty)] = rng.choice(DUST) + (255,)
    return layers


def layout():
    """Unity 맵이 쓸 배치. 벽·구역은 경비실 그림 객체 기준(경비실 바닥의 위쪽 가운데가 (0, 0), 위가 +), 지점은 월드 좌표다."""
    u = lambda v: v / 16.0
    anchor = (14.5, 26.0)                                   # 경비실 바닥의 위쪽 가운데가 놓인 월드 좌표(이전 맵과 같음)
    local = lambda tx, ty: (u(tx - CENTER_X), -u(ty))
    world = lambda point: [anchor[0] + u(point[0] - CENTER_X), anchor[1] - u(point[1])]

    def box(name, x0, y0, x1, y1):
        """T의 두 모서리로 벽 상자를 만든다"""
        (lx0, ly0), (lx1, ly1) = local(x0, y0), local(x1, y1)
        return {"name": name, "x": (lx0 + lx1) / 2, "y": (ly0 + ly1) / 2, "w": abs(lx1 - lx0), "h": abs(ly1 - ly0)}

    stop = GATE_BASE - GATE_STOP
    walls = [
        # 경비실: 이전 맵과 같은 값
        {"name": "Left", "x": -10.25, "y": -4.0625, "w": 1.0, "h": 10.125},
        {"name": "Right", "x": 10.25, "y": -4.0625, "w": 1.0, "h": 10.125},
        {"name": "TopLeft", "x": -6.25, "y": 0.5, "w": 9.0, "h": 1.0},
        {"name": "TopRight", "x": 6.25, "y": 0.5, "w": 9.0, "h": 1.0},
        {"name": "BottomLeft", "x": -6.25, "y": -8.625, "w": 9.0, "h": 1.0},
        {"name": "BottomRight", "x": 6.25, "y": -8.625, "w": 9.0, "h": 1.0},
        # 출구에서 어둠의 문까지
        box("ExitCorridorLeft", FLOOR_X0 - 16, stop - 16, FLOOR_X0, -16),
        box("ExitCorridorRight", FLOOR_X1, stop - 16, FLOOR_X1 + 16, -16),
        box("DarkGateBack", FLOOR_X0 - 16, stop - 16, FLOOR_X1 + 16, stop),
        # 경비실 입구에서 시작 통로의 아래쪽 끝까지
        box("EntryCorridorLeft", FLOOR_X0 - 16, 146, FLOOR_X0, BOTTOM),
        box("EntryCorridorRight", FLOOR_X1, 146, FLOOR_X1 + 16, BOTTOM),
        box("EntryCorridorBottom", FLOOR_X0 - 16, START_END, FLOOR_X1 + 16, BOTTOM),
    ]
    return {
        "size": [W, H],
        "pivot": [(CENTER_X - LEFT) / W, 1 - (0 - TOP) / H],       # 그림에서 경비실 바닥의 위쪽 가운데
        "anchor": list(anchor),
        "cameraVerticalLimits": [world((0, BOTTOM))[1], world((0, TOP))[1]],
        "walls": walls,
        # 밝기 전환 구역(경비실 그림 객체 기준의 x, y, 폭, 높이): 입구 쪽은 시작 통로의 아래쪽 끝까지, 출구 쪽은 어둠의 문 위까지
        "interiorArea": [-11.0, -8.5, 22.0, 10.3125],
        "entryArea": [-2.25, -u(BOTTOM), 4.5, u(BOTTOM) - 8.5],
        "exitArea": [-11.0, 1.8125, 22.0, -u(TOP) - 1.8125],
        "entryLineOffset": -9.8125, "exitLineOffset": 0.0,
        # 지점(월드): 깨어나는 자리이자 새 게임의 시작 위치, 입장 걸음의 출발·도착, 재도전 위치, 수정, 어둠의 문
        "wake": world(WAKE), "checkpoint": world(WAKE),
        "entranceSpawn": world(WALK_FROM), "entranceStop": world(WAKE),
        "respawn": world(RESPAWN), "crystal": world(CRYSTAL),
        # 문턱 가운데와 그 기준의 진입 감지 범위(x, y, 폭, 높이): 발이 문턱을 0.25 넘어 몸이 문 속에 들어온 뒤부터 닿는다
        "darkGate": {"position": world((CENTER_X, GATE_BASE)), "trigger": [0.0, 0.75, 3.0, 0.3]},
    }


def install(dark_gate):
    """맵 그림·어둠의 문 그림과 GuardMapBuilder가 읽을 목록 파일을 Unity 프로젝트에 넣는다."""
    plan = layout()
    target = ASSETS / MAP_TEXTURE
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(HERE / "lab-descent.png", target)
    gate_target = ASSETS / GATE_TEXTURE
    gate_target.parent.mkdir(parents=True, exist_ok=True)
    dark_gate.save(gate_target)
    manifest = {
        "ppu": 16, "mapTexture": MAP_TEXTURE, "mapPivot": plan["pivot"], "darkGateTexture": GATE_TEXTURE, "darkGate": plan["darkGate"]["position"],
        "darkGateTrigger": plan["darkGate"]["trigger"],
        "remove": LEFTOVERS, "walls": plan["walls"],
        **{key: plan[key] for key in ("interiorArea", "entryArea", "exitArea", "entryLineOffset", "exitLineOffset", "cameraVerticalLimits",
                                      "entranceSpawn", "entranceStop", "checkpoint", "respawn", "crystal")},
    }
    json.dump(manifest, open(ASSETS / "Editor/GuardMapManifest.json", "w", encoding="utf8"), indent=1)


def main():
    from aseio import write_ase
    import gate
    layers = build()
    names = list(layers)
    flat = Image.new("RGBA", (W, H), BLACK)
    for name in names:
        flat.alpha_composite(layers[name])
    flat.save(HERE / "lab-descent.png")
    json.dump(layout(), open(HERE / "map-layout.json", "w", encoding="utf8"), indent=1)

    dark, _, _, _ = gate.build()
    gate_layer = Image.new("RGBA", (W, H))
    gate_layer.alpha_composite(dark, (CENTER_X - LEFT - dark.width // 2, GATE_BASE - TOP - dark.height))
    backdrop = Image.new("RGBA", (W, H), BLACK)
    write_ase(HERE / "lab-descent.aseprite", (W, H), ["backdrop"] + names + ["dark-gate (separate object, not exported)"], [100], [],
              [[backdrop] + [layers[n] for n in names] + [gate_layer]])
    preview = flat.copy()
    preview.alpha_composite(gate_layer)
    preview.save(HERE / "preview.png")
    install(dark)
    colors = {p[:3] for p in flat.get_flattened_data()} if hasattr(flat, "get_flattened_data") else {p[:3] for p in flat.getdata()}
    print(f"size={W}x{H} colors={len(colors)} pivot={layout()['pivot']}")


if __name__ == "__main__":
    main()
