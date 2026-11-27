# -*- coding: utf-8 -*-
"""차원의 기운으로 변형된 연구 시설 복도 끝에 원형 석대가 드러난 최종 보스 맵 그림을 만든다.

구성(아래 → 위)
- 시설 복도: 참고 게임의 시설 전장 그림에서 소품이 겹치지 않은 16px 칸만 골라 다시 깐다(facility-tiles.png).
  위로 갈수록 벽이 끊겨 떠다니고 색이 석대의 보랏빛으로 물든다.
- 다리: 참고 게임의 다리 여섯 프레임(꺼진 판이 5px 떠오르며 켜짐, 장당 0.05초)을 그대로 쓰고, 그 앞에 더 깊이 가라앉은 네 장을 덧붙인다.
  맵 그림에 굽지 않고 따로 움직이는 객체로 내보낸다(bridge.aseprite, bridge-frames/). 계단은 쓰지 않는다.
- 석대: 참고 게임의 석대 그림에서 전투 바닥인 윗면만 남기고, 옆면은 장식 없는 벽돌 면으로 다시 그린다.
- 허공: 복도에서 떨어진 벽·바닥·다리 조각 몇 개와 차원 균열, 먼지 점. 소품은 두지 않는다.
- 위쪽 출구: 석대 위쪽 끝에 같은 다리 한 벌을 더 두어 처치 뒤에 올라오게 하고, 그 끝의 석판에 빛의 문(../../../environment/ancient-gate/gate.py)을 세운다.
  다리와 문은 따로 놓는 객체이며 맵 그림에는 석판만 굽는다. 석판과 문은 석대와 같은 돌 색으로 장식 없이 그린다.

좌표: 1px = 원본 1px, 16px = 1유닛. 캔버스의 ORIGIN이 월드 (0, 0)이고 석대는 이전 맵과 같은 월드 위치에 있다.
      위쪽 출구를 넣으려고 캔버스를 위로 TOP_EXTRA만큼 늘렸다. build()는 늘리기 전의 좌표로 그리고 extend()가 아래로 밀어 넣는다.
결과: 이 폴더의 warped-facility.aseprite(층 분리 원본), warped-facility.png(다리를 뺀 합친 그림), bridge.aseprite·bridge-frames/(입구 다리 프레임),
      exit-bridge-frames/(출구 다리 프레임), map-layout.json(월드 배치)
"""
import json
import random
import sys
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
TEX = ROOT / "docs/reference/sephiria/assets/Assets/Texture2D"
ROOMS = ROOT / "art/preview/boss-arena-environments"
sys.path.insert(0, str(ROOT / "art/work/chrono_askard_vfx_work"))
sys.path.insert(0, str(ROOT / "art/archive/environment/ancient-gate"))

W, H = 544, 688
ORIGIN = (271, 255)          # 월드 (0, 0)이 놓이는 캔버스 좌표
PLATFORM_AT = (16, 104)      # 석대 그림의 왼쪽 위(이전 맵과 같은 월드 위치)
CORRIDOR_X = ORIGIN[0] - 56  # 복도 7칸(벽 1 + 바닥 5 + 벽 1)의 왼쪽 끝
CORRIDOR_TOP = 416           # 복도 바닥이 시작되는 줄
WALL_TOP = 464               # 온전한 벽이 시작되는 줄
BRIDGE_AT = (208, 323)       # 다리 프레임의 왼쪽 위가 놓이는 캔버스 좌표(다 올라온 첫 판의 윗줄이 석대 테두리 바로 아래)
BRIDGE_FRAME = (128, 132)    # 다리 프레임 크기: 참고 그림 128×112에 가라앉을 여유 20줄
BRIDGE_SINK = ((14, 0.68), (10, 0.76), (6, 0.84), (3, 0.92))  # 참고 첫 장보다 더 내려간 정도(px)와 밝기
BRIDGE_FRAME_MS = 50         # 참고 게임의 재생 속도(20fps)
BACKDROP = (22, 20, 29, 255)
TOP_EXTRA = 208              # 위쪽 출구를 넣으려고 캔버스 위에 덧붙인 줄 수
FULL_H = H + TOP_EXTRA       # 늘린 캔버스의 높이
FULL_ORIGIN = (ORIGIN[0], ORIGIN[1] + TOP_EXTRA)   # 늘린 캔버스에서 월드 (0, 0)의 자리
PLATFORM_TOP = PLATFORM_AT[1] + 17 + TOP_EXTRA     # 늘린 캔버스에서 석대 윗면의 첫 줄
EXIT_BRIDGE_AT = (208, PLATFORM_TOP - 104)         # 출구 다리 프레임의 왼쪽 위: 아래쪽 톱니가 석대 윗면 밑으로 들어간다
LANDING = (211, EXIT_BRIDGE_AT[1] - 54, 331, EXIT_BRIDGE_AT[1] + 2)   # 빛의 문이 서는 석판 윗면의 왼쪽·위·오른쪽·아래(끝은 다음 칸)
LANDING_FACE = 8             # 석판 앞면의 높이
GATE_BASE = LANDING[1] + 28  # 빛의 문의 문턱 줄
GATE_STOP = 12               # 문턱에서 문 속으로 더 걸어 들어갈 수 있는 거리(px)
TOP_FACE = ((0x77, 0x69, 0x7e), (0x66, 0x59, 0x6c), (0x57, 0x4c, 0x5f))  # 석대 윗면의 밝은 줄 → 바탕 → 홈

BASE = {tuple(int(h[i:i + 2], 16) for i in (1, 3, 5)) for h in
        "#6c6c90 #9ba3c6 #51546e #43455c #5e607f #35364b #2c2b40 #8488ab #b27156 #b3bfe1 #985b47 #6a4845 #4b3a43 #222034".split()}
OUTSIDE = {(0x2c, 0x2b, 0x40), (0x22, 0x20, 0x34)}  # 시설 그림의 방 바깥색과 구덩이색


def harvest():
    """시설 전장 그림의 세로 복도와 위쪽 벽에서 소품·빛줄기가 겹치지 않은 칸을 모은다."""
    room = Image.open(next(ROOMS.glob("03-*/01-*.png"))).convert("RGB")
    px = room.load()

    def clean(x0, y0):
        return all(px[x, y] in BASE for y in range(y0, y0 + 16) for x in range(x0, x0 + 16))

    groups = {"wall-left": [], "floor": [], "wall-right": []}
    for k in range(32, 46):  # 첫 줄의 문턱과 끝 줄의 모서리 칸은 곧은 복도에 쓰지 않는다
        y0 = 8 + 16 * k
        for c in range(7):
            x0 = 760 + 16 * c
            if clean(x0, y0):
                name = "wall-left" if c == 0 else "wall-right" if c == 6 else "floor"
                groups[name].append(room.crop((x0, y0, x0 + 16, y0 + 16)).convert("RGBA"))
    count = max(len(v) for v in groups.values())
    sheet = Image.new("RGBA", (16 * count, 16 * 3 + 40))
    for row, cells in enumerate(groups.values()):
        for i, cell in enumerate(cells):
            sheet.paste(cell, (16 * i, 16 * row))

    # 위쪽 벽 한 구간: 방 바깥색과 구덩이색으로 이어진 위아래 가장자리를 투명하게
    wall = room.crop((560, 160, 664, 200)).convert("RGBA")
    wp = wall.load()
    for x in range(wall.width):
        for ys in (range(wall.height), range(wall.height - 1, -1, -1)):
            for y in ys:
                if wp[x, y][:3] in OUTSIDE:
                    wp[x, y] = (0, 0, 0, 0)
                else:
                    break
    sheet.paste(wall, (0, 48))
    sheet.save(HERE / "facility-tiles.png")
    json.dump({"cell": 16, "rows": {k: len(v) for k, v in groups.items()}, "wall_top": [0, 48, wall.width, wall.height]},
              open(HERE / "facility-tiles.json", "w", encoding="utf8"), indent=1)


def load_tiles():
    sheet = Image.open(HERE / "facility-tiles.png").convert("RGBA")
    info = json.load(open(HERE / "facility-tiles.json", encoding="utf8"))
    tiles = {}
    for row, (name, count) in enumerate(info["rows"].items()):
        tiles[name] = [sheet.crop((16 * i, 16 * row, 16 * i + 16, 16 * row + 16)) for i in range(count)]
    x, y, w, h = info["wall_top"]
    tiles["wall-top"] = sheet.crop((x, y, x + w, y + h))
    return tiles


# ---------------------------------------------------------------- 색
def tint(image, warp=0.0, dark=1.0):
    """기운이 닿은 정도(warp 0~1)만큼 시설의 푸른 회색을 석대의 보랏빛 쪽으로 옮기고 dark로 밝기를 낮춘다.

    칸마다 같은 값을 쓰므로 원래 팔레트의 색 수가 단계 수만큼만 늘어난다.
    """
    if warp <= 0 and dark >= 1:
        return image
    out = image.copy()
    px = out.load()
    cache = {}
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if not a:
                continue
            key = (r, g, b)
            if key not in cache:
                nr = r + (r * 0.03 + 3) * warp
                ng = g - g * 0.10 * warp
                nb = b - b * 0.11 * warp
                cache[key] = tuple(max(0, min(255, round(v * dark))) for v in (nr, ng, nb))
            px[x, y] = cache[key] + (a,)
    return out


def recolor(image, mapping):
    out = image.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if a and (r, g, b) in mapping:
                px[x, y] = mapping[(r, g, b)] + (a,)
    return out


def outline(layer, color=(0x1b, 0x18, 0x26, 255)):
    """허공과 맞닿은 조각의 바깥 1px에 어두운 테두리를 둘러 배경에서 떼어 보이게 한다."""
    alpha = layer.getchannel("A").load()
    out = layer.copy()
    px = out.load()
    for y in range(layer.height):
        for x in range(layer.width):
            if alpha[x, y]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < layer.width and 0 <= ny < layer.height and alpha[nx, ny]:
                    px[x, y] = color
                    break
    return out


def thickness(piece, rows=3, color=(0x23, 0x21, 0x33, 255)):
    """떠 있는 바닥 조각의 아래쪽에 두께 면을 붙인다."""
    out = Image.new("RGBA", (piece.width, piece.height + rows))
    alpha = piece.getchannel("A").load()
    px = out.load()
    for x in range(piece.width):
        bottom = max((y for y in range(piece.height) if alpha[x, y]), default=None)
        if bottom is None:
            continue
        for k in range(1, rows + 1):
            px[x, bottom + k] = color
    out.alpha_composite(piece)
    return out


# ---------------------------------------------------------------- 석대
STONE_C, STONE_G, STONE_I, STONE_F, STONE_H = (0x49, 0x40, 0x52), (0x3c, 0x35, 0x48), (0x34, 0x2f, 0x3c), (0x2c, 0x2c, 0x2c), (0x57, 0x4c, 0x5f)
TOP_LEFT, TOP_RIGHT, TOP_FIRST, RIM, FACE_BOTTOM, CUT = 64, 447, 17, 222, 287, 189  # 윗면의 좌우·위 끝, 윗면 아래 끝 줄, 옆면 아래 끝 줄, 아래 모서리 사선의 시작 줄
FRONT_LEFT, FRONT_RIGHT = 97, 413   # 앞면(양 끝 기둥 포함)의 좌우 끝
FACE_HEIGHT = 66                    # 옆면의 세로 높이


def platform():
    """석대 그림에서 전투 바닥인 윗면만 남기고, 옆면은 원본의 줄 구성과 색으로 장식 없는 벽돌 면을 다시 그린다.

    계단·뿌리·덤불·뼈·가시 아치·모서리의 해골 장식은 모두 뺀다. 아래 모서리는 윗면의 사선을 그대로 내려 그어 허공에 뜬 판으로 보이게 한다.
    """
    image = Image.open(TEX / "AskardMap.png").convert("RGBA")
    px = image.load()
    clear = (0, 0, 0, 0)

    # 윗면의 위쪽 두 모서리는 장식이 없는 오른쪽 사선을 재서 왼쪽에도 같은 폭으로 적용
    corner = {}
    for y in range(TOP_FIRST, TOP_FIRST + 60):
        xs = [x for x in range(300, TOP_RIGHT + 1) if px[x, y][3]]
        corner[y] = TOP_RIGHT - max(xs) if xs else 999

    # 윗면 밖은 모두 지운다
    for y in range(image.height):
        for x in range(image.width):
            inset = corner.get(y, 0)
            outside = y < TOP_FIRST or y > RIM or x < TOP_LEFT + inset or x > TOP_RIGHT - inset
            if not outside and y >= CUT:
                outside = x < TOP_LEFT + (y - CUT) or x > TOP_RIGHT - (y - CUT)
            if outside:
                px[x, y] = clear

    rng = random.Random(64)
    rows = {223: STONE_C, 224: STONE_C, 225: STONE_C, 226: STONE_G, 227: STONE_G, 228: STONE_G, 229: STONE_I, 230: STONE_G, 231: STONE_I,
            239: STONE_I, 240: STONE_G, 241: STONE_I}
    for y in range(232, 239):
        rows[y] = STONE_G
    mortar = (248, 257, 265, 274, 282)
    courses = ((242, 247, 0.0), (249, 256, 0.0), (258, 264, 0.2), (266, 273, 0.45), (275, 281, 0.75), (283, FACE_BOTTOM, 1.0))  # (첫 줄, 끝 줄, 어두운 벽돌 비율)

    # 앞면의 벽돌 단: 길이가 다른 벽돌을 단마다 어긋나게 놓는다
    for y in rows:
        for x in range(FRONT_LEFT, FRONT_RIGHT + 1):
            px[x, y] = rows[y] + (255,)
    for y in mortar:
        for x in range(FRONT_LEFT, FRONT_RIGHT + 1):
            px[x, y] = STONE_F + (255,)
    for first, last, dark in courses:
        x = FRONT_LEFT + 12
        while x <= FRONT_RIGHT - 12:
            length = rng.choice((18, 22, 26, 30, 34, 40))
            fill = STONE_I if rng.random() < dark else STONE_G
            joint = STONE_F if fill == STONE_I else STONE_I
            for yy in range(first, last + 1):
                for xx in range(x, min(x + length, FRONT_RIGHT - 11)):
                    px[xx, yy] = (joint if xx == x and x > FRONT_LEFT + 12 else fill) + (255,)
            x += length

    # 양 끝의 기둥과 안쪽 테두리 두 줄
    for left in (True, False):
        pillar = range(97, 106) if left else range(405, 414)
        lines = (106, 107, 108) if left else (404, 403, 402)
        for y in range(226, FACE_BOTTOM + 1):
            color = STONE_I if y == 229 or y >= 283 else STONE_F if y in (257, 282) else STONE_G
            for x in pillar:
                px[x, y] = color + (255,)
            if y >= 230:
                for x, c in zip(lines, (STONE_I, STONE_G, STONE_I)):
                    px[x, y] = c + (255,)

    # 모서리 면: 윗면의 사선 아래로 같은 높이만큼 내려오고, 위쪽 절반은 빛 받는 면·아래쪽은 벽돌 면
    for left in (True, False):
        for x in (range(TOP_LEFT, 96) if left else range(415, TOP_RIGHT + 1)):
            top = CUT + ((x - TOP_LEFT) if left else (TOP_RIGHT - x))
            bottom = min(FACE_BOTTOM, top + FACE_HEIGHT + 1)
            for y in range(top + 1, bottom + 1):
                d = y - top
                if d in (7, 9, 17, 19):
                    color = STONE_C
                elif d <= 33:
                    color = STONE_H
                elif d <= 38:
                    color = STONE_C
                elif d == 46 or d >= bottom - top - 1:
                    color = STONE_I if d == 46 else STONE_F
                elif d <= 52:
                    color = STONE_G
                else:
                    color = STONE_F if d == 60 else STONE_I
                px[x, y] = color + (255,)
        edge = 96 if left else 414
        for y in range(CUT + 33 + (0 if left else 1), FACE_BOTTOM + 1):
            px[edge, y] = (STONE_F if y in (257, 282) or y == FACE_BOTTOM else STONE_C) + (255,)
    return image


# ---------------------------------------------------------------- 조립
def build():
    rng = random.Random(20261127)
    tiles = load_tiles()
    layers = {name: Image.new("RGBA", (W, H)) for name in
              ("void", "far-debris", "platform", "corridor", "bridge", "near-debris", "lights", "cracks")}

    def put(layer, piece, x, y):
        layers[layer].alpha_composite(piece, (int(x), int(y)))

    def warp_at(y):
        """줄의 세로 위치에 따른 기운의 단계"""
        return 1.0 if y < 432 else 0.75 if y < 464 else 0.5 if y < 496 else 0.25 if y < 544 else 0.0

    def dark_at(y):
        return 0.55 if y >= 672 else 0.72 if y >= 656 else 0.86 if y >= 640 else 1.0

    # 석대
    put("platform", platform(), *PLATFORM_AT)

    # 복도 바닥과 벽
    floor_rows = list(range(CORRIDOR_TOP, H, 16))
    for y in floor_rows:
        w, d = warp_at(y), dark_at(y)
        for c in range(1, 6):
            put("corridor", tint(rng.choice(tiles["floor"]), w, d), CORRIDOR_X + 16 * c, y)
        if y >= WALL_TOP:
            put("corridor", tint(rng.choice(tiles["wall-left"]), w, d), CORRIDOR_X, y)
            put("corridor", tint(rng.choice(tiles["wall-right"]), w, d), CORRIDOR_X + 96, y)

    # 바닥 윗변과 벽 끝을 들쭉날쭉하게 끊는다
    cp = layers["corridor"].load()
    for x0 in range(CORRIDOR_X + 16, CORRIDOR_X + 96, 4):
        notch = rng.choice((0, 0, 1, 2, 3, 5))
        for y in range(CORRIDOR_TOP, CORRIDOR_TOP + notch):
            for x in range(x0, x0 + 4):
                cp[x, y] = (0, 0, 0, 0)
    for x0, depth in ((CORRIDOR_X, (7, 4, 9, 2)), (CORRIDOR_X + 96, (3, 8, 5, 10))):
        for i, notch in enumerate(depth):
            for y in range(WALL_TOP, WALL_TOP + notch):
                for x in range(x0 + 4 * i, x0 + 4 * i + 4):
                    cp[x, y] = (0, 0, 0, 0)
    # 벽이 없는 구간의 바닥 옆변도 4px 단위로 파낸다
    for y0 in range(CORRIDOR_TOP, WALL_TOP, 4):
        for side, x_edge in ((1, CORRIDOR_X + 16), (-1, CORRIDOR_X + 95)):
            notch = rng.choice((0, 1, 1, 2, 4))
            for k in range(notch):
                for y in range(y0, y0 + 4):
                    cp[x_edge + side * k, y] = (0, 0, 0, 0)

    # 떠다니는 조각에 쓸 다리 판(다리 자체는 bridge_frames가 따로 만든다)
    mid = Image.open(TEX / "Lib_Bridge3.png").convert("RGBA")
    off = Image.open(TEX / "Lib_Bridge0.png").convert("RGBA")
    slab_mid = [mid.crop((x0, y0, x1 + 1, y1 + 3)) for y0, y1, x0, x1 in ((2, 17, 17, 110), (36, 49, 21, 114), (68, 81, 11, 104))]
    slab_off = [off.crop((x0, y0, x1 + 1, y1 + 3)) for y0, y1, x0, x1 in ((7, 22, 17, 110), (25, 38, 14, 107), (41, 53, 21, 114), (57, 70, 17, 110), (73, 86, 11, 104))]

    # 바닥 조명: 온전한 구간은 켜진 등, 기운이 닿은 구간은 보랏빛으로 바랜 등
    lamp = Image.open(TEX / "Library_Light0.png").convert("RGBA")
    for y in range(CORRIDOR_TOP + 32, H - 16, 64):
        for c in (1, 5):
            put("lights", tint(lamp, warp_at(y), dark_at(y)), CORRIDOR_X + 16 * c, y)

    # 떠다니는 조각
    def floor_chunk(cols, rows, w):
        piece = Image.new("RGBA", (16 * cols, 16 * rows))
        for r in range(rows):
            for c in range(cols):
                if rows * cols > 2 and rng.random() < 0.18:
                    continue
                piece.paste(tint(rng.choice(tiles["floor"]), w), (16 * c, 16 * r))
        return thickness(piece)

    def wall_chunk(side, rows, w):
        piece = Image.new("RGBA", (16, 16 * rows))
        for r in range(rows):
            piece.paste(tint(rng.choice(tiles[side]), w), (0, 16 * r))
        px = piece.load()
        for i in range(4):  # 위아래 끝을 끊는다
            for y in range(rng.choice((1, 3, 5, 6))):
                for x in range(4 * i, 4 * i + 4):
                    px[x, y] = (0, 0, 0, 0)
            for y in range(rng.choice((0, 2, 4))):
                for x in range(4 * i, 4 * i + 4):
                    px[x, piece.height - 1 - y] = (0, 0, 0, 0)
        return piece

    def wall_top(x0, width, w):
        piece = tiles["wall-top"].crop((x0, 0, x0 + width, tiles["wall-top"].height))
        px = piece.load()
        for x in list(range(0, rng.choice((2, 4)))) + list(range(width - rng.choice((1, 3)), width)):
            for y in range(piece.height):
                if rng.random() < 0.7:
                    px[x, y] = (0, 0, 0, 0)
        return tint(piece, w)

    near = [  # (조각, x, y): 복도 끝에서 막 떨어져 나간 벽과 바닥, 걸어 다니는 자리와 겹치지 않게 둔다
        (wall_chunk("wall-left", 2, 1.0), 202, 420),
        (wall_chunk("wall-right", 1, 1.0), 323, 426),
        (wall_chunk("wall-right", 1, 0.75), 321, 446),
        (floor_chunk(1, 1, 1.0), 184, 404),
        (floor_chunk(1, 1, 0.75), 348, 440),
        (tint(slab_mid[0], 1.0), 76, 452),
        (tint(slab_mid[2], 1.0), 374, 446),
    ]
    for piece, x, y in near:
        put("near-debris", piece, x, y)

    far = [  # 멀리 가라앉은 어두운 다리 판과 바닥 조각 몇 개
        (slab_off[1], 28, 560), (slab_off[3], 412, 600),
        (tint(floor_chunk(1, 1, 1.0), 0, 0.6), 150, 520), (tint(floor_chunk(1, 1, 1.0), 0, 0.6), 388, 500),
        (tint(floor_chunk(1, 1, 1.0), 0, 0.6), 96, 70), (tint(floor_chunk(1, 1, 1.0), 0, 0.6), 430, 56),
    ]
    for piece, x, y in far:
        put("far-debris", piece, x, y)

    # 차원 균열: 흰색·청록을 석대의 보랏빛 계열로 바꿔 탄환·경고보다 뒤로 물린다
    crack_map = None

    def crack(name, turn=0, flip=False):
        nonlocal crack_map
        image = Image.open(TEX / name).convert("RGBA")
        if crack_map is None:
            colors = sorted({p[:3] for p in image.getdata() if p[3]}, key=sum, reverse=True)
            crack_map = {}
            for c in colors:
                crack_map[c] = (0xb9, 0xa8, 0xee) if sum(c) > 660 else (0x75, 0x63, 0xc2) if sum(c) > 380 else (0x3f, 0x34, 0x70)
        image = recolor(image, {**crack_map, **{p[:3]: crack_map.get(p[:3], (0x75, 0x63, 0xc2)) for p in image.getdata() if p[3]}})
        if flip:
            image = image.transpose(Image.FLIP_LEFT_RIGHT)
        return image.rotate(90 * turn)

    hole = recolor(Image.open(TEX / "DimensionCrack_Idle_Opened_InArea_00.png").convert("RGBA"), {(0, 0, 0): (0x0b, 0x09, 0x12)})
    for x, y, turn, flip in ((128, 404, 0, False),):
        put("cracks", hole, x, y)
        put("cracks", crack("DimensionCrack_Idle_Opened_Blank_00.png", turn, flip), x, y)
    for name, x, y, turn, flip in (
            ("DimensionCrack_Idle_Closed_00.png", CORRIDOR_X + 22, 420, 0, False),
            ("DimensionCrack_Idle_Closed_04.png", CORRIDOR_X + 58, 446, 1, True),
            ("DimensionCrack_Hit_00.png", CORRIDOR_X - 20, 470, 0, True),
            ("DimensionCrack_Idle_Closed_02.png", CORRIDOR_X + 84, 492, 2, False),
            ("DimensionCrack_Idle_Closed_06.png", CORRIDOR_X + 10, 538, 3, False)):
        put("cracks", crack(name, turn, flip), x, y)

    # 허공의 먼지 점: 석대와 복도 끝에 가까울수록 촘촘하게
    vp = layers["void"].load()
    for _ in range(520):
        x, y = rng.randrange(W), rng.randrange(H)
        distance = min(abs(y - 230) / 260, 1.0)
        if rng.random() < distance * 0.8:
            continue
        vp[x, y] = rng.choice(((0x3b, 0x38, 0x4e, 255), (0x2c, 0x29, 0x3d, 255), (0x57, 0x4c, 0x6f, 255)))

    # 허공과 맞닿은 조각에 어두운 테두리
    for name in ("corridor", "bridge", "near-debris", "far-debris"):
        layers[name] = outline(layers[name])
    return layers


def bridge_frames(cover, at=BRIDGE_AT):
    """다리의 참고 원본 여섯 장과 게임용 열 장(가라앉은 네 장 + 석대 색으로 물들인 참고 여섯 장)을 만든다.

    게임용 첫 장이 내려가 있는 모습, 끝 장이 다 올라온 모습이다. 올라올 때는 순서대로, 내려갈 때는 역순으로 재생한다.
    cover(입구는 복도 바닥, 출구는 석대 윗면)와 겹치는 자리는 지워, 판이 그 아래로 들어간 것처럼 보이게 한다.
    """
    reference = []
    for i in range(6):
        frame = Image.new("RGBA", BRIDGE_FRAME)
        frame.alpha_composite(Image.open(TEX / f"Lib_Bridge{i}.png").convert("RGBA"))
        reference.append(frame)
    game = []
    for sink, dark in BRIDGE_SINK:
        frame = Image.new("RGBA", BRIDGE_FRAME)
        frame.alpha_composite(tint(Image.open(TEX / "Lib_Bridge0.png").convert("RGBA"), 1.0, dark), (0, sink))
        game.append(frame)
    game += [tint(frame, 1.0) for frame in reference]

    floor = cover.getchannel("A").load()
    for frame in game:
        px = frame.load()
        for y in range(BRIDGE_FRAME[1]):
            for x in range(BRIDGE_FRAME[0]):
                cx, cy = at[0] + x, at[1] + y
                if px[x, y][3] and cx < cover.width and cy < cover.height and floor[cx, cy]:
                    px[x, y] = (0, 0, 0, 0)
    return reference, game


def extend(layers):
    """build()가 그린 층을 늘린 캔버스의 아래쪽에 옮기고, 위쪽에 빛의 문이 설 석판과 허공 먼지를 더한다."""
    rng = random.Random(1127)
    out = {}
    for name, layer in layers.items():
        grown = Image.new("RGBA", (W, FULL_H))
        grown.alpha_composite(layer, (0, TOP_EXTRA))
        out[name] = grown

    # 석판: 석대 윗면의 바탕색에 안쪽 홈 한 줄, 아래에 석대 옆면과 같은 색의 앞면
    slab = Image.new("RGBA", (W, FULL_H))
    px = slab.load()
    x0, y0, x1, y1 = LANDING
    for y in range(y0, y1):
        for x in range(x0, x1):
            inset = min(x - x0, x1 - 1 - x, y - y0, y1 - 1 - y)
            color = TOP_FACE[0] if inset == 0 and y == y0 else TOP_FACE[2] if inset == 3 else TOP_FACE[1]
            px[x, y] = color + (255,)
    for y in range(y1, y1 + LANDING_FACE):
        for x in range(x0, x1):
            row = y - y1
            px[x, y] = (STONE_C if row < 2 else STONE_F if row == LANDING_FACE - 1 else STONE_G) + (255,)
    out["exit-landing"] = outline(slab)

    # 위쪽 허공의 먼지 점: 석판에 가까울수록 촘촘하게
    vp = out["void"].load()
    center = ((x0 + x1) // 2, y0)
    for _ in range(260):
        x, y = rng.randrange(W), rng.randrange(TOP_EXTRA + 40)
        distance = ((x - center[0]) ** 2 + (y - center[1]) ** 2) ** 0.5
        if rng.random() < min(distance / 220.0, 1.0) * 0.85:
            continue
        vp[x, y] = rng.choice(((0x3b, 0x38, 0x4e, 255), (0x2c, 0x29, 0x3d, 255), (0x57, 0x4c, 0x6f, 255)))
    return out


def layout():
    """Unity 맵이 쓸 월드 배치: 벽은 걸어 다닐 수 없는 영역의 도형이다."""
    u = lambda px: px / 16.0
    floor_half = 2.5       # 복도 바닥 5칸과 다리의 절반 폭
    edge = -4.5625         # 석대 윗면의 아래쪽 끝(이전 맵의 벽과 같은 값)
    bottom = -24.0         # 복도에서 더 내려갈 수 없는 선
    top = 8.5              # 석대 윗면의 위쪽 끝(이전 맵의 벽과 같은 값)
    gate_y = u(FULL_ORIGIN[1] - GATE_BASE)             # 빛의 문의 문턱
    stop = gate_y + u(GATE_STOP)                       # 문 속으로 걸어 들어갈 수 있는 끝
    ceiling = u(FULL_ORIGIN[1])                        # 그림의 위쪽 끝
    walls = [
        {"name": "PlatformLeft", "kind": "box", "x": -19.5, "y": 2.0, "w": 15.0, "h": 14.0, "points": []},
        {"name": "PlatformRight", "kind": "box", "x": 19.5, "y": 2.0, "w": 15.0, "h": 14.0, "points": []},
        # 석대 위쪽은 출구 다리의 폭만 남기고 막는다. 다리가 내려가 있는 동안은 exitBridge의 blocker가 그 폭을 막는다
        {"name": "PlatformTopLeft", "kind": "box", "x": (-27.0 - floor_half) / 2, "y": (top + ceiling) / 2, "w": 27.0 - floor_half, "h": ceiling - top, "points": []},
        {"name": "PlatformTopRight", "kind": "box", "x": (27.0 + floor_half) / 2, "y": (top + ceiling) / 2, "w": 27.0 - floor_half, "h": ceiling - top, "points": []},
        {"name": "LightGateBack", "kind": "box", "x": 0.0, "y": (stop + ceiling) / 2, "w": 2 * floor_half + 2, "h": ceiling - stop, "points": []},
        # 문 기둥이 선 자리는 문턱부터 막는다(문 속의 폭은 3유닛)
        {"name": "LightGatePostLeft", "kind": "box", "x": -2.0, "y": (gate_y + stop) / 2, "w": 1.0, "h": stop - gate_y, "points": []},
        {"name": "LightGatePostRight", "kind": "box", "x": 2.0, "y": (gate_y + stop) / 2, "w": 1.0, "h": stop - gate_y, "points": []},
        {"name": "PlatformTopLeftCorner", "kind": "polygon", "x": 0, "y": 0, "w": 0, "h": 0, "points": [-12.0, 8.5, -9.95, 8.5, -12.0, 6.45]},
        {"name": "PlatformTopRightCorner", "kind": "polygon", "x": 0, "y": 0, "w": 0, "h": 0, "points": [12.0, 8.5, 12.0, 6.45, 9.95, 8.5]},
        {"name": "PlatformBottomLeftCorner", "kind": "polygon", "x": 0, "y": 0, "w": 0, "h": 0, "points": [-12.0, edge, -12.0, -2.5625, -10.0, edge]},
        {"name": "PlatformBottomRightCorner", "kind": "polygon", "x": 0, "y": 0, "w": 0, "h": 0, "points": [12.0, edge, 10.0, edge, 12.0, -2.5625]},
        {"name": "VoidLeft", "kind": "box", "x": (-27.0 - floor_half) / 2, "y": (edge - 30.0) / 2, "w": 27.0 - floor_half, "h": edge + 30.0, "points": []},
        {"name": "VoidRight", "kind": "box", "x": (27.0 + floor_half) / 2, "y": (edge - 30.0) / 2, "w": 27.0 - floor_half, "h": edge + 30.0, "points": []},
        {"name": "CorridorBottom", "kind": "box", "x": 0.0, "y": bottom - 3.0, "w": 2 * floor_half + 2, "h": 6.0, "points": []},
    ]
    return {
        "size": [W, FULL_H], "origin": list(FULL_ORIGIN),
        "cameraVerticalLimits": [u(FULL_ORIGIN[1] - FULL_H), u(FULL_ORIGIN[1])],
        "walls": walls,
        # 수정은 다리가 올라오는 모습이 Player 중심 화면에 들어오도록 복도 위쪽 끝에 둔다
        "entranceSpawn": [0.0, -19.5], "entranceStop": [0.0, -16.5], "checkpoint": [0.0, -16.0], "respawn": [0.0, -12.0], "crystal": [1.5, -10.9],
        "bossSpawn": [0.0, 3.5], "encounter": [0.0, 2.25, 23.0, 11.5],
        "bridge": {
            "position": [u(BRIDGE_AT[0] + BRIDGE_FRAME[0] // 2 - ORIGIN[0]), u(ORIGIN[1] - BRIDGE_AT[1])],  # 프레임의 위쪽 가운데
            "blocker": [0.0, (edge + u(ORIGIN[1] - CORRIDOR_TOP) + 0.0625) / 2, 2 * floor_half + 0.4, edge - u(ORIGIN[1] - CORRIDOR_TOP) - 0.0625],  # 내려가 있는 동안 막을 영역의 월드 중심과 크기
            "frameSeconds": BRIDGE_FRAME_MS / 1000.0,
        },
        # 처치 뒤에 올라오는 출구 다리: 석대 위쪽 끝에서 석판까지
        "exitBridge": {
            "position": [u(EXIT_BRIDGE_AT[0] + BRIDGE_FRAME[0] // 2 - FULL_ORIGIN[0]), u(FULL_ORIGIN[1] - EXIT_BRIDGE_AT[1])],
            "blocker": [0.0, (top + u(FULL_ORIGIN[1] - LANDING[3])) / 2, 2 * floor_half + 0.4, u(FULL_ORIGIN[1] - LANDING[3]) - top],
            "frameSeconds": BRIDGE_FRAME_MS / 1000.0,
        },
        # 빛의 문: 문턱 가운데의 월드 좌표와 그 기준의 진입 감지 범위(x, y, 폭, 높이), 문 앞 바닥의 빛 번짐이 놓일 자리
        "lightGate": {"position": [0.0, gate_y], "trigger": [0.0, 0.75, 3.0, 0.3], "spill": [0.0, gate_y]},
    }


def main():
    if "--harvest" in sys.argv or not (HERE / "facility-tiles.png").exists():
        harvest()
    base = build()
    from aseio import composite, read_ase, write_ase
    import gate

    # 다리는 맵 그림에 굽지 않는다. 층 원본과 미리보기에는 다 올라온 모습을 참고용으로만 올린다
    reference, game = bridge_frames(base["corridor"])
    layers = extend(base)
    names = list(layers)
    entry_at = (BRIDGE_AT[0], BRIDGE_AT[1] + TOP_EXTRA)
    layers["bridge"].alpha_composite(game[-1], entry_at)
    _, exit_game = bridge_frames(layers["platform"], EXIT_BRIDGE_AT)
    _, light, spill, _ = gate.build()
    gate_at = (FULL_ORIGIN[0] - light.width // 2, GATE_BASE - light.height)
    spill_at = (FULL_ORIGIN[0] - spill.width // 2, GATE_BASE)
    objects = Image.new("RGBA", (W, FULL_H))   # 따로 놓는 출구 객체의 참고 모습
    objects.alpha_composite(exit_game[-1], EXIT_BRIDGE_AT)
    objects.alpha_composite(spill, spill_at)
    objects.alpha_composite(light, gate_at)
    flat = Image.new("RGBA", (W, FULL_H))
    for name in names:
        if name != "bridge":
            flat.alpha_composite(layers[name])
    flat.save(HERE / "warped-facility.png")
    json.dump(layout(), open(HERE / "map-layout.json", "w", encoding="utf8"), indent=1)

    backdrop = Image.new("RGBA", (W, FULL_H), BACKDROP)
    shown = ["bridge (separate object, not exported)" if n == "bridge" else n for n in names]
    write_ase(HERE / "warped-facility.aseprite", (W, FULL_H), ["backdrop (not exported)"] + shown + ["exit bridge and light gate (separate objects, not exported)"],
              [100], [], [[backdrop] + [layers[n] for n in names] + [objects]])

    # 다리 원본: 참고 프레임을 그대로 옮긴 구간과 게임용 구간을 한 파일에 둔다
    frames = reference + game
    tags = [("Reference - Open", 1, 6), ("Opening", 7, 6 + len(game)), ("Closed", 7, 7), ("Open", 6 + len(game), 6 + len(game))]
    write_ase(HERE / "bridge.aseprite", BRIDGE_FRAME, ["bridge"], [BRIDGE_FRAME_MS] * len(frames), tags, [[f] for f in frames])
    out = HERE / "bridge-frames"
    out.mkdir(exist_ok=True)
    for old in out.glob("*.png"):
        old.unlink()
    for i, frame in enumerate(game):
        frame.save(out / f"bridge-{i:02d}.png")
    out = HERE / "exit-bridge-frames"
    out.mkdir(exist_ok=True)
    for old in out.glob("*.png"):
        old.unlink()
    for i, frame in enumerate(exit_game):
        frame.save(out / f"exit-bridge-{i:02d}.png")

    # 저장한 파일을 다시 읽어 참고 구간이 원본 텍스처·순서·시간과 같은지 대조
    def visible(image):
        mask = image.getchannel("A").point(lambda v: 255 if v else 0)
        return Image.composite(image, Image.new("RGBA", image.size), mask).tobytes()

    size, _, durations, _, saved = read_ase(HERE / "bridge.aseprite")
    same = durations[:6] == [BRIDGE_FRAME_MS] * 6 and all(
        visible(composite(size, saved[i]).crop((0, 0, 128, 112))) == visible(Image.open(TEX / f"Lib_Bridge{i}.png").convert("RGBA")) for i in range(6))
    print(f"bridge: reference section identical to source={same} game frames={len(game)} frame={BRIDGE_FRAME_MS}ms")

    # 미리보기: 처치 뒤(출구 다리가 올라옴)와 전투 중(두 다리가 모두 내려감)
    preview = Image.new("RGBA", (W, FULL_H), BACKDROP)
    preview.alpha_composite(flat)
    preview.alpha_composite(game[-1], entry_at)
    preview.alpha_composite(objects)
    preview.save(HERE / "preview.png")
    lowered = Image.new("RGBA", (W, FULL_H), BACKDROP)
    lowered.alpha_composite(flat)
    lowered.alpha_composite(game[0], entry_at)
    lowered.alpha_composite(exit_game[0], EXIT_BRIDGE_AT)
    lowered.alpha_composite(spill, spill_at)
    lowered.alpha_composite(light, gate_at)
    lowered.save(HERE / "preview-bridge-lowered.png")
    colors = {p[:3] for p in flat.getdata() if p[3]}
    print(f"layers={len(names)} colors={len(colors)} size={W}x{FULL_H}")


if __name__ == "__main__":
    main()
