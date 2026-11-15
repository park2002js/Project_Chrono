"""Replace only the reference sword with the current Chrono sprite in the measured poses."""
import csv
import hashlib
import json
import math
import re
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT = next(p for p in Path(__file__).resolve().parents if (p / 'AGENTS.md').is_file())
OUT = Path(__file__).resolve().parent
REFERENCE = ROOT / 'docs/archive/03_Analysis/Sephiria_GreatSword_Attacks_20261115'
ASSETS = ROOT / 'docs/reference/sephiria/assets/ExportedProject/Assets'
SWORD_PATH = ROOT / 'project/Assets/Art/Weapons/weapon.png'
NATIVE_PATH = ROOT / 'art/archive/weapon/weapon.aseprite'
META_PATH = SWORD_PATH.with_name('weapon.png.meta')
CURVES_PATH = REFERENCE / 'motion_curves_and_frames.json'
DATA = json.loads(CURVES_PATH.read_text(encoding='utf-8'))
FONT = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 19)
SMALL = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 16)
ORIGIN = (64, 88)
SIZE = (128, 128)
PPU = 16
WEAPON_PIVOT_UP = (3.5, 25.5)  # Pixel coordinates from top-left after 180-degree orientation alignment.
FX_PIVOT = (35, 33)
FX_OFFSET = (2.5, 24)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def source_record(path):
    return {'path':path.relative_to(ROOT).as_posix(), 'sha256':sha(path)}


def screen(point, scale):
    return ((ORIGIN[0]+point[0])*scale, (ORIGIN[1]-point[1])*scale)


def place(image, pivot, position, degrees, scale):
    """Inverse affine samples preserve source RGBA and rotate around the selected image pivot."""
    angle = math.radians(degrees)
    a, b = math.cos(angle)/scale, -math.sin(angle)/scale
    d, e = math.sin(angle)/scale, math.cos(angle)/scale
    px,py = screen(position, scale)
    return image.transform((SIZE[0]*scale,SIZE[1]*scale), Image.Transform.AFFINE,
        (a,b,pivot[0]-a*px-b*py,d,e,pivot[1]-d*px-e*py),Image.Resampling.NEAREST)


def fx_index(time, attack):
    age = time-(11 if attack==1 else 7)/30
    return math.floor((age+1e-7)*20) if -1e-7<=age<.2-1e-7 else None


def frame(row, attack, scale):
    idx = fx_index(row['clip_time_seconds'],attack)
    fx = Image.new('RGBA',(SIZE[0]*scale,SIZE[1]*scale))
    if idx is not None:
        sprite = FX[idx]
        if attack==2:
            sprite = ImageOps.mirror(sprite)
        fx = Image.alpha_composite(fx,place(sprite,FX_PIVOT,FX_OFFSET,0,scale))
    pivot = (row['up_aim_player_relative_pivot_x']*PPU,row['up_aim_player_relative_pivot_y']*PPU)
    sword = place(SWORD,WEAPON_PIVOT_UP,pivot,row['euler_z_degrees'],scale)
    combined = Image.alpha_composite(fx,sword)
    return combined,sword,fx


def complete_bounds(image, pivot, position, degrees):
    """Check the complete sprite rectangle before resampling, so even tiny tails cannot be clipped."""
    angle=math.radians(degrees)
    points=[]
    for x in (0,image.width):
        for y in (0,image.height):
            dx,dy=x-pivot[0],pivot[1]-y
            px=position[0]+math.cos(angle)*dx-math.sin(angle)*dy
            py=position[1]+math.sin(angle)*dx+math.cos(angle)*dy
            sx,sy=screen((px,py),1)
            assert 1<sx<SIZE[0]-1 and 1<sy<SIZE[1]-1,(sx,sy)
            points.append((sx,sy))
    return points


def contact(rows,attack,rate,path):
    columns=5
    cell_w,cell_h=384,450
    height=math.ceil(len(rows)/columns)
    sheet=Image.new('RGB',(columns*cell_w,110+height*cell_h),'#151a23')
    draw=ImageDraw.Draw(sheet)
    draw.text((18,12),f'Project Chrono sword / Sephiria attack {attack} / all {rate} Hz samples / aim UP / speed 1',font=FONT,fill='white')
    draw.text((18,40),'Same reference motion, pivot spacing and original 20 Hz VFX. Only the sword sprite is replaced; no resizing.',font=SMALL,fill='#c6d1e1')
    draw.text((18,66),'Red = player origin | Green = shoulder | Gold = sword grip | Cyan = VFX pivot | Images shown at 3x',font=SMALL,fill='#c6d1e1')
    for number,row in enumerate(rows):
        x0,y0=(number%columns)*cell_w,110+(number//columns)*cell_h
        idx=fx_index(row['clip_time_seconds'],attack)
        label='-' if idx is None else f'FX{idx}'
        draw.text((x0+12,y0+3),f"F{row['frame']:02}  {row['clip_time_seconds']:.4f}s  Z {row['euler_z_degrees']:.2f}  {label}",font=SMALL,fill='white')
        artwork=Image.new('RGBA',(384,384),'#212938')
        grid=ImageDraw.Draw(artwork)
        for coordinate in range(0,129,16):
            grid.line((coordinate*3,0,coordinate*3,383),fill='#303b4d')
            grid.line((0,coordinate*3,383,coordinate*3),fill='#303b4d')
        combined,_,_=frame(row,attack,3)
        artwork.alpha_composite(combined)
        grid=ImageDraw.Draw(artwork)
        for pos,color in [((0,0),'#ef6c73'),((2.5,8),'#6ed6bd'),(FX_OFFSET,'#7ae8ff')]:
            x,y=screen(pos,3)
            grid.line((x-4,y,x+4,y),fill=color,width=2)
            grid.line((x,y-4,x,y+4),fill=color,width=2)
        x,y=screen((row['up_aim_player_relative_pivot_x']*16,row['up_aim_player_relative_pivot_y']*16),3)
        grid.ellipse((x-2,y-2,x+2,y+2),fill='#ffcc76')
        sheet.paste(artwork.convert('RGB'),(x0,y0+28))
        draw.text((x0+12,y0+418),f"Grip ({row['up_aim_player_relative_pivot_x']*16:.2f}, {row['up_aim_player_relative_pivot_y']*16:.2f}) px",font=SMALL,fill='#ffcc76')
    sheet.save(path)


initial_hashes={p.as_posix():sha(p) for p in [SWORD_PATH,NATIVE_PATH,META_PATH,CURVES_PATH]}
meta=META_PATH.read_text(encoding='utf-8-sig')
assert re.search(r'name: weapon_00\s+rect:\s+serializedVersion: 2\s+x: 0\s+y: 0\s+width: 7\s+height: 29',meta)
assert 'pivot: {x: 0.5, y: 0.87931037}' in meta
assert 'spritePixelsToUnits: 16' in meta
sheet=Image.open(SWORD_PATH).convert('RGBA')
assert sheet.size==(21,29)
raw=sheet.crop((0,0,7,29))
assert hashlib.sha256(raw.tobytes()).hexdigest()=='e2a28f719ed566616778bc0b509700965562b0a871db5697bf3e27bb03bba1fc'
SWORD=raw.transpose(Image.Transpose.ROTATE_180)
FX=[Image.open(ASSETS/f'Texture2D/GreatSword0_Swing{i}.png').convert('RGBA') for i in range(4)]
assert all(image.size==(70,66) for image in FX)
(OUT/'source').mkdir(exist_ok=True)
raw.save(OUT/'source/our-sword-current.png')
SWORD.save(OUT/'source/our-sword-pointing-up.png')

records=[]
checks=[]
total=0
for attack,clip in enumerate(DATA['clips'],1):
    assert sha(ROOT/clip['file'])==clip['sha256']
    for rate in (30,60):
        rows=clip[f'frames{rate}']
        folder=OUT/f'attack-{attack:02}/{rate}fps'
        for sub in ('native','6x'):
            (folder/sub).mkdir(parents=True,exist_ok=True)
        for row in rows:
            pivot=(row['up_aim_player_relative_pivot_x']*16,row['up_aim_player_relative_pivot_y']*16)
            complete_bounds(SWORD,WEAPON_PIVOT_UP,pivot,row['euler_z_degrees'])
            idx=fx_index(row['clip_time_seconds'],attack)
            if idx is not None:
                complete_bounds(FX[idx],FX_PIVOT,FX_OFFSET,0)
            native,_,_=frame(row,attack,1)
            enlarged,sword,fx=frame(row,attack,6)
            filename=f"frame-{row['frame']:03}.png"
            native.save(folder/'native'/filename)
            enlarged.save(folder/'6x'/filename)
            angle=math.radians(row['euler_z_degrees'])
            records.append({'attack':attack,'sampling_hz':rate,'frame':row['frame'],
                'clip_time_seconds':row['clip_time_seconds'],'reference_local_x':row['local_x'],'reference_local_y':row['local_y'],
                'reference_euler_z_degrees':row['euler_z_degrees'],'grip_player_relative_pixels':list(pivot),
                'sword_angle_after_source_orientation_correction':row['euler_z_degrees']+180,
                'fx_frame':idx,'fx_pivot_player_relative_pixels':list(FX_OFFSET),'fx_flip_x':attack==2,
                'file_native':(folder/'native'/filename).relative_to(OUT).as_posix(),
                'file_6x':(folder/'6x'/filename).relative_to(OUT).as_posix(),
                'fx_only_rgba_sha256':hashlib.sha256(fx.tobytes()).hexdigest()})
            # Independently rebuild just the FX layer from the old figure's 3x placement convention.
            if idx is not None:
                old_fx=FX[idx] if attack==1 else ImageOps.mirror(FX[idx])
                expected=Image.new('RGBA',(768,768))
                old_fx=old_fx.resize((420,396),Image.Resampling.NEAREST)
                center=screen(FX_OFFSET,6)
                expected.alpha_composite(old_fx,(round(center[0]-210),round(center[1]-198)))
                assert fx.tobytes()==expected.tobytes(),'FX changed by sword replacement'
            total+=1
        contact(rows,attack,rate,OUT/f'attack-{attack:02}-all-{rate}fps.png')
        checks.append({'attack':attack,'sampling_hz':rate,'frame_count':len(rows),'all_bounds_safe':True})

assert total==124
assert all(sha(Path(p))==value for p,value in initial_hashes.items())
checks.extend([{'name':'source native, Unity PNG/meta and reference curves untouched','pass':True},
    {'name':'reference grip position, Z curves, 20 Hz timing and original FX pixels/pivot unchanged','pass':True},
    {'name':'all 124 poses and 248 individual PNG files written','pass':True}])
sources=[source_record(p) for p in [NATIVE_PATH,SWORD_PATH,META_PATH,CURVES_PATH]]
sources += [source_record(ASSETS/f'Texture2D/GreatSword0_Swing{i}.png') for i in range(4)]
manifest={'date':'2026-11-16 Asia/Seoul','kind':'Static frame reconstruction with only sword sprite substituted; no runtime capture',
    'weapon':'current weapon_00, sword-sheathed, crop(0,0,7,29), 16PPU, original pixels preserved',
    'orientation':'Original points down with top-left grip(3.5,3.5); rotated exactly180deg to point up with grip(3.5,25.5)',
    'reference':'Sephiria GreatSword_Attack1/2 position/euler curves, shoulder(.15625,.5), VFX pivot(.15625,1.5)',
    'canvas_native_pixels':list(SIZE),'player_origin_top_left_pixels':list(ORIGIN),'pixels_per_unity_unit':16,
    'fx':'Original4frames at nominal20Hz; attack1 F30=11, attack2 F30=7; attack2 flipX; unscaled shape and distance',
    'limits':'Aim up, attack speed1, static player-relative frame. FX is nominal timing; exact runtime update quantization not simulated. FX drawn behind sword, body omitted. Final sample is clip endpoint, not a held extra frame.',
    'sources':sources,'checks':checks,'frames':records}
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'samples':total,'individual_pngs':total*2,'contact_sheets':4,'checks':checks},ensure_ascii=False,indent=2))
