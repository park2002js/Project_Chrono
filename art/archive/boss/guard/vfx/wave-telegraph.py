# -*- coding: utf-8 -*-
"""경비 패턴 7의 충격파가 터질 자리에 띄우는 예고 균열을 만든다.

사용법: python wave-telegraph.py [미리보기 PNG를 둘 폴더]
결과(이 폴더): wave-telegraph.aseprite (56×38, 7장, 작은 불꽃에서 벌어진 균열까지)

참고 게임의 X자 균열 7장(BigRoot_GreenHall_Ready00~06)을 재서, 십자보다 원형에 가까운 균열로 다시 그린다.
장마다 원본에서 읽는 것: 가운데에서 바깥으로 놓인 색 띠와 그 두께, 다리 사이 오목한 곳까지의 반지름, 네 다리 끝까지의 거리.
다시 그릴 때 바꾸는 것:
  - 몸통은 오목한 곳 반지름에 장별 배율(BODY_SCALES)을 곱한 타원으로 메워 둥글게 한다(세로는 캔버스 비율만큼 눌림).
    앞의 장은 작은 불꽃처럼 다리가 두드러지고, 뒤로 갈수록 몸통이 커져 둥근 균열로 벌어진다.
  - 네 다리는 몸통 밖으로 나온 길이를 LEG_SCALE배(약 2/3 줄임)로 하고, 밑동에서 끝으로 가늘어지는 가시 모양으로 붙인다.
  - 다리 밑동에는 원본처럼 한 단계 안쪽 색의 가는 금을 넣는다.
색, 장 수, 캔버스 크기, 가운데 위치는 원본과 같다. 칸마다 여러 표본 가운데 가장 많은 색을 골라 계단을 다듬고,
몸통과 색 띠의 가장자리에서 한 칸만 튀어나온 돌기를 지운다.
Unity에서는 export-unity.py가 이 7장을 0.30초에 걸쳐 차례로 보여준 뒤 마지막 두 장을 번갈아 0.30초 유지한다.
Aseprite 실행 파일은 환경 변수 CHRONO_ASEPRITE 또는 아래 기본 경로에서 찾는다.
"""
import math, os, shutil, struct, subprocess, sys, zlib
from collections import Counter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", "..", "..", "..")).replace(os.sep, "/")
REFTEX = ROOT + "/docs/reference/sephiria/assets/Assets/Texture2D"
ASE = next((p for p in [os.environ.get("CHRONO_ASEPRITE", ""), "D:/02_Games/01_Steam/Library/steamapps/common/Aseprite/Aseprite.exe",
                        "D:/02_Games/01_Steam/SteamClient/steamapps/common/Aseprite/Aseprite.exe"] if p and os.path.exists(p)), None)

SOURCES = ["BigRoot_GreenHall_Ready%02d" % i for i in range(7)]
W, H = 56, 38
CX, CY = W // 2, H // 2     # 균열의 중심(픽셀 경계 좌표)
SQUASH = W / float(H)       # 세로 거리를 가로 기준으로 환산하는 배율
FRAME_MS = 43               # 7장을 0.30초에 보여 주는 장당 시간

BODY_SCALES = [0.55, 0.62, 0.72, 0.82, 0.9, 0.9, 0.9]  # 장마다 원본의 오목한 곳 반지름에 곱해 몸통 반지름을 정하는 배율
LEG_SCALE = 1 / 3.0         # 몸통 밖으로 나온 다리 길이의 배율(약 2/3를 줄임)
SPIKE_HALF_ANGLES = [22.0, 20.0, 18.0, 16.0, 14.5, 14.5, 14.5]  # 장마다 다리 밑동이 대각선 좌우로 차지하는 각도(°)
SPIKE_POWER = 1.25          # 1보다 크면 다리 옆선이 안으로 휘어 몸통과 부드럽게 이어짐
STREAK_LENGTH = 0.5         # 다리 길이 가운데 밑동의 금이 차지하는 비율
MIN_BODY = 2.0              # 가는 불꽃 장에서도 남길 몸통 반지름(px)
SAMPLES = 5                 # 한 칸을 가로세로 몇 표본으로 나눠 색을 고를지
CLEAR = (0, 0, 0, 0)


def read_png(path):
    data = open(path, "rb").read()
    pos, idat, plte, trns = 8, b"", None, None
    while pos < len(data):
        length, kind = struct.unpack(">I4s", data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + length]
        if kind == b"IHDR":
            w, h, bd, ct = struct.unpack(">IIBB", body[:10])
        elif kind == b"PLTE":
            plte = body
        elif kind == b"tRNS":
            trns = body
        elif kind == b"IDAT":
            idat += body
        pos += 12 + length
    raw = zlib.decompress(idat)
    channels = {6: 4, 2: 3, 3: 1, 4: 2, 0: 1}[ct]
    bpp = max(1, channels * bd // 8)
    stride = (w * channels * bd + 7) // 8
    out, prev, p = [], bytearray(stride), 0
    for _ in range(h):
        f = raw[p]; line = bytearray(raw[p + 1:p + 1 + stride]); p += 1 + stride
        for i in range(stride):
            a = line[i - bpp] if i >= bpp else 0
            b = prev[i]
            c = prev[i - bpp] if i >= bpp else 0
            if f == 1: line[i] = (line[i] + a) & 255
            elif f == 2: line[i] = (line[i] + b) & 255
            elif f == 3: line[i] = (line[i] + (a + b) // 2) & 255
            elif f == 4:
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                line[i] = (line[i] + (a if pa <= pb and pa <= pc else (b if pb <= pc else c))) & 255
        prev = line
        row = []
        for x in range(w):
            if ct == 6: row.append(tuple(line[x * 4:x * 4 + 4]))
            elif ct == 2: row.append(tuple(line[x * 3:x * 3 + 3]) + (255,))
            elif ct == 3:
                idx = line[x] if bd == 8 else (line[(x * bd) // 8] >> (8 - bd - (x * bd) % 8)) & ((1 << bd) - 1)
                row.append(tuple(plte[idx * 3:idx * 3 + 3]) + ((trns[idx] if trns and idx < len(trns) else 255),))
            elif ct == 4: row.append((line[x * 2],) * 3 + (line[x * 2 + 1],))
            else: row.append((line[x],) * 3 + (255,))
        out.append(row)
    return w, h, out


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


def measure(src):
    """원본 한 장에서 색 띠(색, 바깥 반지름)와 네 다리 끝까지의 거리를 읽음"""
    # 가운데에서 오른쪽으로 나가며 색이 바뀌는 자리를 띠의 경계로 사용
    bands = []
    x = CX
    while x < W and src[CY][x][3]:
        color = src[CY][x]
        if bands and bands[-1][0] == color:
            bands[-1][1] = x + 1 - CX
        else:
            bands.append([color, x + 1 - CX])
        x += 1

    # 사분면마다 가장 먼 칸까지의 거리(세로를 가로 기준으로 환산)를 다리 끝으로 사용
    tips = {}
    for sx in (1, -1):
        for sy in (1, -1):
            best = 0.0
            for y in range(H):
                for x in range(W):
                    dx, dy = x + 0.5 - CX, y + 0.5 - CY
                    if src[y][x][3] and dx * sx > 0 and dy * sy > 0:
                        best = max(best, math.hypot(dx, dy * SQUASH))
            tips[(sx, sy)] = best
    return bands, tips


def redraw(src, index):
    bands, tips = measure(src)
    half_angle = SPIKE_HALF_ANGLES[index]
    valley = float(bands[-1][1])
    body = max(MIN_BODY, valley * BODY_SCALES[index])
    grow = body / valley
    rings = [(color, end * grow) for color, end in bands]
    outer = rings[-1][0]
    streak = rings[-2][0] if len(rings) > 1 else None
    legs = {key: max(0.0, tip - body) * LEG_SCALE for key, tip in tips.items()}

    def color_at(u, v):
        radius = math.hypot(u, v)
        if radius <= body:
            for color, end in rings:
                if radius <= end:
                    return color
            return outer
        key = (1 if u >= 0 else -1, 1 if v >= 0 else -1)
        length = legs[key]
        if length <= 0:
            return CLEAR

        # 대각선에서 벗어난 각도가 클수록 다리가 짧아져 끝이 뾰족해짐
        off = abs(math.degrees(math.atan2(abs(v), abs(u))) - 45.0)
        reach = body + length * max(0.0, 1.0 - off / half_angle) ** SPIKE_POWER
        if radius > reach:
            return CLEAR
        across = radius * math.sin(math.radians(off))
        if streak is not None and across < 0.5 and radius < body + length * STREAK_LENGTH:
            return streak
        return outer

    out = [[CLEAR] * W for _ in range(H)]
    for y in range(H):
        for x in range(W):
            votes = Counter()
            for sy in range(SAMPLES):
                for sx in range(SAMPLES):
                    votes[color_at(x + (sx + 0.5) / SAMPLES - CX, (y + (sy + 0.5) / SAMPLES - CY) * SQUASH)] += 1
            out[y][x] = votes.most_common(1)[0][0]

    # 다리 밖의 몸통에서, 색 띠마다 가장자리로 한 칸만 튀어나온 돌기를 바깥 띠의 색으로 넘김(가장 바깥 띠는 지움)
    order = [color for color, _ in rings]
    for level in range(len(order) - 1, -1, -1):
        inside = [[out[y][x][3] != 0 and out[y][x] in order[:level + 1] for x in range(W)] for y in range(H)]
        for y in range(1, H - 1):
            for x in range(1, W - 1):
                if not inside[y][x]:
                    continue
                u, v = x + 0.5 - CX, (y + 0.5 - CY) * SQUASH
                if abs(math.degrees(math.atan2(abs(v), abs(u))) - 45.0) <= half_angle + 4.0 and math.hypot(u, v) > body - 1.0:
                    continue
                if inside[y][x - 1] + inside[y][x + 1] + inside[y - 1][x] + inside[y + 1][x] <= 1:
                    out[y][x] = order[level + 1] if level + 1 < len(order) else CLEAR

    # 홀로 남은 칸은 지움
    for y in range(H):
        for x in range(W):
            if out[y][x][3] and not any(out[j][i][3] for j in range(max(0, y - 1), min(H, y + 2)) for i in range(max(0, x - 1), min(W, x + 2)) if (i, j) != (x, y)):
                out[y][x] = CLEAR
    return out


def main():
    frames = []
    for index, name in enumerate(SOURCES):
        w, h, src = read_png(REFTEX + "/" + name + ".png")
        if (w, h) != (W, H):
            raise SystemExit("%s 크기가 %dx%d가 아닙니다." % (name, W, H))
        frames.append(redraw(src, index))

    if len(sys.argv) > 1:
        os.makedirs(sys.argv[1], exist_ok=True)
        for i, frame in enumerate(frames):
            write_png(os.path.join(sys.argv[1], "wave-telegraph-%02d.png" % i), frame)

    if ASE is None:
        raise SystemExit("Aseprite 실행 파일을 찾지 못했습니다. CHRONO_ASEPRITE 환경 변수에 경로를 지정하세요.")
    tmp = os.path.join(HERE, "_tmp-wave-telegraph")
    os.makedirs(tmp, exist_ok=True)
    for i, frame in enumerate(frames):
        write_png(os.path.join(tmp, "f-%d.png" % i), frame)
    out = os.path.join(tmp, "wave-telegraph.aseprite").replace("\\", "/")
    lua = '''
local dir = "%s/"
local n = %d
local spr = Sprite(%d, %d, ColorMode.RGB)
local layer = spr.layers[1]
layer.name = "균열"
for i = 2, n do spr:newEmptyFrame() end
for i = 1, n do
  spr.frames[i].duration = %d / 1000
  spr:newCel(layer, i, Image{ fromFile = dir .. "f-" .. (i - 1) .. ".png" }, Point(0, 0))
end
local tag = spr:newTag(1, n)
tag.name = "open-once"
spr:saveAs("%s")
''' % (tmp.replace("\\", "/"), len(frames), W, H, FRAME_MS, out)
    script = os.path.join(tmp, "build.lua")
    open(script, "w", encoding="utf-8").write(lua)
    subprocess.run([ASE, "-b", "-script", script], check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    shutil.copyfile(out, os.path.join(HERE, "wave-telegraph.aseprite"))
    shutil.rmtree(tmp, ignore_errors=True)
    print("wave-telegraph.aseprite: %dx%d, %d frames" % (W, H, len(frames)))


main()
