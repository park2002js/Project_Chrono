"""Composite existing sprites; preserve attacks 1/2 and splice attack1 F1-31 and attack2 F23-45 into attack3."""
from pathlib import Path
import csv, hashlib, json, math
from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT = next(p for p in Path(__file__).resolve().parents if (p / 'AGENTS.md').is_file())
OUT = Path(__file__).resolve().parent
REFERENCE = ROOT / 'docs/archive/03_Analysis/Sephiria_GreatSword_Attacks_20261115/motion_curves_and_frames.json'
PREVIOUS = ROOT / 'art/archive/weapon/sephiria-motion-our-player-r02/manifest.json'
WORKSPACE = ROOT / 'art/archive/weapon/greatsword-vfx-edit'
SWORD_PATH = ROOT / 'project/Assets/Art/Weapons/weapon.png'
PLAYER_PATH = ROOT / 'art/archive/player/final/unity/idle-left-back.png'
DATA = json.loads(REFERENCE.read_text(encoding='utf-8'))
OLD = json.loads(PREVIOUS.read_text(encoding='utf-8'))
SIZE, ORIGIN = (128,128), (64,88)
PLAYER_PIVOT, SWORD_PIVOT = (10,20), (3.5,25.5)
FX_PIVOT, FX_OFFSET = (35,33), (2.5,24)
CROP = (16,18,112,114)
FONT = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',20)
SMALL = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',15)
BG, MUTED = '#171e2a', '#c6d3e6'
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()

sources = [REFERENCE,PREVIOUS,SWORD_PATH,SWORD_PATH.with_suffix('.png.meta'),PLAYER_PATH,
           ROOT/'art/archive/weapon/weapon.aseprite',ROOT/'art/archive/player/final/aseprite/idle-left-back.aseprite']
for attack in range(1,4):
    folder = ['png','png-attack2','png-attack3'][attack-1]
    sources.extend(WORKSPACE/folder/f'slash-{i:02}.png' for i in range(1,5))
    sources.append(WORKSPACE/folder/'frames.json')
sources.extend(ROOT/clip['file'] for clip in DATA['clips'])
before = {p:sha(p) for p in sources}
for record in OLD['sources']:
    if record['path'] in [p.relative_to(ROOT).as_posix() for p in sources]:
        assert sha(ROOT/record['path']) == record['sha256'], record['path']
PLAYER = Image.open(PLAYER_PATH).convert('RGBA').crop((0,0,20,20))
SWORD = Image.open(SWORD_PATH).convert('RGBA').crop((0,0,7,29)).transpose(Image.Transpose.ROTATE_180)
FX = {a:[Image.open(WORKSPACE/['png','png-attack2','png-attack3'][a-1]/f'slash-{i:02}.png').convert('RGBA') for i in range(1,5)] for a in range(1,4)}

def place(image,pivot,position,angle):
    radians = math.radians(angle)
    a,b,d,e = math.cos(radians),-math.sin(radians),math.sin(radians),math.cos(radians)
    px,py = ORIGIN[0]+position[0],ORIGIN[1]-position[1]
    return image.transform(SIZE,Image.Transform.AFFINE,
        (a,b,pivot[0]-a*px-b*py,d,e,pivot[1]-d*px-e*py),Image.Resampling.NEAREST)

BODY = place(PLAYER,PLAYER_PIVOT,(0,0),0)
def third_effect(row):
    # Keep both complete originals; only pixels shared by both can occlude each other.
    purple=(FX[1][row['fx_purple_image']-1] if row['fx_purple_image'] else Image.new('RGBA',(70,66)))
    blue=(ImageOps.mirror(FX[2][row['fx_blue_image']-1]) if row['fx_blue_image'] else Image.new('RGBA',(70,66)))
    result=Image.alpha_composite(purple,blue)
    for y in range(66):
        for x in range(70):
            a,b=purple.getpixel((x,y)),blue.getpixel((x,y))
            if a[3] and b[3] and x>=35: result.putpixel((x,y),a)
            value=result.getpixel((x,y))
            if a[3] and not b[3]: assert value==a
            if b[3] and not a[3]: assert value==b
            if a[3] and b[3]: assert value==(b if x<35 else a)
            assert value[3]==max(a[3],b[3])
    return result


def compose(row):
    sword=place(SWORD,SWORD_PIVOT,row['grip_pixels'],row['angle_degrees'])
    fx=Image.new('RGBA',SIZE)
    if row['attack']==3:
        fx=place(third_effect(row),FX_PIVOT,FX_OFFSET,0)
    elif row['fx_image']:
        sprite=FX[row['attack']][row['fx_image']-1]
        if row['attack']==2: sprite=ImageOps.mirror(sprite)
        fx=place(sprite,FX_PIVOT,FX_OFFSET,0)
    layers=[sword,BODY,fx] if row['weapon_order']<0 else [BODY,fx,sword]
    composite=Image.new('RGBA',SIZE)
    for layer in layers: composite=Image.alpha_composite(composite,layer)
    bbox=composite.getbbox()
    assert bbox[0]>=CROP[0] and bbox[1]>=CROP[1] and bbox[2]<=CROP[2] and bbox[3]<=CROP[3],bbox
    return composite,sword,fx

def panel(row,image,scale=3):
    result=Image.new('RGB',(96*scale,96*scale),BG)
    view=image.crop(CROP).resize(result.size,Image.Resampling.NEAREST)
    result.paste(view,(0,0),view)
    return result


import copy, shutil
previous=OUT.parent/'player-combo-preview-r07'
baseline=json.loads((previous/'manifest.json').read_text(encoding='utf-8'))
rows=copy.deepcopy([r for r in baseline['frames'] if r['attack']==3])
old44=rows[43]
for field in ('fx_image','fx_purple_image','fx_blue_image'):
    rows[37][field]=old44[field]
    rows[43][field]=None
for sub in ('native','4x'): (OUT/'attack-03'/sub).mkdir(parents=True,exist_ok=True)
images=[]
for row in rows:
    frame=row['frame']
    if frame in (38,44):
        rendered,sword,fx=compose(row)
        row['sword_rgba_sha256']=hashlib.sha256(sword.tobytes()).hexdigest()
        row['fx_rgba_sha256']=hashlib.sha256(fx.tobytes()).hexdigest()
        rendered.save(OUT/row['file'])
        rendered.resize((512,512),Image.Resampling.NEAREST).save(OUT/f'attack-03/4x/frame-{frame:02}.png')
    else:
        for sub in ('native','4x'):
            relative=f'attack-03/{sub}/frame-{frame:02}.png'
            shutil.copyfile(previous/relative,OUT/relative)
    images.append(Image.open(OUT/row['file']).convert('RGBA'))
oldrows=[r for r in baseline['frames'] if r['attack']==3]
assert rows[37]['fx_rgba_sha256']==oldrows[43]['fx_rgba_sha256']
assert rows[43]['fx_rgba_sha256']==hashlib.sha256(Image.new('RGBA',SIZE).tobytes()).hexdigest()
for row,oldrow in zip(rows,oldrows):
    for field in ('grip_pixels','angle_degrees','weapon_order','sword_rgba_sha256','source_attack','source_frame'):
        assert row[field]==oldrow[field]
for sub in ('native','4x'):
    changed=[n for n in range(1,55) if (OUT/f'attack-03/{sub}/frame-{n:02}.png').read_bytes()!=(previous/f'attack-03/{sub}/frame-{n:02}.png').read_bytes()]
    assert changed==[38,44],changed
sheet=Image.new('RGB',(2100,2825),BG)
draw=ImageDraw.Draw(sheet)
draw.text((20,12),'3타 — 전체 54장',font=FONT,fill='white')
draw.text((20,45),'38번째에 기존44번째 VFX 이동 · 44번째 VFX 제거',font=SMALL,fill=MUTED)
draw.text((20,70),'첫 그림부터 왼쪽→오른쪽→다음 행 순서',font=SMALL,fill=MUTED)
for i,(row,rendered) in enumerate(zip(rows,images)):
    x,y=(i%7)*300,105+(i//7)*340
    draw.text((x+8,y+4),f"{row['frame']}번째",font=FONT,fill='white')
    sheet.paste(panel(row,rendered),(x+6,y+30))
sheet.save(OUT/'attack-03-all-frames.png')
manifest={'date':'2026-11-16 Asia/Seoul','scope':'Attack3 frame sheet only; no runtime change','baseline':'../player-combo-preview-r07/manifest.json','frame_numbering':'First drawing1; left-to-right then next row','edit':{'move_vfx_from':44,'move_vfx_to':38,'clear_vfx_at':44},'checks':{'only38_and44_changed':True,'frame38_fx_matches_previous44':True,'frame44_fx_empty':True,'other52_images_and54_sword_poses_preserved':True},'frames':rows}
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
with (OUT/'vfx-timing.csv').open('w',newline='',encoding='utf-8-sig') as f:
    writer=csv.DictWriter(f,['frame','purple_image','blue_image']);writer.writeheader();writer.writerows({'frame':r['frame'],'purple_image':r['fx_purple_image'],'blue_image':r['fx_blue_image']} for r in rows)
assert all(sha(p)==value for p,value in before.items())
print('54 frames saved; only38 and44 changed; movedFX and emptyFX verified')
