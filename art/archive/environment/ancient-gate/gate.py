# -*- coding: utf-8 -*-
"""연구 시설 아래에서 드러난 석조 문 한 벌을 그린다. 같은 문틀에 속만 어둠과 빛으로 다르게 채운다.

- 문틀: 최종 보스 석대의 옆면과 같은 돌 색·벽돌 단으로 쌓은 두 기둥과 상인방. 장식은 두지 않는다.
- 어둠의 문(경비 맵의 심층 통로 끝): 속을 허공색으로 채우고 안쪽 가장자리에 차원 균열과 같은 보랏빛 테를 두른다.
- 빛의 문(최종 보스 맵의 위쪽 끝): 속을 흰빛으로 채우고 기둥 안쪽 면과 문 앞 바닥에 빛이 번진다.

좌표: 1px = 원본 1px, 16px = 1유닛. 그림의 아래 가운데가 문턱의 가운데다.
결과: 이 폴더의 ancient-gate.aseprite(층 분리 원본), gate-dark.png, gate-light.png, gate-light-spill.png, preview.png
"""
import random
import sys
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
sys.path.insert(0, str(ROOT / "art/work/chrono_askard_vfx_work"))

W, H = 80, 58                # 문틀 전체 크기
POST = 14                    # 기둥 폭
OVERHANG = 2                 # 상인방이 기둥 밖으로 나온 폭
CAP, LINTEL = 6, 12          # 상인방 윗면과 앞면의 높이
OPEN_X0, OPEN_X1 = OVERHANG + POST, W - OVERHANG - POST   # 문 속의 좌우 끝(오른쪽은 다음 열)
OPEN_Y0 = CAP + LINTEL       # 문 속의 위쪽 끝
SPILL = (112, 26)            # 문 앞 바닥에 번지는 빛의 그림 크기(최종 보스 맵의 석판 안에 들어가는 높이)

# 석대 옆면과 같은 돌 색: 빛 받는 면 → 기본 → 어두운 면 → 줄눈
STONE_H, STONE_C, STONE_G, STONE_I, STONE_F = (0x57, 0x4c, 0x5f), (0x49, 0x40, 0x52), (0x3c, 0x35, 0x48), (0x34, 0x2f, 0x3c), (0x2c, 0x2c, 0x2c)
EDGE = (0x1b, 0x18, 0x26)    # 허공과 맞닿는 바깥 테두리
VOID = (0x0b, 0x09, 0x12)    # 문 속의 어둠
RIFT = ((0x3f, 0x34, 0x70), (0x75, 0x63, 0xc2), (0xb9, 0xa8, 0xee))   # 차원 균열과 같은 보랏빛 세 단계
LIGHT = ((0xf6, 0xf8, 0xff), (0xd5, 0xde, 0xfb), (0xa9, 0xb4, 0xe4))  # 빛의 속 → 가장자리 → 돌에 번진 빛


def frame():
    """문틀: 상인방 윗면·앞면과 두 기둥. 문 속은 투명하게 비워 둔다."""
    image = Image.new("RGBA", (W, H))
    px = image.load()
    rng = random.Random(7)

    def put(x, y, color):
        px[x, y] = color + (255,)

    # 상인방 윗면: 가장자리 한 줄을 뺀 안쪽이 가장 밝다
    for y in range(CAP):
        for x in range(W):
            put(x, y, STONE_C if y in (0, CAP - 1) or x in (0, W - 1) else STONE_H)

    # 상인방 앞면: 줄눈 한 줄로 나뉜 두 단의 벽돌
    for y in range(CAP, CAP + LINTEL):
        row = y - CAP
        for x in range(W):
            put(x, y, STONE_F if row == 6 else STONE_G)
    for first, last, offset in ((0, 5, 9), (7, LINTEL - 1, 0)):
        x = offset
        while x < W:
            for y in range(CAP + first, CAP + last + 1):
                if 0 < x < W - 1:
                    put(x, y, STONE_I)
            x += rng.choice((16, 18, 20))
    for x in range(W):                      # 상인방 아래 그늘 한 줄
        put(x, CAP + LINTEL - 1, STONE_I)

    # 두 기둥: 8줄 높이의 벽돌 단과 줄눈, 문 속과 맞닿는 안쪽 두 열은 그늘
    for x0 in (OVERHANG, W - OVERHANG - POST):
        inner = (x0 + POST - 2, x0 + POST - 1) if x0 == OVERHANG else (x0, x0 + 1)
        for y in range(OPEN_Y0, H):
            course = (y - OPEN_Y0) // 8
            joint = (y - OPEN_Y0) % 8 == 7
            for x in range(x0, x0 + POST):
                color = STONE_G if course % 2 == 0 else STONE_I
                if x in inner:
                    color = STONE_I
                elif x in (x0, x0 + POST - 1):
                    color = STONE_C
                if joint:
                    color = STONE_F
                put(x, y, color)
        for x in range(x0, x0 + POST):      # 상인방이 드리운 그늘 두 줄과 바닥에 닿는 밑줄
            put(x, OPEN_Y0, STONE_F)
            put(x, OPEN_Y0 + 1, STONE_I)
            put(x, H - 1, STONE_F)
    return image


def fill(color):
    """문 속을 한 색으로 채운 층"""
    image = Image.new("RGBA", (W, H))
    px = image.load()
    for y in range(OPEN_Y0, H):
        for x in range(OPEN_X0, OPEN_X1):
            px[x, y] = color + (255,)
    return image


def dark_rim():
    """어둠의 안쪽 가장자리를 따라 끊어진 보랏빛 테와 안으로 뻗는 잔금"""
    image = Image.new("RGBA", (W, H))
    px = image.load()
    rng = random.Random(41)
    border = [(x, OPEN_Y0) for x in range(OPEN_X0, OPEN_X1)]
    border += [(OPEN_X0, y) for y in range(OPEN_Y0, H)] + [(OPEN_X1 - 1, y) for y in range(OPEN_Y0, H)]
    for x, y in border:
        roll = rng.random()
        if roll < 0.62:
            px[x, y] = RIFT[0] + (255,)
        elif roll < 0.80:
            px[x, y] = RIFT[1] + (255,)
    # 가장자리에서 안으로 뻗는 잔금: 한 칸씩 꺾이며 옅어진다
    for sx, sy, dx, dy, length in ((OPEN_X0 + 1, OPEN_Y0 + 9, 1, 1, 7), (OPEN_X1 - 2, OPEN_Y0 + 5, -1, 1, 9), (OPEN_X0 + 13, OPEN_Y0 + 1, 1, 1, 6),
                                   (OPEN_X1 - 2, OPEN_Y0 + 24, -1, -1, 6), (OPEN_X0 + 1, OPEN_Y0 + 28, 1, -1, 5), (OPEN_X1 - 17, OPEN_Y0 + 1, -1, 1, 5)):
        x, y = sx, sy
        for i in range(length):
            if OPEN_X0 <= x < OPEN_X1 and OPEN_Y0 <= y < H:
                px[x, y] = (RIFT[2] if i == 0 else RIFT[1] if i < length // 2 else RIFT[0]) + (255,)
            if rng.random() < 0.5:
                x += dx
            else:
                y += dy
    return image


def light_rim():
    """빛의 안쪽 가장자리 두 줄과 기둥·상인방 안쪽 면에 번진 빛"""
    image = Image.new("RGBA", (W, H))
    px = image.load()
    for y in range(OPEN_Y0, H):
        for x in range(OPEN_X0, OPEN_X1):
            depth = min(x - OPEN_X0, OPEN_X1 - 1 - x, y - OPEN_Y0)
            if depth == 0:
                px[x, y] = LIGHT[2] + (255,)
            elif depth <= 2:
                px[x, y] = LIGHT[1] + (255,)
    for y in range(OPEN_Y0 + 2, H - 1):     # 기둥의 안쪽 두 열
        for x in (OPEN_X0 - 1, OPEN_X1):
            px[x, y] = LIGHT[2] + (255,)
        for x in (OPEN_X0 - 2, OPEN_X1 + 1):
            px[x, y] = STONE_H + (255,)
    for x in range(OPEN_X0, OPEN_X1):       # 상인방 아랫줄
        px[x, OPEN_Y0 - 1] = LIGHT[2] + (255,)
    return image


def light_spill():
    """문 앞 바닥에 번지는 빛: 문턱에서 멀어질수록 넓어지고 네 단계로 옅어진다. 그림의 위쪽 가운데가 문턱의 가운데다."""
    image = Image.new("RGBA", SPILL)
    px = image.load()
    cx = SPILL[0] // 2
    opening = (OPEN_X1 - OPEN_X0) // 2
    for y in range(SPILL[1]):
        half = opening + y * (cx - opening) // SPILL[1]
        alpha = 150 if y < 5 else 96 if y < 11 else 52 if y < 18 else 24
        for x in range(cx - half, cx + half):
            edge = min(x - (cx - half), cx + half - 1 - x)
            px[x, y] = LIGHT[0] + (alpha if edge >= 3 else alpha * 2 // 3 if edge >= 1 else alpha // 3,)
    return image


def outline(layer):
    """문틀의 바깥 1px에 어두운 테두리를 둘러 허공과 검은 바깥에서 떼어 보이게 한다. 문 속과 아래쪽에는 두르지 않는다."""
    out = Image.new("RGBA", (W + 2, H + 1))
    out.alpha_composite(layer, (1, 1))
    alpha = out.getchannel("A").load()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            if alpha[x, y] or (OPEN_X0 + 1 <= x < OPEN_X1 + 1 and y > OPEN_Y0):
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < out.width and 0 <= ny < out.height and alpha[nx, ny]:
                    px[x, y] = EDGE + (255,)
                    break
    return out


def build():
    """(어둠의 문, 빛의 문, 빛 번짐, 층 목록)을 만든다. 문 그림은 테두리 때문에 82×59이며 문틀은 (1, 1)에서 시작한다."""
    stone = outline(frame())
    layers = {"dark-fill": fill(VOID), "dark-rim": dark_rim(), "light-fill": fill(LIGHT[0]), "light-rim": light_rim()}

    def stack(*names):
        out = Image.new("RGBA", stone.size)
        for name in names:
            out.alpha_composite(layers[name], (1, 1))
        out.alpha_composite(stone)
        return out

    dark = stack("dark-fill", "dark-rim")
    light = stack("light-fill")
    light.alpha_composite(layers["light-rim"], (1, 1))   # 기둥 안쪽 면의 빛은 문틀 위에 얹는다
    layers["stone"] = stone
    return dark, light, light_spill(), layers


def main():
    from aseio import write_ase
    dark, light, spill, layers = build()
    dark.save(HERE / "gate-dark.png")
    light.save(HERE / "gate-light.png")
    spill.save(HERE / "gate-light-spill.png")
    size = dark.size

    def padded(layer):
        if layer.size == size:
            return layer
        out = Image.new("RGBA", size)
        out.alpha_composite(layer, (1, 1))
        return out

    names = ["dark-fill", "dark-rim", "light-fill", "stone", "light-rim"]
    write_ase(HERE / "ancient-gate.aseprite", size, names, [100], [], [[padded(layers[n]) for n in names]])

    # 미리보기: 검은 바깥의 어둠의 문과 허공색 위의 빛의 문을 나란히, 3배로
    sheet = Image.new("RGBA", (size[0] * 2 + SPILL[0] + 40, size[1] + SPILL[1] + 16), (0, 0, 0, 255))
    right = Image.new("RGBA", (size[0] + SPILL[0], sheet.height), (22, 20, 29, 255))
    sheet.alpha_composite(right, (size[0] + 30, 0))
    sheet.alpha_composite(dark, (10, 8))
    gx = size[0] + 30 + SPILL[0] // 2
    sheet.alpha_composite(spill, (gx + size[0] // 2 - SPILL[0] // 2, 8 + size[1]))
    sheet.alpha_composite(light, (gx, 8))
    sheet.resize((sheet.width * 3, sheet.height * 3), Image.NEAREST).save(HERE / "preview.png")
    print("gate", size, "colors dark", len({p[:3] for p in dark.getdata() if p[3]}), "light", len({p[:3] for p in light.getdata() if p[3]}))


if __name__ == "__main__":
    main()
