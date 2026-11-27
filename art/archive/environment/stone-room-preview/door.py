# -*- coding: utf-8 -*-
"""참고 게임 기계식 셔터 문의 골판 무늬·명암·1px 직선을 그대로 써서, 좌우로 갈라져 열리는 격벽문을 새로 구성한다.

- 바탕: docs/reference/.../Texture2D/BossGate_MoleGoat00.png (112×40)
  · 문짝은 원본 앞판의 행 구성(윗줄 반사광 → 민면 → 안쪽 테두리 → 3px 간격 세로 골 → 아랫단)을 28px 폭으로 다시 짬
  · 주황 사선과 녹색 원형 표식도 원본의 색과 그리는 방식을 그대로 사용
  · 원본의 돌기둥은 쓰지 않는다. 틀은 통로 가장자리 돌과 같은 폭(7px)의 금속 문설주와 상인방으로만 이뤄져
    통로 폭 밖으로 튀어나오지 않는다.
  · 상인방은 외곽선 사이의 밝은 다섯 줄이 방 벽의 돌 줄(위쪽 벽 띠의 윗단, 아래쪽 가장자리 돌)과 같은 행에 오도록
    7px로 잡고, 문 전체 높이는 벽 띠의 윗단부터 바닥까지인 29px로 맞춘다.
- 다른 매체의 문에서 가져온 구성: 가운데에서 좌우로 갈라지는 두 장의 격벽(SF 영화의 방폭문),
  이음매를 향해 모이는 경고 사선(게임 속 연구 시설의 보안문), 문 위 잠금 상태등(잠김 빨강·열림 초록),
  이음매에 걸쳐 둘로 갈라지는 표식(금고·격리 구역의 문)
- 실험실 색 판은 방의 벽 띠와 같은 밝기 범위(576D7D~D6DFDF)를 써서 벽과 한 재질로 보이게 한다.
- 통로 안쪽 폭 56px(경비 몸통 폭)에 맞춘 70×29 그림이며, 열릴 때 문짝이 양쪽 문설주 뒤로 들어간다.

결과(이 폴더): hidden-gate.aseprite(실험실 색, 레이어 문짝·틀·신호등, 태그 closed·opening·open·closing),
  hidden-gate-lab.png / hidden-gate-original.png (열림 9프레임 시트)
Unity(Assets/Art/Environment/StoneRoomPreview/Gate/): gate-back-00..08.png(문짝과 문턱), gate-front-closed.png·gate-front-open.png(틀과 신호등)
"""
import os, shutil, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from compose import read_png, write_png, rgb, ROOT, UNITY  # noqa: E402

TEX = os.path.join(ROOT, "docs", "reference", "sephiria", "assets", "Assets", "Texture2D")
ASE = next((p for p in [os.environ.get("CHRONO_ASEPRITE", ""), "D:/02_Games/01_Steam/Library/steamapps/common/Aseprite/Aseprite.exe",
                        "D:/02_Games/01_Steam/SteamClient/steamapps/common/Aseprite/Aseprite.exe"] if p and os.path.exists(p)), None)
GATE = os.path.join(UNITY, "Gate")
JAMB = 7                         # 문설주 폭(통로 가장자리 돌의 폭과 같음)
W, H = 56 + JAMB * 2, 29         # 70×29
DOOR_L, DOOR_R = JAMB, W - JAMB  # 통로 안쪽(문짝이 보이는 범위) 7..63
LEAF = (DOOR_R - DOOR_L) // 2    # 문짝 한 장의 폭 28
BEAM = 7                         # 상인방 높이(외곽선 2 + 벽의 돌 줄과 같은 밝은 다섯 줄)
TOP, BOTTOM = BEAM - 1, H - 4    # 문짝 윗·아랫 외곽선 행 (6, 25). 윗 외곽선은 상인방의 아랫 외곽선과 같은 행
OFFSETS = [0, 1, 3, 6, 10, 15, 20, 24, 26]   # 열릴 때 문짝이 밀려난 거리(px)
DURATIONS = [60, 50, 50, 50, 50, 50, 50, 50, 60]   # 프레임 길이(ms)

# 원본의 색 (어두운 순)
OUT = (51, 43, 62)       # 외곽선
BACK = (62, 58, 75)      # 뒤판의 깊은 골
RIB_DEEP = (73, 72, 87)  # 깊은 골
RIB = (87, 89, 103)      # 골
FACE = (111, 115, 131)   # 판의 면
HI = (134, 139, 159)     # 윗줄 반사광
HI2 = (189, 190, 207)    # 가장 밝은 반사광
ORANGE, ORANGE_D = (251, 158, 54), (213, 119, 53)
RED, RED_HI = rgb("E96870"), rgb("FFAAA0")      # 실험실 신호색: 잠김
GREEN, GREEN_HI = rgb("36B967"), rgb("98EFAD")  # 실험실 신호색: 열림
# 구조물의 회색은 방의 벽 띠가 쓰는 실험실 벽색으로, 주황 표시·녹색 표식·신호등은 그대로
LAB = {
    OUT: rgb("344958"), BACK: rgb("435868"), RIB_DEEP: rgb("576D7D"), RIB: rgb("6B8191"),
    FACE: rgb("94A8B5"), HI: rgb("BDCCD0"), HI2: rgb("D6DFDF"),
}


def blank():
    return [[(0, 0, 0, 0)] * W for _ in range(H)]


def build_leaf(ref, mirror):
    """문짝 한 장(28×20, 외곽선 포함): 원본 앞판의 행 구성을 넓힌 골판 위에 사선과 표식 절반을 얹음"""
    w, h = LEAF, BOTTOM - TOP + 1
    px = [[OUT + (255,)] * w for _ in range(h)]
    inner = w - 2
    rows = h - 2
    for r in range(rows):
        for c in range(inner):
            edge = c == 0 or c == inner - 1
            if r == 0:
                color = HI2 if c < 3 or c in (inner - 4, inner - 2, inner - 1) else HI
            elif r == 1 or r == rows - 1:
                color = FACE
            elif r == 2:
                color = FACE if edge else RIB_DEEP if c in (1, 2, inner - 3, inner - 2) else RIB
            elif r == rows - 2:
                color = FACE if edge else RIB if (c - 2) % 3 == 2 and 2 < c < inner - 3 else RIB_DEEP
            else:
                # 몸판: 3px마다 한 줄의 세로 골, 양끝에서 한 칸 안쪽은 깊은 골
                if edge:
                    color = FACE
                elif c == 1:
                    color = RIB_DEEP
                elif c == inner - 2:
                    color = RIB_DEEP if r == rows - 3 or r == 3 else RIB
                elif (c - 2) % 3 == 2:
                    color = RIB_DEEP if r == rows - 3 else RIB
                else:
                    color = FACE
            px[r + 1][c + 1] = color + (255,)

    # 이음매 쪽(왼쪽 문짝 기준 오른쪽 끝)에 세로 보강대: 밝은 면 한 줄과 깊은 골 한 줄
    for r in range(2, rows - 1):
        px[r + 1][w - 3] = HI + (255,)
        px[r + 1][w - 4] = RIB_DEEP + (255,)

    # 경고 사선: 아래쪽 5행에 이음매를 향해 올라가는 3px 폭 사선을 6px 간격으로, 아래 가장자리는 어두운 주황
    for r in range(rows - 8, rows - 3):
        for c in range(1, inner - 3):
            k = (c + r) % 6
            if k < 3:
                px[r + 1][c + 1] = (ORANGE_D if k == 0 else ORANGE) + (255,)

    # 녹색 원형 표식의 왼쪽 절반(원본 열 50..55, 행 18..27)을 이음매에 붙임
    for r in range(10):
        for c in range(6):
            px[3 + r][w - 1 - 6 + c] = ref[18 + r][50 + c]
    if mirror:
        px = [list(reversed(row)) for row in px]
        # 표식의 오른쪽 절반은 원본의 오른쪽 절반을 그대로 사용
        for r in range(10):
            for c in range(6):
                px[3 + r][1 + c] = ref[18 + r][56 + c]
    return px


def build_frame(lamp, lamp_hi):
    """틀: 양쪽 금속 문설주와 그 위를 가로지르는 상인방, 가운데 신호등"""
    px = blank()
    # 문설주: 외곽선 → 반사광 → 밝은 면 → 면 → 골 → 깊은 골 → 외곽선의 세로 띠, 8px마다 리벳
    post = [OUT, HI2, HI, FACE, RIB, RIB_DEEP, OUT]
    for y in range(BEAM - 1, H):
        for i, color in enumerate(post):
            c = BACK if i == 3 and (y - BEAM) % 8 == 4 else color
            px[y][i] = c + (255,)
            px[y][W - 1 - i] = c + (255,)
    for i in range(JAMB):
        px[H - 1][i] = OUT + (255,)
        px[H - 1][W - 1 - i] = OUT + (255,)
    # 상인방: 문설주 위까지 덮는 가로 띠. 외곽선 → 반사광 → 밝은 면 → 면 → 골 → 깊은 골 → 외곽선, 8px마다 리벳
    beam = [OUT, HI2, HI, FACE, RIB, RIB_DEEP, OUT]
    for i, color in enumerate(beam):
        for x in range(W):
            c = color
            if x in (0, W - 1):
                c = OUT
            elif i == 1 and x % 3 == 1:
                c = HI
            elif i == 3 and x % 8 == 3:
                c = BACK
            px[i][x] = c + (255,)
    # 신호등 함: 가운데 10×7, 안쪽에 6×3 등
    cx = W // 2
    for y in range(0, BEAM):
        for x in range(cx - 5, cx + 5):
            border = y in (0, BEAM - 1) or x in (cx - 5, cx + 4)
            px[y][x] = (OUT if border else RIB_DEEP) + (255,)
    for y in range(2, 5):
        for x in range(cx - 3, cx + 3):
            px[y][x] = (lamp_hi if y == 2 and x < cx else lamp) + (255,)
    return px


def build_back(left, right, offset):
    """문짝 두 장을 좌우로 offset만큼 밀고 통로 안쪽 범위로 잘라 그림. 문짝 아래에는 열려도 남는 문턱 레일 3행"""
    px = blank()
    for y in range(len(left)):
        for x in range(LEAF):
            lx = DOOR_L + x - offset
            if DOOR_L <= lx < DOOR_R:
                px[TOP + y][lx] = left[y][x]
            rx = DOOR_L + LEAF + x + offset
            if DOOR_L <= rx < DOOR_R:
                px[TOP + y][rx] = right[y][x]
    for y, color in zip(range(BOTTOM + 1, H), (RIB_DEEP, RIB, OUT)):
        for x in range(DOOR_L, DOOR_R):
            px[y][x] = (BACK if y == BOTTOM + 2 and (x - DOOR_L) % 8 == 3 else color) + (255,)
    return px


def to_lab(px):
    return [[(LAB.get(p[:3], p[:3]) + (p[3],)) if p[3] else p for p in row] for row in px]


def merge(*layers):
    out = blank()
    for layer in layers:
        for y in range(H):
            for x in range(W):
                if layer[y][x][3]:
                    out[y][x] = layer[y][x]
    return out


if __name__ == "__main__":
    rw, rh, ref = read_png(os.path.join(TEX, "BossGate_MoleGoat00.png"))
    left, right = build_leaf(ref, False), build_leaf(ref, True)
    backs = [build_back(left, right, o) for o in OFFSETS]
    fronts = {"closed": build_frame(RED, RED_HI), "open": build_frame(GREEN, GREEN_HI)}

    # 열림 시트: 닫힘(빨강) 한 장 뒤로는 초록 신호
    for variant, convert in (("original", lambda p: p), ("lab", to_lab)):
        sheet = [[(0, 0, 0, 0)] * (W * len(OFFSETS)) for _ in range(H)]
        for i, back in enumerate(backs):
            frame = merge(convert(back), convert(fronts["closed" if i == 0 else "open"]))
            for y in range(H):
                sheet[y][i * W:(i + 1) * W] = frame[y]
        write_png(os.path.join(HERE, "hidden-gate-%s.png" % variant), W * len(OFFSETS), H, sheet)

    variant = "original" if "--original" in sys.argv else "lab"
    convert = to_lab if variant == "lab" else (lambda p: p)
    os.makedirs(GATE, exist_ok=True)
    for i, back in enumerate(backs):
        write_png(os.path.join(GATE, "gate-back-%02d.png" % i), W, H, convert(back))
    for name, front in fronts.items():
        write_png(os.path.join(GATE, "gate-front-%s.png" % name), W, H, convert(front))
    print("installed to Unity:", variant, len(backs), "leaf frames")

    # Aseprite 원본(실험실 색): 레이어 문짝·틀·신호등, 태그 closed·opening·open·closing
    if ASE:
        tmp = os.path.join(HERE, "_tmp")
        os.makedirs(tmp, exist_ok=True)
        signal = (RED, RED_HI, GREEN, GREEN_HI)
        for name, front in fronts.items():
            write_png(os.path.join(tmp, "lamp-%s.png" % name), W, H, [[p if p[:3] in signal else (0, 0, 0, 0) for p in row] for row in to_lab(front)])
        write_png(os.path.join(tmp, "frame.png"), W, H, [[p if p[:3] not in signal else (0, 0, 0, 0) for p in row] for row in to_lab(fronts["closed"])])
        for i, back in enumerate(backs):
            write_png(os.path.join(tmp, "back-%d.png" % i), W, H, to_lab(back))
        out = os.path.join(tmp, "hidden-gate.aseprite").replace("\\", "/")
        lua = '''
local dir = "%s/"
local n = %d
local durations = {%s}
local spr = Sprite(%d, %d, ColorMode.RGB)
local back = spr.layers[1]
back.name = "문짝"
local frame = spr:newLayer()
frame.name = "틀"
local lamp = spr:newLayer()
lamp.name = "신호등"
for i = 2, n do spr:newEmptyFrame() end
local frameImage = Image{ fromFile = dir .. "frame.png" }
local lampClosed = Image{ fromFile = dir .. "lamp-closed.png" }
local lampOpen = Image{ fromFile = dir .. "lamp-open.png" }
for i = 1, n do
  spr.frames[i].duration = durations[i] / 1000
  spr:newCel(back, i, Image{ fromFile = dir .. "back-" .. (i - 1) .. ".png" }, Point(0, 0))
  spr:newCel(frame, i, frameImage, Point(0, 0))
  spr:newCel(lamp, i, i == 1 and lampClosed or lampOpen, Point(0, 0))
end
local closed = spr:newTag(1, 1)
closed.name = "closed"
local opening = spr:newTag(1, n)
opening.name = "opening"
local open = spr:newTag(n, n)
open.name = "open"
local closing = spr:newTag(1, n)
closing.name = "closing"
closing.aniDir = AniDir.REVERSE
spr:saveAs("%s")
''' % (tmp.replace("\\", "/"), len(OFFSETS), ",".join(str(d) for d in DURATIONS), W, H, out)
        script = os.path.join(tmp, "build.lua")
        open(script, "w", encoding="utf-8").write(lua)
        subprocess.run([ASE, "-b", "-script", script], check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        shutil.copyfile(out, os.path.join(HERE, "hidden-gate.aseprite"))
        shutil.rmtree(tmp, ignore_errors=True)
        print("aseprite saved")
    print("door %dx%d, doorway x %d..%d, leaf %d px, offsets %s, durations %s" % (W, H, DOOR_L, DOOR_R, LEAF, OFFSETS, DURATIONS))
