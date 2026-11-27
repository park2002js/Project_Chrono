# -*- coding: utf-8 -*-
"""경비·공용 전투 효과의 편집 원본을 Unity 프로젝트의 Assets로 내보내고 편집기 빌더가 읽을 목록을 만든다.

사용법: python export-unity.py
  1. art/archive의 부위·효과 PNG와 Aseprite, 참고 게임의 공개 효과 프레임, 음원을 project/Assets 아래의 정해진 이름으로 복사한다.
  2. 텍스처 기준점·9분할 테두리·클립 구성을 project/Assets/Editor/GuardAssetManifest.json에 기록한다.
  3. Unity에서 Project Chrono > Rebuild Guard Assets를 실행하면 임포트 설정·클립·프리팹 연결이 다시 만들어진다.
Aseprite 실행 파일은 환경 변수 CHRONO_ASEPRITE 또는 아래 기본 경로에서 찾는다.
"""
import glob, json, os, re, shutil, struct, subprocess, tempfile, zlib

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "..")).replace(os.sep, "/")
ARCH = ROOT + "/art/archive"
GUARD = ARCH + "/boss/guard"
ASSETS = ROOT + "/project/Assets"
REF = ROOT + "/docs/reference/sephiria/project/ExportedProject/Assets"
REFTEX = ROOT + "/docs/reference/sephiria/assets/Assets/Texture2D"
ASE = next((p for p in [os.environ.get("CHRONO_ASEPRITE", ""), "D:/02_Games/01_Steam/Library/steamapps/common/Aseprite/Aseprite.exe",
                        "D:/02_Games/01_Steam/SteamClient/steamapps/common/Aseprite/Aseprite.exe"] if p and os.path.exists(p)), None)
if ASE is None:
    raise SystemExit("Aseprite 실행 파일을 찾지 못했습니다. CHRONO_ASEPRITE 환경 변수에 경로를 지정하세요.")
TMP = tempfile.mkdtemp(prefix="chrono-export-")

def read_png(path):
    with open(path, 'rb') as f:
        data = f.read()
    pos = 8
    idat = b''
    plte = None; trns = None
    while pos < len(data):
        ln, typ = struct.unpack('>I4s', data[pos:pos+8])
        body = data[pos+8:pos+8+ln]
        if typ == b'IHDR':
            w, h, bd, ct, _, _, il = struct.unpack('>IIBBBBB', body)
        elif typ == b'IDAT':
            idat += body
        elif typ == b'PLTE':
            plte = body
        elif typ == b'tRNS':
            trns = body
        pos += 12 + ln
    raw = zlib.decompress(idat)
    ch = {0:1, 2:3, 3:1, 4:2, 6:4}[ct]
    bpp = ch * bd // 8 if bd >= 8 else 1
    stride = (w * ch * bd + 7) // 8
    out = []
    prev = bytearray(stride)
    p = 0
    for y in range(h):
        ft = raw[p]; p += 1
        line = bytearray(raw[p:p+stride]); p += stride
        for i in range(stride):
            a = line[i-bpp] if i >= bpp else 0
            b = prev[i]
            c = prev[i-bpp] if i >= bpp else 0
            if ft == 1: line[i] = (line[i] + a) & 255
            elif ft == 2: line[i] = (line[i] + b) & 255
            elif ft == 3: line[i] = (line[i] + (a + b) // 2) & 255
            elif ft == 4:
                pa = abs(b - c); pb = abs(a - c); pc = abs(a + b - 2*c)
                pr = a if pa <= pb and pa <= pc else (b if pb <= pc else c)
                line[i] = (line[i] + pr) & 255
        prev = line
        row = []
        for x in range(w):
            if ct == 6: row.append(tuple(line[x*4:x*4+4]))
            elif ct == 2: row.append(tuple(line[x*3:x*3+3]) + (255,))
            elif ct == 3:
                if bd == 8: idx = line[x]
                else:
                    byte = line[(x*bd)//8]; shift = 8 - bd - (x*bd) % 8
                    idx = (byte >> shift) & ((1 << bd) - 1)
                a = trns[idx] if trns and idx < len(trns) else 255
                row.append(tuple(plte[idx*3:idx*3+3]) + (a,))
            elif ct == 4: row.append((line[x*2],)*3 + (line[x*2+1],))
            elif ct == 0: row.append((line[x],)*3 + (255,))
        out.append(row)
    return w, h, out

def bbox(px, w, h):
    xs = [x for y in range(h) for x in range(w) if px[y][x][3] > 0]
    ys = [y for y in range(h) for x in range(w) if px[y][x][3] > 0]
    return (min(xs), min(ys), max(xs)+1, max(ys)+1) if xs else None


_guid_cache = {}

def ref_build_guid_map(names_prefix):
    """필요한 스프라이트 이름 접두사에 해당하는 meta만 읽어 guid→이름 표를 만든다"""
    for prefix in names_prefix:
        for meta in glob.glob(os.path.join(REF, "Sprite", prefix + "*.asset.meta")):
            g = re.search(r"guid: ([0-9a-f]+)", open(meta, encoding="utf-8").read()).group(1)
            _guid_cache[g] = os.path.basename(meta)[:-len(".asset.meta")]

def ref_timeline(asset):
    text = open(os.path.join(REF, "MonoBehaviour", asset + ".asset"), encoding="utf-8").read()
    out = []
    for m in re.finditer(r"frameIdx: (\d+)\s+sprite: \{fileID: (\d+)(?:, guid: ([0-9a-f]+))?", text):
        out.append((int(m.group(1)), _guid_cache.get(m.group(3), m.group(3))))
    other = re.findall(r"^\s+(\w+): (.+)$", text, re.M)
    return out, text

def ref_sprite_info(name):
    text = open(os.path.join(REF, "Sprite", name + ".asset"), encoding="utf-8").read()
    rect = re.search(r"m_Rect:\s+serializedVersion: 2\s+x: ([\d.\-]+)\s+y: ([\d.\-]+)\s+width: ([\d.\-]+)\s+height: ([\d.\-]+)", text)
    pivot = re.search(r"m_Pivot: \{x: ([\d.\-e]+), y: ([\d.\-e]+)\}", text)
    ppu = re.search(r"m_PixelsToUnits: ([\d.]+)", text)
    return dict(rect=[float(v) for v in rect.groups()], pivot=[float(v) for v in pivot.groups()], ppu=float(ppu.group(1)))


textures = {}  # Assets 상대 경로 → 임포트 설정
clips = {}     # 클립 에셋 경로 → 프레임·시간


def run(args):
    subprocess.run(args, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def write_png(path, w, h, px):
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        for x in range(w):
            raw.extend(px[y][x])

    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)

    data = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
    data += chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b"")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(data)


def put(src, rel, pivot=(0.5, 0.5), border=None):
    """PNG 한 장을 Assets로 복사하고 임포트 설정을 기록"""
    dst = os.path.join(ASSETS, rel)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copyfile(src, dst)
    textures[rel] = {"pivot": list(pivot), "border": border or [0, 0, 0, 0]}
    return rel


def clip(rel, frames, durations, loop=False, loop_start=0):
    clips[rel] = {"frames": frames, "durations": [round(d, 5) for d in durations], "loop": loop, "loopStart": loop_start}


def ase_durations(src):
    out = os.path.join(TMP, "meta.json")
    run([ASE, "-b", src, "--format", "json-array", "--data", out])
    d = json.load(open(out, encoding="utf-8"))
    return [f["duration"] / 1000.0 for f in d["frames"]]


def frame_files(prefix):
    files = glob.glob(os.path.join(TMP, prefix + "-*.png"))
    return sorted(files, key=lambda p: int(re.search(r"-(\d+)\.png$", p).group(1)))


def clear(prefix):
    for f in glob.glob(os.path.join(TMP, prefix + "-*.png")):
        os.remove(f)


# ── 1. 가드 부위 ──────────────────────────────────────────────
PARTS = "Art/Enemy/Guard/Parts/"
for n in ["torso", "head", "eye-left", "eye-right", "muzzle-left", "muzzle-right", "crystal"]:
    put(GUARD + "/png/parts/" + n + ".png", PARTS + n + ".png", (0.5, 0.0))
for n in ["fist-left", "fist-right"]:
    w, h, px = read_png(GUARD + "/png/parts/" + n + ".png")
    x0, y0, x1, y1 = bbox(px, w, h)
    put(GUARD + "/png/parts/" + n + ".png", PARTS + n + ".png", (((x0 + x1) / 2) / w, (h - y1) / h))
    print(n, "bbox", (x0, y0, x1, y1))
run([ASE, "-b", GUARD + "/combine fist.aseprite", "--save-as", os.path.join(TMP, "fist-combined.png")])
put(os.path.join(TMP, "fist-combined.png"), PARTS + "fist-combined.png", (0.5, 0.0))

# ── 2. 가드 전용 클립(128×51 전체 캔버스) ────────────────────
anim = json.load(open(GUARD + "/png/animations.json", encoding="utf-8"))
VFX = "Art/Enemy/Guard/Vfx/"
for name, loop in [("crystal-active", True), ("eye-laser-tell", False), ("eye-laser-fire", True), ("muzzle-left", False), ("muzzle-right", False)]:
    c = anim["clips"][name]
    frames = []
    for i in range(c["frame_count"]):
        src = "%s/png/vfx/%s/%s-%03d.png" % (GUARD, name, name, i + 1)
        frames.append(put(src, "%s%s/%s-%02d.png" % (VFX, name, name, i), (0.5, 0.0)))
    clip("%s%s/%s.asset" % (VFX, name, name), frames, [d / 1000.0 for d in c["durations_ms"]], loop)

# ── 3. 하강 스미어(왼손·오른손·양손) ─────────────────────────
smear = GUARD + "/vfx/fist-drop-smear.aseprite"
for key, layers in [("left", ["Left fist"]), ("right", ["Right fist"]), ("both", ["Left fist", "Right fist"])]:
    prefix = "smear-" + key
    clear(prefix)
    args = [ASE, "-b"]
    for l in layers:
        args += ["--layer", l]
    args += [smear, "--save-as", os.path.join(TMP, prefix + "-{frame}.png")]
    run(args)
    files = frame_files(prefix)
    w, h, px = read_png(files[0])
    x0, y0, x1, y1 = bbox(px, w, h)
    pivot = (((x0 + x1) / 2) / w, (h - y1) / h)
    name = "fist-smear-" + key
    frames = [put(f, "%s%s/%s-%02d.png" % (VFX, name, name, i), pivot) for i, f in enumerate(files)]
    clip("%s%s/%s.asset" % (VFX, name, name), frames, [0.03] * len(frames))
    print("smear", key, len(files), "bbox", (x0, y0, x1, y1), "pivot", pivot)

# ── 4. 참고 게임의 공개 효과 프레임 ──────────────────────────
ref_build_guid_map(["Askard_Phase1_Wave", "Golem_Stmap", "OinkTotemDust", "QliphothRabbit_Fly_Down", "Crash_Green_Front_FX_Small"])


def ref_pivot(sprite, w, h):
    info = ref_sprite_info(sprite)
    r, p = info["rect"], info["pivot"]
    return ((r[0] + p[0] * r[2]) / w, (r[1] + p[1] * r[3]) / h)


def resample(px, w, h, sw, sh):
    return [[px[min(h - 1, int((y + 0.5) * h / sh))][min(w - 1, int((x + 0.5) * w / sw))] for x in range(sw)] for y in range(sh)]


def ref_clip(asset, out_name, folder, tick=0.05, loop=False, loop_start_tick=None, max_steps=None):
    tl, text = ref_timeline(asset)
    # 첫 상태의 타임라인만 사용(틱 번호가 다시 줄어들면 다음 상태)
    seq = []
    for i, n in tl:
        if seq and i <= seq[-1][0]:
            break
        seq.append((i, n))
    made = {}
    frames, durations, starts = [], [], []
    if seq[0][0] > 0:
        frames.append(None); durations.append(seq[0][0] * tick); starts.append(0)
    for k, (i, n) in enumerate(seq):
        nxt = seq[k + 1][0] if k + 1 < len(seq) else i + 1
        if n is None:
            if k + 1 < len(seq):
                frames.append(None); durations.append((nxt - i) * tick); starts.append(i)
            continue
        if n not in made:
            src = REFTEX + "/" + n + ".png"
            w, h, px = read_png(src)
            made[n] = put(src, "%s%s/%s-%02d.png" % (folder, out_name, out_name, len(made)), ref_pivot(n, w, h))
        frames.append(made[n]); durations.append((nxt - i) * tick); starts.append(i)
    # 끝에 남은 빈 구간과 반복 주기에 맞지 않는 꼬리는 잘라냄
    while frames and frames[-1] is None:
        frames.pop(); durations.pop(); starts.pop()
    if max_steps:
        frames, durations, starts = frames[:max_steps], durations[:max_steps], starts[:max_steps]
    ls = 0
    if loop_start_tick is not None:
        ls = next(k for k, s in enumerate(starts) if s >= loop_start_tick)
    clip("%s%s/%s.asset" % (folder, out_name, out_name), frames, durations, loop, ls)
    print(asset, "->", out_name, len(made), "sprites", len(frames), "steps", round(sum(durations), 3), "s loopStart", ls)


ref_clip("GolemHandStampFx", "stomp-ring", VFX)
ref_clip("AskardStompReadyFx", "fist-charge-front", VFX, loop=True, loop_start_tick=6, max_steps=33)
ref_clip("AskardStompReadyBackFx", "fist-charge-back", VFX, loop=True, loop_start_tick=3)
ref_clip("AskardStompBustFx", "slam-burst", VFX)
ref_clip("AskardStompBustGroundFx", "slam-crack", VFX)
ref_clip("OinkTotemDustFx", "slam-dust", VFX)
ref_clip("BreakProtectionFx", "groggy-contact", "Art/FX/Groggy/")

# 충격파의 자리별 예고는 참고 게임의 X자 청록 균열을 둥글게 다시 그린 원본(vfx/wave-telegraph.aseprite, wave-telegraph.py로 생성)의
# 7장을 참고 클립의 순서(틱 0~12)대로 쓰되,
# 작은 균열에서 벌어진 균열까지의 7장은 0.30초에 걸쳐 보여주고 이어지는 벌어진 균열의 두 장 교대는 원래 속도(20fps)로 0.30초 유지
GROW_FRAMES, GROW_TIME, HOLD_STEPS = 7, 0.30, 6
ref_build_guid_map(["BigRoot_GreenHall"])
tl, _ = ref_timeline("RootDemonTentacleBulletGround")
clear("wave-telegraph")
run([ASE, "-b", GUARD + "/vfx/wave-telegraph.aseprite", "--save-as", os.path.join(TMP, "wave-telegraph-{frame}.png")])
wave_files = frame_files("wave-telegraph")
made, frames = {}, []
for tick, n in sorted(e for e in tl if e[1] and e[0] < GROW_FRAMES + HOLD_STEPS):
    if n not in made:
        w, h, px = read_png(REFTEX + "/" + n + ".png")
        # 참고 장이 처음 나오는 순서와 같은 번호의 다시 그린 장을 쓰고 기준점은 참고 장의 값을 유지
        made[n] = put(wave_files[len(made)], "%swave-telegraph/wave-telegraph-%02d.png" % (VFX, len(made)), ref_pivot(n, w, h))
    frames.append(made[n])
durations = [GROW_TIME / GROW_FRAMES] * GROW_FRAMES + [0.05] * (len(frames) - GROW_FRAMES)
clip(VFX + "wave-telegraph/wave-telegraph.asset", frames, durations)
print("wave-telegraph", len(made), "sprites", len(frames), "steps", round(sum(durations), 3), "s")

# 지면 타격은 검수 문서의 8장·20fps·44×36 축소를 사용
frames = []
for i in range(8):
    n = "QliphothRabbit_Fly_DownFX%02d" % i
    w, h, px = read_png(REFTEX + "/" + n + ".png")
    piv = ref_pivot(n, w, h)
    rel = "%sground-hit/ground-hit-%02d.png" % (VFX, i)
    write_png(os.path.join(ASSETS, rel), 44, 36, resample(px, w, h, 44, 36))
    textures[rel] = {"pivot": list(piv), "border": [0, 0, 0, 0]}
    frames.append(rel)
clip(VFX + "ground-hit/ground-hit.asset", frames, [0.05] * 8)
print("ground-hit pivot", piv)


# ── 5. 공용 탄환·기절·예고 ──────────────────────────────────
def core_pivot(w, h, px):
    # 흰 핵의 중심을 탄환 몸체의 중심으로 사용
    pts = [(x, y) for y in range(h) for x in range(w) if px[y][x][3] == 255 and min(px[y][x][:3]) >= 250]
    xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
    return ((min(xs) + max(xs) + 1) / 2 / w, (h - (min(ys) + max(ys) + 1) / 2) / h)


def ase_clip(src, out_name, folder, pivot_fn=None, loop=True):
    clear(out_name)
    run([ASE, "-b", src, "--save-as", os.path.join(TMP, out_name + "-{frame}.png")])
    files = frame_files(out_name)
    durs = ase_durations(src)
    frames = []
    piv = (0.5, 0.5)
    for i, f in enumerate(files):
        w, h, px = read_png(f)
        if pivot_fn and i == 0:
            piv = pivot_fn(w, h, px)
        frames.append(put(f, "%s%s-%02d.png" % (folder, out_name, i), piv))
    clip("%s%s.asset" % (folder, out_name), frames, durs, loop)
    print(out_name, len(files), "frames", durs[:3], "pivot", piv)


PROJ = "Art/Enemy/Projectiles/"
ase_clip(ARCH + "/fx/탄환/일반탄/일반탄-빨강-12px.aseprite", "normal-red", PROJ, core_pivot)
ase_clip(ARCH + "/fx/탄환/일반탄/일반탄-노랑-12px.aseprite", "normal-gold", PROJ, core_pivot)
ase_clip(ARCH + "/fx/탄환/특수탄/특수탄-빨강.aseprite", "special-red", PROJ, core_pivot)
ase_clip(ARCH + "/fx/탄환/특수탄/특수탄-노랑.aseprite", "special-gold", PROJ, core_pivot)
# 총구 준비 효과는 캔버스 한가운데가 총구 그림의 중심이며 한 번만 재생
ase_clip(GUARD + "/vfx/muzzle-charge.aseprite", "muzzle-charge", VFX + "muzzle-charge/", loop=False)
ase_clip(ARCH + "/fx/boss-groggy-contact/그로기-구슬.aseprite", "groggy-orb", "Art/FX/Groggy/")
ase_clip(ARCH + "/fx/boss-groggy-contact/기절별-수평회전.aseprite", "groggy-star", "Art/FX/Groggy/")

WARN = "Art/Enemy/Warnings/"
put(ARCH + "/fx/공격예고/사각-9슬라이스.png", WARN + "warning-rect-9slice.png", (0.5, 0.5), [1, 1, 1, 1])
put(ARCH + "/fx/공격예고/열린경로-측선.png", WARN + "warning-open-line.png", (0.5, 0.5), [0, 2, 0, 2])

# ── 6. 음원 ──────────────────────────────────────────────────
audio = {}
AUD = "Audio/Enemy/"
SFX = ARCH + "/sfx/guard"  # 경비 음원 원본 폴더
STREAMING = {"guard-bgm.ogg"}  # 길어서 메모리에 풀지 않고 흘려 재생할 음원
for src, name in [(SFX + "/레이저 차징 중.ogg", "guard-laser-charge.ogg"), (SFX + "/레이저 발사 중.ogg", "guard-laser-fire.ogg"),
                  (SFX + "/주먹 바닥에 내려찍는 소리.ogg", "guard-fist-slam.ogg"), (SFX + "/차징 주먹 바닥에 내려찍는 소리.mp3", "guard-fist-slam-charged.mp3"),
                  (SFX + "/차징하는 소리.mp3", "guard-fist-charge.mp3"),
                  (SFX + "/총구 차징하는 소리.mp3", "guard-muzzle-charge.mp3"), (SFX + "/총구에서 투사체 발사시 마다.mp3", "guard-muzzle-fire.mp3"),
                  (SFX + "/패턴 7에서 바닥에서 폭발하는 소리.mp3", "guard-wave-blast.mp3"), (SFX + "/bgm.ogg", "guard-bgm.ogg")]:
    dst = os.path.join(ASSETS, AUD + name)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copyfile(src, dst)
    audio[name] = AUD + name

manifest = {
    "ppu": 16,
    "textures": [{"path": k, "pivotX": v["pivot"][0], "pivotY": v["pivot"][1], "borderLeft": v["border"][0], "borderBottom": v["border"][1],
                  "borderRight": v["border"][2], "borderTop": v["border"][3]} for k, v in sorted(textures.items())],
    "clips": [{"path": k, "frames": [f or "" for f in v["frames"]], "durations": v["durations"], "loop": v["loop"], "loopStart": v["loopStart"]}
              for k, v in sorted(clips.items())],
    "audio": [{"name": k, "path": v, "streaming": k in STREAMING} for k, v in sorted(audio.items())],
}
out = os.path.join(ASSETS, "Editor/GuardAssetManifest.json")
json.dump(manifest, open(out, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("textures", len(textures), "clips", len(clips), "->", out)
