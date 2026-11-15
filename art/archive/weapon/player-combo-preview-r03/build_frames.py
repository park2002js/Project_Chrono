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
def reference_order(clip,time):
    values=[e['intParameter'] for e in clip['events'] if e['function']=='SetSortingOrder_MainWeapon' and e['time']<=time+1e-7]
    return values[-1]

def rows_for(attack):
    if attack < 3:
        clip = DATA['clips'][attack-1]
        for row in clip['frames60']:
            frame,time = row['frame']+1,row['clip_time_seconds']
            start = (11 if attack==1 else 7)/30
            age=time-start
            fx = math.floor((age+1e-7)*20)+1 if -1e-7<=age<.2-1e-7 else None
            end = abs(time-clip['stopTime'])<1e-7
            swing_start,swing_end = ((.3,.4) if attack==1 else (7/30,1/3))
            phase='종료 경계' if end else ('준비' if time<swing_start-1e-7 else ('휘두름' if time<=swing_end+1e-7 else '회복'))
            yield {'attack':attack,'frame':frame,'time_seconds':time,'reference_frame_zero_based':row['frame'],
                   'grip_pixels':[row['up_aim_player_relative_pivot_x']*16,row['up_aim_player_relative_pivot_y']*16],
                   'angle_degrees':row['euler_z_degrees'],'fx_image':fx,'weapon_order':reference_order(clip,time),
                   'phase':phase,'endpoint_only':end}
    else:
        # Copy the requested inclusive ranges without retiming, interpolation or a transition frame.
        first=list(rows_for(1))[:31]
        second=list(rows_for(2))[22:45]
        assert len(first)==31 and len(second)==23
        for frame,source in enumerate(first+second,1):
            fx=(frame-31)//3+1 if 31<=frame<=42 else None
            yield {**source,'attack':3,'frame':frame,'time_seconds':(frame-1)/60,
                   'source_attack':source['attack'],'source_frame':source['frame'],
                   'source_time_seconds':source['time_seconds'],'source_phase':source['phase'],
                   'fx_image':fx,'phase':f"{source['attack']}타 F{source['frame']:02}"}

def compose(row):
    sword=place(SWORD,SWORD_PIVOT,row['grip_pixels'],row['angle_degrees'])
    fx=Image.new('RGBA',SIZE)
    if row['fx_image']:
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

records=[]
all_images={}
timings=[]
for attack in range(1,4):
    rows=list(rows_for(attack))
    all_images[attack]=[]
    folder=OUT/f'attack-{attack:02}'
    for sub in ['native','4x']: (folder/sub).mkdir(parents=True,exist_ok=True)
    for row in rows:
        image,sword,fx=compose(row)
        name=f"frame-{row['frame']:02}.png"
        image.save(folder/'native'/name)
        image.resize((512,512),Image.Resampling.NEAREST).save(folder/'4x'/name)
        all_images[attack].append(image)
        records.append({**row,'file':(folder/'native'/name).relative_to(OUT).as_posix(),
                        'fx_pivot_player_relative_pixels':list(FX_OFFSET),'fx_flip_x':attack==2,
                        'sword_rgba_sha256':hashlib.sha256(sword.tobytes()).hexdigest(),
                        'fx_rgba_sha256':hashlib.sha256(fx.tobytes()).hexdigest()})
        if row['fx_image']: timings.append({'attack':attack,'frame':row['frame'],'fx_image':row['fx_image']})
    columns=7
    cell_w,cell_h=300,340
    sheet=Image.new('RGB',(columns*cell_w,105+math.ceil(len(rows)/columns)*cell_h),BG)
    draw=ImageDraw.Draw(sheet)
    draw.text((20,12),f'{attack}타 — Player + 검 + 현재 검기 / 전체 {len(rows)}장',font=FONT,fill='white')
    subtitle='기존 궤적·시간 유지 / 마지막 장은 종료 경계' if attack<3 else '54프레임 / 1타 F1–31 + 2타 F23–45 그대로 연결 / 검기 F31 동시 시작'
    draw.text((20,45),subtitle,font=SMALL,fill=MUTED)
    draw.text((20,70),'위쪽 조준 · 기존 몸체 Sprite 고정 · 프레임 번호는 F1부터 · 검기 그림은 V1~V4',font=SMALL,fill=MUTED)
    for i,(row,image) in enumerate(zip(rows,all_images[attack])):
        x,y=(i%columns)*cell_w,105+(i//columns)*cell_h
        draw.text((x+8,y+4),f"F{row['frame']:02}  {row['phase']}"+(f"  V{row['fx_image']}" if row['fx_image'] else ''),font=SMALL,fill='white')
        sheet.paste(panel(row,image),(x+6,y+30))
        draw.text((x+8,y+318),f"검 Z {row['angle_degrees']:.1f}°",font=SMALL,fill=MUTED)
    sheet.save(OUT/f'attack-{attack:02}-all-frames.png')
    # GIF is intentionally slow for inspection; images/manifest retain the authored timing.
    previews=[]
    for row,image in zip(rows,all_images[attack]):
        frame=Image.new('RGB',(480,465),BG)
        text=ImageDraw.Draw(frame)
        text.text((18,12),f"{attack}타  F{row['frame']:02}  {row['phase']}"+(f"  V{row['fx_image']}" if row['fx_image'] else ''),font=FONT,fill='white')
        text.text((18,42),'느린 프레임 검토용 · 실제 재생 속도와 별도',font=SMALL,fill=MUTED)
        frame.paste(panel(row,image,4),(48,72))
        previews.append(frame)
    previews[0].save(OUT/f'attack-{attack:02}-preview.gif',save_all=True,append_images=previews[1:],duration=80,loop=0,disposal=2,optimize=False)

# Independently compare every preserved first/second motion and sword layer with the old output.
for row in records:
    if row['attack']==3: continue
    old=next(r for r in OLD['frames'] if r['attack']==row['attack'] and r['sampling_hz']==60 and r['frame']==row['reference_frame_zero_based'])
    assert old['grip_player_relative_pixels']==row['grip_pixels']
    assert old['reference_euler_z_degrees']==row['angle_degrees']
    assert old['clip_time_seconds']==row['time_seconds']
    assert old['weapon_sorting_relative_to_body']==row['weapon_order']
    assert old['fx_frame']==(row['fx_image']-1 if row['fx_image'] else None)
third=[r for r in records if r['attack']==3]
requested=[(1,i) for i in range(1,32)]+[(2,i) for i in range(23,46)]
source_rows={(r['attack'],r['frame']):r for r in records if r['attack']<3}
assert len(third)==len(requested)==54
for actual,key in zip(third,requested):
    source=source_rows[key]
    assert (actual['source_attack'],actual['source_frame'])==key
    for field in ('grip_pixels','angle_degrees','weapon_order','sword_rgba_sha256','endpoint_only'):
        assert actual[field]==source[field],(actual['frame'],field)
    expected_fx=(actual['frame']-31)//3+1 if 31<=actual['frame']<=42 else None
    assert actual['fx_image']==expected_fx
assert third[30]['source_frame']==31 and third[31]['source_frame']==23
assert third[30]['source_attack']==1 and third[31]['source_attack']==2
assert third[-1]['source_frame']==45 and third[-1]['source_attack']==2
# Attacks 1/2 must remain byte-identical to the previous current-palette preview.
for attack in (1,2):
    for sub in ('native','4x'):
        for path in (OUT/f'attack-{attack:02}'/sub).glob('*.png'):
            previous=OUT.parent/'player-combo-preview-r01'/f'attack-{attack:02}'/sub/path.name
            assert path.read_bytes()==previous.read_bytes()
    for suffix in ('all-frames.png','preview.gif'):
        name=f'attack-{attack:02}-{suffix}'
        assert (OUT/name).read_bytes()==(OUT.parent/'player-combo-preview-r01'/name).read_bytes()
assert all(sha(p)==value for p,value in before.items())
assert [len(all_images[i]) for i in range(1,4)]==[37,45,54]
with (OUT/'vfx-timing.csv').open('w',newline='',encoding='utf-8-sig') as f:
    writer=csv.DictWriter(f,['attack','frame','fx_image']);writer.writeheader();writer.writerows(timings)
manifest={'date':'2026-11-16 Asia/Seoul','scope':'Static frame composites; game code/clips unchanged',
    'frame_counts':{'attack1':37,'attack2':45,'attack3':54},'frame_numbering':'one-based; attack1/2 final sample and spliced attack3 final sample are endpoint snapshots',
    'time_basis':'60 timeline frames per second; first/second retain measured reference clip times; GIF intentionally slow',
    'canvas':list(SIZE),'player_origin':list(ORIGIN),'pixels_per_unit':16,
    'player':'idle-left-back first20x20 cell, fixed body pose; not newly drawn body attack animation',
    'sword':'weapon_00 sheathed first7x29 cell; original pixels rotated180 for up-facing source',
    'vfx':'Current pale-purple1, pale-blue2 flipped, combined original1+2 arcs for3; 70x66 pivot35,33 at player relative2.5,24px',
    'third_splice':[{'output':[1,31],'source_attack':1,'source_frames':[1,31]},{'output':[32,54],'source_attack':2,'source_frames':[23,45]}],'intermediate_frames_added':False,'third_vfx_ranges':{'V1':[31,33],'V2':[34,36],'V3':[37,39],'V4':[40,42]},
    'third_vfx_composition':'Unmodified attack1 purple + mirrored attack2 blue at same pivot; left overlap blue-front/right purple-front; synchronized image stages and end before F43',
    'length_policy':'Exact user splice:31+23=54 samples; last is the original attack2 ending sample',
    'third_damage':'one stronger damage event remains policy; this preview does not implement damage',
    'sources':[{'path':p.relative_to(ROOT).as_posix(),'sha256':v} for p,v in before.items()],
    'checks':{'input_files_untouched':True,'first_second_all_82_motion_timing_sorting_vfx_indices_unchanged':True,
              'third_exact_user_splice_1_F1_31_plus_2_F23_45_FX_F31_42':True,'all_rendered_pixels_inside_common_review_crop':True},
    'frames':records}
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'frames':manifest['frame_counts'],'individual_pngs':len(records)*2,'contact_sheets':3,'preview_gifs':3,'checks':manifest['checks']},ensure_ascii=False))
