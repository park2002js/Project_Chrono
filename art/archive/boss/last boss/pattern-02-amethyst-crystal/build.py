# -*- coding: utf-8 -*-
"""패턴 2에서 땅에서 솟는 물체를 한 꼭짓점이 위로 솟은 자수정으로 다시 그린다.

- 크기와 움직임은 기존 돌기(objects.aseprite의 `P2 - Amethyst tentacle 1~3`, Object 레이어)를 따른다.
  세 종류 각각의 폭·높이와, 프레임마다 땅 위로 드러난 높이(솟음·유지·가라앉음 39장, 장당 0.05초)를 그대로 쓴다.
- 모양은 육각 기둥 위에 뾰족한 끝이 하나 달린 수정이다. 왼쪽 위에서 빛을 받는 것으로 보고
  끝의 세 면과 기둥의 세 면을 밝은 면·중간 면·그늘 면으로 나누고, 땅에 가까울수록 한 단계 어둡게 해 두께가 읽히게 한다.
  색은 기존 돌기의 다섯 색과 끝 장에 쓰인 가장 밝은 한 색만 쓴다.
- 구멍에서 솟은 것으로 보이게 하는 네 가지를 기존 돌기에서 옮겼다.
  ① 기둥 폭(19~21px)을 구멍의 가장 좁은 폭(23~25px)보다 좁게 잡아 양옆으로 구멍 안쪽이 보인다.
  ② 땅 높이를 구멍 가운데 줄(PIT_LINE)에 두고, 그 아래는 구멍 안으로 본다.
  ③ 구멍 안 부분은 구멍 속과 비슷한 어두운 색으로만 칠하고 밑면을 따로 그리지 않으며, 구멍 그림 밖으로 나가지 않게 자른다.
  ④ 어두워지는 경계는 수정이 아니라 구멍에 붙어 있어, 솟거나 가라앉을 때 구멍을 지나는 부분만 밝아지거나 어두워진다.
- 바닥의 균열·차원문·그림자 레이어와 파편 레이어는 기존 그림을 그대로 옮긴다.

결과: 이 폴더의 amethyst-crystal.aseprite(게임용 원본), amethyst-crystal-sheet.png(세 종류의 유지 모습과 기존 돌기 비교)
"""
import math
import sys
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
sys.path.insert(0, str(ROOT / "art/work/chrono_askard_vfx_work"))
from aseio import read_ase, write_ase  # noqa: E402

SOURCE = HERE.parent / "objects.aseprite"
HI, LIGHT, LIT, MID, SHADE, DARK = "#e0b5ff", "#c26bff", "#9d5cdc", "#7542bb", "#613a9a", "#4c3178"
DEEP = "#3f347f"  # 구멍 속 맨 아래에서만 쓰는 색: 수정의 가장 어두운 보라와 구멍 속 남색(#373c8f)의 사이
STEPS = [HI, LIGHT, LIT, MID, SHADE, DARK]  # 밝은 색부터 어두운 색까지


def rgb(code):
    return tuple(int(code[i:i + 2], 16) for i in (1, 3, 5)) + (255,)


def darker(code, steps):
    return STEPS[min(len(STEPS) - 1, STEPS.index(code) + steps)]


# 종류별 꼭짓점(캔버스 좌표): 끝점, 어깨 네 점(왼쪽 윤곽·앞면 왼쪽 모서리·앞면 오른쪽 모서리·오른쪽 윤곽), 밑동 네 점
SHAPES = [  # 어깨와 밑동의 비스듬한 변은 가로 2px마다 세로 1px씩 변하게 잡아 계단 폭이 고르게 나온다
    {"apex": (32, 3), "shoulder": [(22, 23), (28, 26), (38, 26), (42, 24)], "base": [(23, 51), (28, 54), (38, 54), (41, 52)]},
    {"apex": (31, 6), "shoulder": [(22, 26), (26, 28), (36, 28), (42, 25)], "base": [(23, 52), (26, 54), (36, 54), (41, 51)]},
    {"apex": (33, 3), "shoulder": [(23, 23), (29, 26), (38, 26), (42, 24)], "base": [(24, 51), (29, 54), (38, 54), (41, 52)]},
]
HOLE_FRAME = 22  # 구멍이 다 열려 머무는 장(종류별 첫 장 기준)


def pit_line(x):
    """땅 높이: 구멍 가운데를 지나는 앞쪽 호이며 이 줄 아래는 구멍 안이다."""
    return 46 - int(((x - 32) / 10) ** 2 * 1.5)


def pit_floor(x):
    """구멍 안에서 수정이 보이는 아래 끝"""
    return 52 - int(((x - 32) / 10) ** 2 * 2)


def along(p, q, y):
    """선분 p→q가 줄 y에서 지나는 열(끝점 밖은 q의 열)"""
    if y >= q[1]:
        return q[0]
    return math.floor(p[0] + (q[0] - p[0]) * (y - p[1]) / (q[1] - p[1]) + 0.5)


def height_at(points, x):
    """꺾은선이 열 x에서 지나는 줄"""
    if x <= points[0][0]:
        return points[0][1]
    for a, b in zip(points, points[1:]):
        if x <= b[0]:
            return math.floor(a[1] + (b[1] - a[1]) * (x - a[0]) / (b[0] - a[0])) if b[0] != a[0] else b[1]
    return points[-1][1]


def crystal(shape, size):
    """유지 상태의 수정 한 장: 면마다 한 색으로 칠하고 밑동을 어둡게 한 뒤 모서리 빛과 결을 넣는다."""
    apex, shoulder, base = shape["apex"], shape["shoulder"], shape["base"]
    image = Image.new("RGBA", size)
    px = image.load()
    faces = {}
    for y in range(apex[1], max(p[1] for p in base) + 1):
        left, right = along(apex, shoulder[0], y), along(apex, shoulder[3], y)
        edge1, edge2 = along(apex, shoulder[1], y), along(apex, shoulder[2], y)
        for x in range(left, right + 1):
            if y > height_at(base, x):
                continue
            band = "L" if x < edge1 else "F" if x <= edge2 else "R"
            tip = y < height_at(shoulder, x)
            color = {("L", True): LIGHT, ("F", True): LIT, ("R", True): SHADE, ("L", False): LIT, ("F", False): MID, ("R", False): SHADE}[(band, tip)]

            # 땅에 가까울수록 한 단계 어둡게: 그늘 면일수록 어둠이 높이 올라온다
            if not tip and pit_line(x) - y < {"L": 6, "F": 9, "R": 13}[band]:
                color = darker(color, 1)
            px[x, y] = rgb(color)
            faces[(x, y)] = (band, tip)

    def dot(x, y, color):
        if (x, y) in faces:
            px[x, y] = rgb(color)

    # 빛 받는 모서리: 끝의 왼쪽 능선과 기둥의 앞면 왼쪽 모서리
    for y in range(apex[1] + 3, shoulder[1][1]):
        if (y - apex[1]) % 5 != 4:
            dot(along(apex, shoulder[1], y) - 1, y, HI if y < apex[1] + 12 else LIGHT)
    for y in range(shoulder[1][1] + 1, shoulder[1][1] + 15):
        dot(shoulder[1][0], y, LIGHT if y < shoulder[1][1] + 9 else LIT)

    # 앞면 왼쪽 모서리 안쪽의 반사 줄 하나와, 앞면·그늘 면이 만나는 모서리의 짙은 줄
    fx, fy = shoulder[1][0], shoulder[1][1]
    for y in range(fy + 4, fy + 11):
        dot(fx + 3, y, LIT)
    for y in range(shoulder[2][1] + 1, shoulder[2][1] + 12):
        dot(shoulder[2][0] + 1, y, DARK if y % 6 else SHADE)
    for y in range(apex[1] + 8, apex[1] + 15):
        dot(along(apex, shoulder[2], y) + 1, y, DARK)
    return image


def outlined(image):
    """투명과 맞닿은 실루엣 안쪽 1px를 가장 어두운 색으로 둘러 끊기지 않는 외곽선을 만든다."""
    alpha = image.getchannel("A").load()
    out = image.copy()
    px = out.load()
    w, h = image.size
    for y in range(h):
        for x in range(w):
            if not alpha[x, y]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if not (0 <= nx < w and 0 <= ny < h) or not alpha[nx, ny]:
                    px[x, y] = rgb(DARK)
                    break
    return out


def recolored(image, mapping):
    table = {rgb(a): rgb(b) for a, b in mapping.items()}
    out = image.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            if px[x, y] in table:
                px[x, y] = table[px[x, y]]
    return out


ERUPT = {HI: LIT, LIGHT: MID, LIT: DARK, MID: DARK, SHADE: DARK}   # 솟는 첫 장: 어두운 실루엣에 빛 받는 면만 남긴다
FLASH = {LIGHT: HI, LIT: LIGHT, MID: LIT}                          # 다 솟은 첫 장: 한 단계 밝게


PIT = {HI: SHADE, LIGHT: SHADE, LIT: SHADE, MID: DARK, SHADE: DARK}  # 구멍 안: 구멍 속과 비슷한 어두운 두 색만 남긴다


def frame(shape, body, top, bottom, size, state, hole):
    """수정을 아래로 내려 윗변이 top 줄에 오게 한 한 장.

    땅 높이 아래로 내려간 부분은 구멍 안의 색으로 바꾸고 구멍 그림 안쪽에서만 보이게 한다.
    끝만 살짝 드러난 장(구멍이 열리기 전후)은 균열 가운데인 bottom 줄에서 자른다.
    """
    shift = top - shape["apex"][1]
    canvas = Image.new("RGBA", size)
    src, dst = body.load(), canvas.load()
    opening = hole.load()
    flat = bottom < 49
    pit = {rgb(a): rgb(b) for a, b in PIT.items()}
    inside = {}  # 구멍 안에 그린 칸: 외곽선을 두르지 않고 아래로 갈수록 구멍 속 색에 가까워진다
    for y in range(size[1] - shift):
        for x in range(size[0]):
            if not src[x, y][3]:
                continue
            ty = y + shift
            if flat:
                if ty <= bottom - 1:
                    dst[x, ty] = src[x, y]
            elif ty <= pit_line(x):
                dst[x, ty] = src[x, y]
            elif ty <= pit_floor(x) and opening[x, ty][3]:
                left = pit_floor(x) - ty
                inside[(x, ty)] = rgb(DEEP) if left < 2 else rgb(DARK) if left < 4 else pit.get(src[x, y], src[x, y])
                dst[x, ty] = inside[(x, ty)]
    canvas = outlined(recolored(canvas, {"erupt": ERUPT, "flash": FLASH}.get(state, {})))
    dst = canvas.load()
    for (x, ty), color in inside.items():
        dst[x, ty] = color
    return canvas


def main():
    size, names, durations, tags, old = read_ase(SOURCE)
    frames, new_tags = [], []
    holds, olds = [], []
    for index, (name, first, last) in enumerate(tags[:3]):
        shape = SHAPES[index]
        body = crystal(shape, size)
        hole = old[first - 1 + HOLE_FRAME - 1][0]
        for number in range(first, last + 1):
            ground, spike, fragments = old[number - 1]
            layer = None
            box = spike.getbbox() if spike is not None else None
            if box:
                local = number - first + 1
                state = "erupt" if local == 14 else "flash" if local == 15 else "hold"
                layer = frame(shape, body, box[1], box[3], size, state, hole)
            frames.append([ground, layer, fragments])
        new_tags.append((name.replace("tentacle", "crystal"), len(frames) - (last - first), len(frames)))
        hold = Image.new("RGBA", size)
        hold.alpha_composite(hole)
        hold.alpha_composite(frame(shape, body, shape["apex"][1], 99, size, "hold", hole))
        holds.append(hold)
        before = Image.new("RGBA", size)
        before.alpha_composite(hole)
        before.alpha_composite(old[first - 1 + HOLE_FRAME - 1][1])
        olds.append(before)
    count = len(frames)
    write_ase(HERE / "amethyst-crystal.aseprite", size, [names[0], "Crystal", names[2]], durations[:count], new_tags, frames)

    # 검수용: 위 줄은 새 수정, 아래 줄은 기존 돌기(구멍과 함께 6배, 오른쪽은 원래 크기)
    scale = 6
    sheet = Image.new("RGBA", (size[0] * scale * 3 + size[0] * 3 + 28, size[1] * scale * 2 + 12), (150, 140, 150, 255))
    for row, images in enumerate((holds, olds)):
        for i, image in enumerate(images):
            sheet.alpha_composite(image.resize((size[0] * scale, size[1] * scale), Image.NEAREST), (i * (size[0] * scale + 4) + 4, row * (size[1] * scale + 4) + 4))
            sheet.alpha_composite(image, (size[0] * scale * 3 + 20 + i * size[0], row * (size[1] * scale + 4) + 4))
    sheet.save(HERE / "amethyst-crystal-sheet.png")
    for i, image in enumerate(holds):
        box = image.getbbox()
        colors = {p[:3] for p in image.getdata() if p[3]}
        print(f"crystal {i + 1} with hole: box={box} colors={len(colors)}")
    print(f"frames={count} tags={[t[0] for t in new_tags]}")


if __name__ == "__main__":
    main()
