# -*- coding: utf-8 -*-
"""경비 복부 총구의 발사 준비 효과를 만든다.

사용법: python muzzle-charge.py [미리보기 PNG를 둘 폴더]
결과(이 폴더): muzzle-charge.aseprite (45×44, 30장·장당 50ms = 1.5초, 레이어 핵·원·연기)

구성
  원   : 총구 바깥에서 점선으로 생긴 원이 좁혀 들어와 총구 테두리에 맺히고, 테두리가 노랑에서 흰빛으로 달아오른다.
  연기 : 총구 둘레에 맺힌 연기 덩이가 소용돌이 가닥으로 늘어나며 구멍 안으로 빨려 들어간다.
  핵   : 연기를 다 삼킨 뒤 구멍 속이 달아올라 발사 섬광의 첫 장으로 이어진다.
캔버스의 가로 22.5·세로 22 지점이 총구 그림(9×8)의 한가운데이며, 왼쪽 총구 기준으로 그린다. 오른쪽 총구는 좌우로 뒤집어 쓴다.
색은 기존 발사 섬광(muzzle-left/right)의 주황 계열과 참고 게임 먼지 효과의 회보라 계열을 따른다.
Aseprite 실행 파일은 환경 변수 CHRONO_ASEPRITE 또는 아래 기본 경로에서 찾는다.
"""
import math, os, shutil, struct, subprocess, sys, zlib

HERE = os.path.dirname(os.path.abspath(__file__))
ASE = next((p for p in [os.environ.get("CHRONO_ASEPRITE", ""), "D:/02_Games/01_Steam/Library/steamapps/common/Aseprite/Aseprite.exe",
                        "D:/02_Games/01_Steam/SteamClient/steamapps/common/Aseprite/Aseprite.exe"] if p and os.path.exists(p)), None)

W, H = 45, 44
FRAMES, FRAME_MS = 30, 50
CX, CY = 22.0, 22.0  # 왼쪽 총구 테두리 원의 중심(픽셀 경계 좌표)
MUZZLE_X, MUZZLE_Y = 18, 18  # 총구 그림 9×8의 왼쪽 위 칸

# 총구 그림: L 밝은 테두리, R 어두운 테두리, S 그림자, H 구멍
MUZZLE = ["..LRLR...",
          ".LRHHRRS.",
          "RLHHHHRRS",
          "LHHHHHHRS",
          "RHHHHHHRS",
          "RRHHHHRRS",
          ".RRHHRRS.",
          "..RRRRS.."]

CLEAR = (0, 0, 0, 0)
EMBER = (0xB5, 0x44, 0x21, 255)   # 총구 테두리의 밝은 색
ORANGE = (0xF2, 0x95, 0x3B, 255)  # 발사 섬광의 바깥 색
YELLOW = (0xFC, 0xC1, 0x5A, 255)  # 발사 섬광의 몸통 색
HOT = (0xFF, 0xFA, 0xE0, 255)     # 발사 섬광의 핵 색
SMOKE_LIGHT = (0xE6, 0xE3, 0xEC, 235)  # 덩이의 밝은 속
SMOKE_MID = (0xB4, 0xAE, 0xC2, 215)    # 덩이의 그늘과 줄기
SMOKE_DIM = (0x96, 0x8B, 0x9D, 150)    # 막 맺히는 연기와 줄기 끝

# 연기 가닥: (태어나는 장, 시작 각도°, 시작 반지름px, 휘는 각도°, 덩이 반지름px). 각도는 오른쪽 0°·아래 90°이며 수정 쪽(오른쪽)은 비워 둔다
WISP_LIFE = 10
WISPS = [(3, 200, 13.5, 100, 2.3), (5, 110, 12.5, 105, 2.1), (8, 262, 13.0, 95, 2.2), (10, 150, 14.0, 100, 2.4),
         (12, 232, 12.5, 105, 2.1), (14, 88, 13.0, 100, 2.2), (15, 185, 14.0, 95, 2.4), (16, 278, 12.0, 100, 2.0)]
HOLE_R = 2.3  # 연기가 사라지는 반지름. 구멍 가장자리 한 칸 안쪽에서 끊어 구멍 속에 조각이 남지 않게 함


def blank():
    return [[CLEAR] * W for _ in range(H)]


def write_png(path, px):
    raw = bytearray()
    for row in px:
        raw.append(0)
        for c in row:
            raw.extend(c)

    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)

    data = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", len(px[0]), len(px), 8, 6, 0, 0, 0))
    data += chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(data)


def muzzle_cells(kinds):
    return [(MUZZLE_X + x, MUZZLE_Y + y, ch) for y, row in enumerate(MUZZLE) for x, ch in enumerate(row) if ch in kinds]


# ── 원 ───────────────────────────────────────────────────────
def circle(px, radius, color, keep=None):
    """중심에서 radius 떨어진 1px 원. keep(각도°)이 False인 칸은 비워 점선을 만든다"""
    for y in range(H):
        for x in range(W):
            dx, dy = x + 0.5 - CX, y + 0.5 - CY
            if abs(math.hypot(dx, dy) - radius) <= 0.5 and (keep is None or keep(math.degrees(math.atan2(dy, dx)) % 360)):
                px[y][x] = color


def rim(px, lit, shade):
    for x, y, ch in muzzle_cells("LR"):
        px[y][x] = lit if ch == "L" else shade


def ring_frame(f):
    px = blank()
    if f == 0:
        circle(px, 9.5, ORANGE, lambda a: a % 45 < 12)        # 여덟 점
    elif f == 1:
        circle(px, 8.5, ORANGE, lambda a: (a + 10) % 45 < 28)  # 여덟 토막
    elif f == 2:
        circle(px, 7.5, YELLOW)
    elif f == 3:
        circle(px, 6.5, YELLOW)
    elif f == 4:
        circle(px, 5.5, HOT)
    elif f == 5:
        # 테두리에 맺히는 순간의 섬광과 바깥 잔광
        circle(px, 4.9, ORANGE)
        rim(px, HOT, HOT)
    elif f == 6:
        rim(px, HOT, YELLOW)
    elif f <= 15:
        rim(px, YELLOW, ORANGE)
    elif f <= 25:
        rim(px, HOT, YELLOW)
    else:
        circle(px, 4.9, ORANGE)
        rim(px, HOT, HOT)
    return px


# ── 핵 ───────────────────────────────────────────────────────
def core_frame(f):
    px = blank()
    hole = muzzle_cells("H")

    def fill(radius, color):
        for x, y, _ in hole:
            if math.hypot(x + 0.5 - CX, y + 0.5 - CY) <= radius:
                px[y][x] = color

    if 22 <= f <= 23:
        fill(1.0, EMBER)
    elif 24 <= f <= 25:
        fill(1.6, EMBER); fill(1.0, ORANGE)
    elif 26 <= f <= 27:
        fill(9, ORANGE); fill(1.6, YELLOW)
    elif f >= 28:
        fill(9, YELLOW); fill(1.6, HOT)
    return px


# ── 연기 ─────────────────────────────────────────────────────
def pull(v):
    """빨려드는 진행: 처음엔 느리고 갈수록 빨라짐"""
    return max(0.0, min(1.0, v)) ** 2.4


def wisp_discs(u, angle, radius, swirl, size, seed):
    """진행 u(0~1)에서 가닥을 이루는 원반들 (x, y, 반지름, 종류). 종류는 2 덩이, 0 줄기"""
    r_out = radius * (1 - pull(u))                      # 바깥쪽 덩이
    r_in = max(HOLE_R, radius * (1 - pull(u * 1.5)))   # 먼저 구멍으로 달려가는 끝
    if r_out <= HOLE_R + 0.3:
        return []
    puff = size * min(1.0, 0.45 + u / 0.3) * max(0.42, min(1.0, r_out / (radius * 0.45)))
    span = r_out - r_in

    def at(r):
        a = math.radians(angle + swirl * (1 - r / radius) ** 1.3)
        return CX + math.cos(a) * r, CY + math.sin(a) * r

    # 덩이는 원반 셋을 조금씩 어긋나게 겹쳐 울퉁불퉁한 연기 모양을 만들고, 진행하면서 천천히 돌려 끓는 느낌을 줌
    x, y = at(r_out)
    turn = seed * 2.4 + u * 2.2
    discs = [(x, y, puff, 2)]
    if puff > 1.3:
        for k, (dist, scale) in enumerate(((0.75, 0.68), (0.8, 0.55))):
            a = turn + k * 2.5
            discs.append((x + math.cos(a) * puff * dist, y + math.sin(a) * puff * dist, puff * scale, 2))
    if span > 1.0:
        # 덩이에서 구멍 쪽으로 가늘어지는 줄기
        steps = max(1, int(span / 0.2))
        for i in range(steps + 1):
            s = i / steps
            discs.append(at(r_in + span * s) + (0.5 + max(0.0, puff * 0.85 - 0.5) * s ** 3, 2 if s > 0.8 else 0))
    return discs


def smoke_frame(f):
    cover = [[0] * W for _ in range(H)]   # 4×4 표본 중 덮인 수
    kind = [[0] * W for _ in range(H)]    # 가장 가까운 원반의 종류
    age_of = [[9] * W for _ in range(H)]  # 그 칸을 덮은 가닥의 나이
    for seed, (born, angle, radius, swirl, size) in enumerate(WISPS):
        age = f - born
        if age < 0 or age >= WISP_LIFE:
            continue
        discs = wisp_discs((age + 0.5) / WISP_LIFE, angle, radius, swirl, size, seed)
        if not discs:
            continue
        x0 = max(0, int(min(d[0] - d[2] for d in discs)) - 1); x1 = min(W, int(max(d[0] + d[2] for d in discs)) + 2)
        y0 = max(0, int(min(d[1] - d[2] for d in discs)) - 1); y1 = min(H, int(max(d[1] + d[2] for d in discs)) + 2)
        for y in range(y0, y1):
            for x in range(x0, x1):
                hits = 0
                for sy in range(4):
                    for sx in range(4):
                        qx, qy = x + (sx + 0.5) / 4, y + (sy + 0.5) / 4
                        if any((qx - dx) ** 2 + (qy - dy) ** 2 <= dr * dr for dx, dy, dr, _ in discs):
                            hits += 1
                if hits > cover[y][x]:
                    cover[y][x] = hits
                    inside = [d for d in discs if (x + 0.5 - d[0]) ** 2 + (y + 0.5 - d[1]) ** 2 <= (d[2] + 0.5) ** 2]
                    kind[y][x] = max(d[3] for d in inside) if inside else 0
                    age_of[y][x] = age
    on = [[cover[y][x] >= 8 for x in range(W)] for y in range(H)]

    # 홀로 떨어진 칸은 지움
    for y in range(H):
        for x in range(W):
            if on[y][x] and not any(on[j][i] for j in range(max(0, y - 1), min(H, y + 2)) for i in range(max(0, x - 1), min(W, x + 2)) if (i, j) != (x, y)):
                on[y][x] = False

    px = blank()
    for y in range(H):
        for x in range(W):
            if not on[y][x]:
                continue
            if age_of[y][x] == 0:
                px[y][x] = SMOKE_DIM
            elif kind[y][x] == 0:
                px[y][x] = SMOKE_DIM if math.hypot(x + 0.5 - CX, y + 0.5 - CY) < 4.2 else SMOKE_MID
            else:
                # 덩이는 아래·오른쪽 가장자리 한 칸을 그늘로 남김
                below = y + 1 < H and on[y + 1][x] and kind[y + 1][x] == 2
                right = x + 1 < W and on[y][x + 1] and kind[y][x + 1] == 2
                px[y][x] = SMOKE_LIGHT if below and right else SMOKE_MID
    return px


def flatten(*layers):
    px = blank()
    for layer in layers:
        for y in range(H):
            for x in range(W):
                c = layer[y][x]
                if c[3] == 255:
                    px[y][x] = c
                elif c[3]:
                    b = px[y][x]
                    if b[3] == 0:
                        px[y][x] = c
                    else:
                        a = c[3] / 255.0
                        px[y][x] = tuple(int(c[i] * a + b[i] * (1 - a) + 0.5) for i in range(3)) + (255,)
    return px


def main():
    cores = [core_frame(f) for f in range(FRAMES)]
    rings = [ring_frame(f) for f in range(FRAMES)]
    smokes = [smoke_frame(f) for f in range(FRAMES)]

    # 검수용 합본 프레임
    if len(sys.argv) > 1:
        os.makedirs(sys.argv[1], exist_ok=True)
        for f in range(FRAMES):
            write_png(os.path.join(sys.argv[1], "muzzle-charge-%02d.png" % f), flatten(cores[f], rings[f], smokes[f]))

    if ASE is None:
        raise SystemExit("Aseprite 실행 파일을 찾지 못했습니다. CHRONO_ASEPRITE 환경 변수에 경로를 지정하세요.")
    tmp = os.path.join(HERE, "_tmp-muzzle-charge")
    os.makedirs(tmp, exist_ok=True)
    for name, frames in (("core", cores), ("ring", rings), ("smoke", smokes)):
        for f in range(FRAMES):
            write_png(os.path.join(tmp, "%s-%d.png" % (name, f)), frames[f])
    out = os.path.join(tmp, "muzzle-charge.aseprite").replace("\\", "/")
    lua = '''
local dir = "%s/"
local n = %d
local spr = Sprite(%d, %d, ColorMode.RGB)
local core = spr.layers[1]
core.name = "핵"
local ring = spr:newLayer()
ring.name = "원"
local smoke = spr:newLayer()
smoke.name = "연기"
for i = 2, n do spr:newEmptyFrame() end
for i = 1, n do
  spr.frames[i].duration = %d / 1000
  spr:newCel(core, i, Image{ fromFile = dir .. "core-" .. (i - 1) .. ".png" }, Point(0, 0))
  spr:newCel(ring, i, Image{ fromFile = dir .. "ring-" .. (i - 1) .. ".png" }, Point(0, 0))
  spr:newCel(smoke, i, Image{ fromFile = dir .. "smoke-" .. (i - 1) .. ".png" }, Point(0, 0))
end
local tag = spr:newTag(1, n)
tag.name = "charge-once"
spr:saveAs("%s")
''' % (tmp.replace("\\", "/"), FRAMES, W, H, FRAME_MS, out)
    script = os.path.join(tmp, "build.lua")
    open(script, "w", encoding="utf-8").write(lua)
    subprocess.run([ASE, "-b", "-script", script], check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    shutil.copyfile(out, os.path.join(HERE, "muzzle-charge.aseprite"))
    shutil.rmtree(tmp, ignore_errors=True)
    print("muzzle-charge.aseprite: %dx%d, %d frames x %dms" % (W, H, FRAMES, FRAME_MS))


main()
