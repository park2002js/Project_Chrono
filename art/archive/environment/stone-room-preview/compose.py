# -*- coding: utf-8 -*-
"""참고 게임의 석실 전경에서 가운데를 16px 단위로 덜어내 경비 전투 구역 크기의 방 그림을 만든다.

- 원본: art/preview/boss-arena-environments/05-*/01-*.png (720×1216, 1px = 원본 1px, 16px = 1유닛)
- 벽 띠와 가장자리 돌은 16px마다 반복되므로 16의 배수만큼만 덜어내 이음매 없이 줄인다.
- 바닥 무늬는 불규칙하므로 방과 통로의 바닥은 원본 바닥의 한 구간을 통째로 가져온다.
- 위아래 통로는 경비 몸통 폭(안쪽 56px)으로 좁히고, 통로 가장자리 돌만 안쪽으로 옮긴다.
  문이 놓일 자리(통로 폭 × 문 높이)의 벽 띠 끝과 돌 끝은 문설주(7px)가 덮는다.
- 전투 공간 밖은 무늬 없이 검은색으로 채운다. 원본에 그려진 통로 문(보라색 띠)은 지운다.
- 형태·무늬·벽 구성은 그대로 두고, 색만 실험실 팔레트(art/archive/environment/laboratory/final/palette.json의
  바닥·벽 색)로 바꾼 판을 함께 만든다. 같은 원본색이라도 바닥 자리와 벽·가장자리 자리에서 다른 실험실 색을 쓴다.

결과: 이 폴더에 stone-room-original.png, stone-room-lab.png
Unity 프로젝트의 Assets/Art/Environment/StoneRoomPreview/stone-room.png에는 실험실 색 판을 넣는다.
원본색 판을 넣으려면 `python compose.py --original`로 실행한다. 문 그림은 같은 폴더의 door.py가 만든다.
좌표 기준(T): 방 바닥의 왼쪽 위 모서리가 (0, 0), 아래와 오른쪽이 +
"""
import glob, os, shutil, struct, sys, zlib

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
SRC = glob.glob(os.path.join(ROOT, "art", "preview", "boss-arena-environments", "05-*", "01-*.png"))[0]
UNITY = os.path.join(ROOT, "project", "Assets", "Art", "Environment", "StoneRoomPreview")

# 원본 석실의 바닥 범위와 줄일 양
REF_X0, REF_Y0 = 128, 432      # 바닥 왼쪽 위
REF_W, REF_H = 416, 216        # 바닥 크기
CUT_X, CUT_Y = 48, 80          # 좌우 각각 덜어낼 폭, 세로로 덜어낼 높이
FLOOR_W, FLOOR_H = REF_W - CUT_X * 2, REF_H - CUT_Y   # 320 × 136
ZONE_W, ZONE_H = 312, 130      # 전투 구역(19.5 × 8.125유닛)
# 캔버스: Player 중심 화면이 구역 끝에서 볼 수 있는 범위까지
LEFT, RIGHT, TOP, BOTTOM = -168, 488, -104, 288
DOOR_W, DOOR_H = 56, 29        # 통로 안쪽 폭(경비 몸통 폭)과 문 높이(벽 띠의 윗단부터 바닥까지)
DOOR_X0 = (FLOOR_W - DOOR_W) // 2   # 통로 바닥의 왼쪽 끝 T 열(132)
DOOR_X1 = DOOR_X0 + DOOR_W          # 통로 바닥의 오른쪽 끝 다음 열(188)
RIM = 7                        # 통로 가장자리 돌과 그 안쪽 틈의 폭
BLACK = (0, 0, 0, 255)


def read_png(path):
    data = open(path, "rb").read()
    pos, idat, plte, trns = 8, b"", None, None
    while pos < len(data):
        ln, typ = struct.unpack(">I4s", data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + ln]
        if typ == b"IHDR":
            w, h, bd, ct, _, _, _ = struct.unpack(">IIBBBBB", body)
        elif typ == b"IDAT":
            idat += body
        elif typ == b"PLTE":
            plte = body
        elif typ == b"tRNS":
            trns = body
        pos += 12 + ln
    raw = zlib.decompress(idat)
    ch = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[ct]
    bpp = max(1, ch * bd // 8)
    stride = (w * ch * bd + 7) // 8
    out, prev, p = [], bytearray(stride), 0
    for _ in range(h):
        ft = raw[p]; p += 1
        line = bytearray(raw[p:p + stride]); p += stride
        for i in range(stride):
            a = line[i - bpp] if i >= bpp else 0
            b = prev[i]
            c = prev[i - bpp] if i >= bpp else 0
            if ft == 1: line[i] = (line[i] + a) & 255
            elif ft == 2: line[i] = (line[i] + b) & 255
            elif ft == 3: line[i] = (line[i] + (a + b) // 2) & 255
            elif ft == 4:
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                line[i] = (line[i] + (a if pa <= pb and pa <= pc else (b if pb <= pc else c))) & 255
        prev = line
        row = []
        for x in range(w):
            if ct == 6: row.append(tuple(line[x * 4:x * 4 + 4]))
            elif ct == 2: row.append(tuple(line[x * 3:x * 3 + 3]) + (255,))
            elif ct == 3:
                idx = line[x]
                row.append(tuple(plte[idx * 3:idx * 3 + 3]) + ((trns[idx] if trns and idx < len(trns) else 255),))
            elif ct == 4: row.append((line[x * 2],) * 3 + (line[x * 2 + 1],))
            else: row.append((line[x],) * 3 + (255,))
        out.append(row)
    return w, h, out


def write_png(path, w, h, px):
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        for x in range(w):
            raw.extend(px[y][x])

    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)
    open(path, "wb").write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
                           + chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b""))


def rgb(code):
    return (int(code[0:2], 16), int(code[2:4], 16), int(code[4:6], 16))


if __name__ == "__main__":
    W, H, ref = read_png(SRC)
    BG = ref[2][2]

    def map_x(tx):
        """T 열 → 원본 열: 좌우 벽에서 2.5칸 안쪽의 48px 띠를 하나씩 덜어냄"""
        if tx < 40: return tx + REF_X0
        if tx < FLOOR_W - 40: return tx + REF_X0 + CUT_X
        return tx + REF_X0 + CUT_X * 2

    def map_x_wall(tx):
        """벽 띠와 아래 가장자리 돌의 열: 원본 통로 입구의 모서리 조각을 피해 같은 위상의 곧은 구간을 사용"""
        if 96 <= tx < DOOR_X0: return tx + REF_X0          # 원본 224.. 의 곧은 벽
        if DOOR_X1 <= tx < 224: return tx + REF_X0 + CUT_X * 2
        return map_x(tx)

    def map_y(ty):
        """T 행 → 원본 행: 위쪽 통로는 깨끗한 구간을 반복하고, 방은 가운데 80px을 덜어내고, 아래 통로는 곧은 구간을 반복"""
        if ty < -46: return 320 + ((ty + 112) % 32)          # 위 통로의 소품 없는 32행을 위로 반복
        if -18 <= ty < 0:                                     # 원본에 그려진 위쪽 문 자리는 바로 위의 빈 바닥으로 대체
            m = ty % 16
            return 400 + m if m < 3 else 384 + m
        if ty < 48: return ty + REF_Y0
        if 148 <= ty < 170: return ty + REF_Y0 + CUT_Y + 32   # 원본에 그려진 아래 문 자리를 같은 위상의 빈 통로로 대체
        if ty < 208: return ty + REF_Y0 + CUT_Y
        return 688 + ((ty - 208) % 32)                        # 아래 통로의 곧은 32행을 아래로 반복

    def wall_y(ty):
        """방의 벽 띠·가장자리 돌이 쓰는 행: 문 자리 치환 없이 방의 세로 대응만 적용"""
        return ty + REF_Y0 if ty < 48 else ty + REF_Y0 + CUT_Y

    def source(tx, ty):
        in_room_rows = 0 <= ty < FLOOR_H
        in_door_cols = DOOR_X0 <= tx < DOOR_X1
        # 방 안쪽 바닥과 통로 바닥은 가운데 구간의 한 덩어리를 그대로 사용해 무늬 이음매를 없앰
        if in_room_rows and 2 <= tx < FLOOR_W - 2 and ty < FLOOR_H - 2:
            return tx + REF_X0 + CUT_X, ty + REF_Y0
        if in_door_cols and not in_room_rows:
            return tx + REF_X0 + CUT_X, map_y(ty)
        # 위 통로의 가장자리 돌은 문 윗단(T -29)에 닿을 때까지 내려 문과의 사이에 빈 줄이 남지 않게 함
        if -32 <= ty < -DOOR_H:
            if DOOR_X0 - RIM <= tx < DOOR_X0:
                return tx - (DOOR_X0 - RIM) + 281, map_y(ty)
            if DOOR_X1 <= tx < DOOR_X1 + RIM:
                return tx - DOOR_X1 + 384, map_y(ty)
        # 방의 벽 띠, 좌우 가장자리 돌, 아래 가장자리 돌
        if -32 <= ty < FLOOR_H + 8 and -8 <= tx < FLOOR_W + 8:
            return map_x_wall(tx), wall_y(ty)
        # 통로의 가장자리 돌: 원본 통로의 왼쪽 7열(281..287)과 오른쪽 7열(384..390)을 좁힌 통로 옆에 붙임
        if DOOR_X0 - RIM <= tx < DOOR_X0:
            return tx - (DOOR_X0 - RIM) + 281, map_y(ty)
        if DOOR_X1 <= tx < DOOR_X1 + RIM:
            return tx - DOOR_X1 + 384, map_y(ty)
        return None

    cw, ch = RIGHT - LEFT, BOTTOM - TOP
    room = [[BG] * cw for _ in range(ch)]
    for ty in range(TOP, BOTTOM):
        for tx in range(LEFT, RIGHT):
            s = source(tx, ty)
            if s and 0 <= s[0] < W and 0 <= s[1] < H:
                room[ty - TOP][tx - LEFT] = ref[s[1]][s[0]]

    def floor_mask(src):
        """걸을 수 있는 바닥 자리: 방 한가운데에서 바깥색 틈을 넘지 않고 이어지는 픽셀 (벽과 가장자리 돌은 틈으로 분리됨)"""
        mask = [[False] * cw for _ in range(ch)]
        sx, sy = FLOOR_W // 2 - LEFT, FLOOR_H // 2 - TOP
        stack = [(sx, sy)]
        mask[sy][sx] = True
        while stack:
            x, y = stack.pop()
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < cw and 0 <= ny < ch and not mask[ny][nx] and src[ny][nx] != BG:
                    mask[ny][nx] = True
                    stack.append((nx, ny))
        return mask

    floor = floor_mask(room)

    # 문 자리에서는 벽 띠와 통로 돌이 바닥과 맞닿으므로, 방 바닥과 통로 바닥의 열 밖으로 번진 곳은 다시 벽으로 분류
    for y in range(ch):
        for x in range(cw):
            tx, ty = x + LEFT, y + TOP
            if floor[y][x] and not (DOOR_X0 <= tx < DOOR_X1) and not (0 <= ty < FLOOR_H and 0 <= tx < FLOOR_W):
                floor[y][x] = False

    # ── 실험실 팔레트 ─────────────────────────────────────────
    # 바닥 자리: 무늬 선 → 기본 → 밝은 얼룩 → 가장 밝은 점 순서를 실험실 바닥색의 같은 순서로
    FLOOR_MAP = {
        (46, 57, 72): rgb("8A877B"), (49, 65, 78): rgb("989589"), (60, 80, 97): rgb("A7A498"),
        (70, 94, 115): rgb("B0ADA1"), (70, 94, 114): rgb("B0ADA1"), (79, 107, 131): rgb("B4B1A5"),
        (38, 41, 59): rgb("8E8B7F"), (35, 36, 55): rgb("8A877B"), (33, 32, 52): rgb("8A877B"),
    }
    # 벽·가장자리 돌: 어두운 면 → 밝은 돌 윗면 순서를 실험실 벽색의 같은 순서로
    WALL_MAP = {
        (46, 57, 72): rgb("576D7D"), (49, 65, 78): rgb("6B8191"), (60, 80, 97): rgb("94A8B5"),
        (70, 94, 115): rgb("BDCCD0"), (70, 94, 114): rgb("BDCCD0"), (79, 107, 131): rgb("D6DFDF"),
        (38, 41, 59): rgb("344958"), (35, 36, 55): rgb("283B4B"), (33, 32, 52): rgb("1C2C39"),
    }

    def paint(src, lab):
        out, missing = [], {}
        for y in range(ch):
            row = []
            for x in range(cw):
                p = src[y][x]
                if p == BG:
                    # 바닥에 둘러싸인 한 점짜리 바깥색은 바닥의 무늬 선 색으로, 나머지는 검은색으로
                    enclosed = 0 < x < cw - 1 and 0 < y < ch - 1 and floor[y][x - 1] and floor[y][x + 1] and floor[y - 1][x] and floor[y + 1][x]
                    if enclosed:
                        row.append((FLOOR_MAP[(49, 65, 78)] if lab else (49, 65, 78)) + (255,))
                    else:
                        row.append(BLACK)
                    continue
                if not lab:
                    row.append(p)
                    continue
                table = FLOOR_MAP if floor[y][x] else WALL_MAP
                if p[:3] not in table:
                    missing[p[:3]] = missing.get(p[:3], 0) + 1
                row.append(table.get(p[:3], p[:3]) + (255,))
            out.append(row)
        if missing:
            print("room colors without a role entry:", missing)
        return out

    os.makedirs(UNITY, exist_ok=True)
    write_png(os.path.join(HERE, "stone-room-original.png"), cw, ch, paint(room, False))
    write_png(os.path.join(HERE, "stone-room-lab.png"), cw, ch, paint(room, True))
    variant = "original" if "--original" in sys.argv else "lab"
    shutil.copyfile(os.path.join(HERE, "stone-room-%s.png" % variant), os.path.join(UNITY, "stone-room.png"))
    print("installed to Unity:", variant)
    print("canvas %dx%d px, zone top-center at pixel (%d, %d) from top-left" % (cw, ch, FLOOR_W // 2 - LEFT, -TOP))
    print("room pivot (normalized): x %.6f y %.6f" % ((FLOOR_W / 2.0 - LEFT) / cw, 1 - (-TOP) / float(ch)))
    print("zone inside art: left %dpx, bottom slack %dpx" % ((FLOOR_W - ZONE_W) // 2, FLOOR_H - ZONE_H))
    print("doorway T x %d..%d (%.3f units wide); exit door base at T y 0, entry door base at T y %d" % (DOOR_X0, DOOR_X1, DOOR_W / 16.0, FLOOR_H + DOOR_H))
