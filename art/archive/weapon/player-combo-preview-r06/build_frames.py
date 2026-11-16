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
            purple=1 if frame in (37,38,39) else (4 if frame==40 else fx)
            blue=None if frame in (37,38) else (1 if frame in (39,40) else fx)
            yield {**source,'attack':3,'frame':frame,'time_seconds':(frame-1)/60,
                   'source_attack':source['attack'],'source_frame':source['frame'],
                   'source_time_seconds':source['time_seconds'],'source_phase':source['phase'],
                   'fx_image':fx,'fx_purple_image':purple,'fx_blue_image':blue,
                   'phase':f"{source['attack']}타 F{source['frame']:02}"}

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


def effect_label(row):
    if row['attack']==3:
        a,b=row['fx_purple_image'],row['fx_blue_image']
        return (f"  보라 V{a or '-'} / 하늘 V{b or '-'}" if a or b else '')
    return f"  V{row['fx_image']}" if row['fx_image'] else ''


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
        if row['fx_image']: timings.append({'attack':attack,'frame':row['frame'],'fx_image':row['fx_image'],'purple_image':row.get('fx_purple_image',row['fx_image'] if attack==1 else None),'blue_image':row.get('fx_blue_image',row['fx_image'] if attack==2 else None)})
    columns=7
    cell_w,cell_h=300,340
    sheet=Image.new('RGB',(columns*cell_w,105+math.ceil(len(rows)/columns)*cell_h),BG)
    draw=ImageDraw.Draw(sheet)
    draw.text((20,12),f'{attack}타 — Player + 검 + 현재 검기 / 전체 {len(rows)}장',font=FONT,fill='white')
    subtitle='기존 궤적·시간 유지 / 마지막 장은 종료 경계' if attack<3 else '54프레임 / F37·38 보라 큰 호만 → F39 두 큰 호 → 40번째 보라는 첨부41번째 그대로 / 41번째 이후 보존'
    draw.text((20,45),subtitle,font=SMALL,fill=MUTED)
    draw.text((20,70),'위쪽 조준 · 기존 몸체 Sprite 고정 · 프레임 번호는 F1부터 · 검기 그림은 V1~V4',font=SMALL,fill=MUTED)
    for i,(row,image) in enumerate(zip(rows,all_images[attack])):
        x,y=(i%columns)*cell_w,105+(i//columns)*cell_h
        draw.text((x+8,y+4),(f"{row['frame']}번째" if attack==3 else f"F{row['frame']:02}  {row['phase']}")+effect_label(row),font=SMALL,fill='white')
        sheet.paste(panel(row,image),(x+6,y+30))
        draw.text((x+8,y+318),f"검 Z {row['angle_degrees']:.1f}°",font=SMALL,fill=MUTED)
    sheet.save(OUT/f'attack-{attack:02}-all-frames.png')
    # GIF is intentionally slow for inspection; images/manifest retain the authored timing.
    previews=[]
    for row,image in zip(rows,all_images[attack]):
        frame=Image.new('RGB',(480,465),BG)
        text=ImageDraw.Draw(frame)
        text.text((18,12),f"{attack}타  F{row['frame']:02}  {row['phase']}"+effect_label(row),font=FONT,fill='white')
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
# Match the attached R03 baseline: change only37-40, preserve all sword poses and every frame from41.
previous_folder=OUT.parent/'player-combo-preview-r03'
previous_manifest=json.loads((previous_folder/'manifest.json').read_text(encoding='utf-8'))
previous_third=[r for r in previous_manifest['frames'] if r['attack']==3]
changed=[]
for row,old in zip(third,previous_third):
    for field in ('grip_pixels','angle_degrees','weapon_order','sword_rgba_sha256','source_attack','source_frame'):
        assert row[field]==old[field]
    new_file=OUT/row['file'];old_file=previous_folder/old['file']
    if new_file.read_bytes()!=old_file.read_bytes(): changed.append(row['frame'])
    assert row['fx_purple_image']==(1 if row['frame'] in (37,38,39) else (4 if row['frame']==40 else row['fx_image']))
    assert row['fx_blue_image']==(None if row['frame'] in (37,38) else (1 if row['frame'] in (39,40) else row['fx_image']))
assert changed==[37,38,39,40],changed
for frame in range(41,55):
    assert (OUT/f'attack-03/native/frame-{frame:02}.png').read_bytes()==(previous_folder/f'attack-03/native/frame-{frame:02}.png').read_bytes()
    assert (OUT/f'attack-03/4x/frame-{frame:02}.png').read_bytes()==(previous_folder/f'attack-03/4x/frame-{frame:02}.png').read_bytes()
assert all(sha(p)==value for p,value in before.items())
assert [len(all_images[i]) for i in range(1,4)]==[37,45,54]
with (OUT/'vfx-timing.csv').open('w',newline='',encoding='utf-8-sig') as f:
    writer=csv.DictWriter(f,['attack','frame','fx_image','purple_image','blue_image']);writer.writeheader();writer.writerows(timings)
manifest={'date':'2026-11-16 Asia/Seoul','scope':'Static frame composites; game code/clips unchanged',
    'frame_counts':{'attack1':37,'attack2':45,'attack3':54},'frame_numbering':'Attack3 PNG first drawing is1; read left-to-right then next row. Confirmed by user 2026-11-16. Final sample is endpoint snapshot',
    'time_basis':'60 timeline frames per second; first/second retain measured reference clip times; GIF intentionally slow',
    'canvas':list(SIZE),'player_origin':list(ORIGIN),'pixels_per_unit':16,
    'player':'idle-left-back first20x20 cell, fixed body pose; not newly drawn body attack animation',
    'sword':'weapon_00 sheathed first7x29 cell; original pixels rotated180 for up-facing source',
    'vfx':'Current pale-purple1, pale-blue2 flipped, combined original1+2 arcs for3; 70x66 pivot35,33 at player relative2.5,24px',
    'third_splice':[{'output':[1,31],'source_attack':1,'source_frames':[1,31]},{'output':[32,54],'source_attack':2,'source_frames':[23,45]}],'intermediate_frames_added':False,'third_vfx_ranges':{'V1':[31,33],'V2':[34,36],'V3':[37,39],'V4':[40,42]},
    'third_vfx_composition':'Complete unmodified attack1 purple + mirrored attack2 blue at same pivot; only shared pixels occluded by left blue/right purple; non-overlapping pixels preserved',
    'purple_copy_reference':{'target_ordinal':40,'source':'attached R03 PNG','source_ordinal':41,'image':4},
    'third_color_overrides':{'purple_only_frames':[37,38],'purple_image_overrides':{'37':1,'38':1,'39':1,'40':4},'blue_image_overrides':{'39':1,'40':1},'other_frames_unchanged':True},
    'third_actual_stage_fields':'fx_purple_image and fx_blue_image are authoritative; fx_image is the original base schedule',
    'length_policy':'Exact user splice:31+23=54 samples; last is the original attack2 ending sample',
    'third_damage':'one stronger damage event remains policy; this preview does not implement damage',
    'sources':[{'path':p.relative_to(ROOT).as_posix(),'sha256':v} for p,v in before.items()],
    'checks':{'input_files_untouched':True,'first_second_all_82_motion_timing_sorting_vfx_indices_unchanged':True,
              'third_exact_splice_and_only_F37_to_F40_changed':True,'all_nonoverlapping_source_pixels_retained':True,'all_rendered_pixels_inside_common_review_crop':True},
    'frames':records}
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'frames':manifest['frame_counts'],'individual_pngs':len(records)*2,'contact_sheets':3,'preview_gifs':3,'checks':manifest['checks']},ensure_ascii=False))

# The requested purple is copied from the attached sheet's literal 41st drawing.
reference41=Image.open(previous_folder/'attack-03/native/frame-41.png').convert('RGBA')
actual40=Image.open(OUT/'attack-03/native/frame-40.png').convert('RGBA')
purple_rgb=(229,210,255)
mask=lambda im: {(x,y) for y in range(im.height) for x in range(im.width) if im.getpixel((x,y))[:3]==purple_rgb and im.getpixel((x,y))[3]}
assert mask(reference41)==mask(actual40), (mask(reference41),mask(actual40))
prior=OUT.parent/'player-combo-preview-r05'
delta=[i for i in range(1,55) if (OUT/f'attack-03/native/frame-{i:02}.png').read_bytes()!=(prior/f'attack-03/native/frame-{i:02}.png').read_bytes()]
assert delta==[40],delta
manifest['checks']['purple_at40_exactly_matches_attached41']=True
manifest['checks']['only40_changed_from_r05']=True
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
# Show the exact neighboring cells and a separate original41 for direct comparison.
chosen=[37,38,39,40,41]
strip=Image.new('RGB',(400*6,480),BG)
draw=ImageDraw.Draw(strip)
for j,frame in enumerate(chosen):
    draw.text((400*j+12,15),f'{frame}번째',font=FONT,fill='white')
    im=Image.open(OUT/f'attack-03/native/frame-{frame:02}.png').convert('RGBA')
    strip.paste(panel(None,im,4),(400*j+8,60))
draw.text((2012,15),'첨부 원본의 41번째',font=FONT,fill='white')
strip.paste(panel(None,reference41,4),(2008,60))
strip.save(OUT/'attack-03-ordinal37-41.png')
print('Verified: original41 purple copied to40; only40 changed from R05')
