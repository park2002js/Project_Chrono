"""User reset: attack1 frames1..24 + attack2 frames16..45; full two-color VFX from24."""
from pathlib import Path
import copy

helper=Path(__file__).resolve().parent.parent/'player-combo-preview-r07/build_frames.py'
exec(compile(helper.read_text(encoding='utf-8').split('\nrecords=[]\n')[0],str(helper),'exec'))

original_folder=OUT.parent/'player-combo-preview-r07'
original=json.loads((original_folder/'manifest.json').read_text(encoding='utf-8'))
first=[r for r in original['frames'] if r['attack']==1 and 1<=r['frame']<=24]
second=[r for r in original['frames'] if r['attack']==2 and 16<=r['frame']<=45]
assert len(first)==24 and len(second)==30
selected=first+second
protected=set(sources)
protected.discard(WORKSPACE/'png-attack3/frames.json')
for folder in (original_folder,OUT.parent/'player-combo-preview-r12'):
    protected.update(p for p in folder.rglob('*') if p.is_file())
before={p:sha(p) for p in protected}
for sub in ('native','4x'):
    (OUT/'attack-03'/sub).mkdir(parents=True,exist_ok=True)

def stages(frame):
    if not 24<=frame<=35: return None,None
    blue=(frame-24)//3+1
    purple=2 if frame==26 else blue
    return purple,blue

rows=[]
images=[]
proof=[]
for frame,src in enumerate(selected,1):
    row=copy.deepcopy(src)
    purple,blue=stages(frame)
    row.update({'attack':3,'frame':frame,'time_seconds':(frame-1)/60,
                'source_attack':src['attack'],'source_frame':src['frame'],
                'source_time_seconds':src['time_seconds'],'source_phase':src['phase'],
                'phase':f"{src['attack']}타 {src['frame']}번째",
                'fx_image':blue,'fx_purple_image':purple,'fx_blue_image':blue,
                'file':f'attack-03/native/frame-{frame:02}.png'})
    source_composite,source_sword,_=compose(src)
    assert source_composite.tobytes()==Image.open(original_folder/src['file']).convert('RGBA').tobytes()
    image,sword,fx=compose(row)
    assert sword.tobytes()==source_sword.tobytes()
    assert hashlib.sha256(sword.tobytes()).hexdigest()==src['sword_rgba_sha256']
    assert all(row[k]==src[k] for k in ('grip_pixels','angle_degrees','weapon_order','endpoint_only'))
    row['fx_rgba_sha256']=hashlib.sha256(fx.tobytes()).hexdigest()
    row['sword_rgba_sha256']=src['sword_rgba_sha256']
    image.save(OUT/row['file'])
    image.resize((512,512),Image.Resampling.NEAREST).save(OUT/f'attack-03/4x/frame-{frame:02}.png')
    images.append(image)
    rows.append(row)

    # Verify full source shapes, not half-clipped sprites: only shared opaque pixels may occlude.
    p=FX[1][purple-1] if purple else Image.new('RGBA',(70,66))
    b=ImageOps.mirror(FX[2][blue-1]) if blue else Image.new('RGBA',(70,66))
    combined=third_effect(row)
    counts={'purple_only':0,'blue_only':0,'left_overlap_blue_front':0,'right_overlap_purple_front':0}
    for y in range(66):
        for x in range(70):
            pp,bp,result=p.getpixel((x,y)),b.getpixel((x,y)),combined.getpixel((x,y))
            assert result[3]==max(pp[3],bp[3])
            if pp[3] and not bp[3]:
                assert result==pp
                counts['purple_only']+=1
            elif bp[3] and not pp[3]:
                assert result==bp
                counts['blue_only']+=1
            elif pp[3] and bp[3]:
                assert result==(bp if x<35 else pp)
                counts['left_overlap_blue_front' if x<35 else 'right_overlap_purple_front']+=1
    proof.append({'frame':frame,'purple_image':purple,'blue_image':blue,**counts})

assert len(rows)==54
assert [(r['source_attack'],r['source_frame']) for r in rows]==[(1,n) for n in range(1,25)]+[(2,n) for n in range(16,46)]
assert stages(23)==(None,None) and stages(24)==(1,1) and stages(25)==(1,1)
assert stages(26)==(2,1) and stages(27)==(2,2) and stages(35)==(4,4) and stages(36)==(None,None)
assert proof[23]['left_overlap_blue_front'] and proof[23]['right_overlap_purple_front']
assert proof[25]['purple_only'] and proof[25]['blue_only'] and proof[25]['right_overlap_purple_front']

sheet=Image.new('RGB',(2100,2825),'white')
draw=ImageDraw.Draw(sheet)
draw.text((20,12),'3타 — 1타 1~24 + 2타 16~45 / 전체 54장',font=FONT,fill='black')
draw.text((20,45),'24번째 두 검기 시작 · 26번째 보라만 2단계 선행 · 35번째 함께 종료 / 36번째부터 없음',font=SMALL,fill='#333333')
draw.text((20,70),'겹친 부분만 왼쪽 파랑 / 오른쪽 보라가 앞 · 겹치지 않은 전체 형상 유지 · 왼→오→다음 행',font=SMALL,fill='#333333')
for i,(row,image) in enumerate(zip(rows,images)):
    x,y=(i%7)*300,105+(i//7)*340
    draw.text((x+8,y+4),f"{row['frame']}번째",font=SMALL,fill='black')
    view=image.crop(CROP).resize((288,288),Image.Resampling.NEAREST)
    panel=Image.new('RGB',(288,288),'white')
    panel.paste(view,(0,0),view)
    sheet.paste(panel,(x+6,y+30))
    draw.text((x+8,y+318),f"검 Z {row['angle_degrees']:.1f}°",font=SMALL,fill='#333333')
sheet.save(OUT/'attack-03-all-frames.png')

assert all(sha(p)==value for p,value in before.items())
checks={'54_poses_equal_attack1_1to24_plus_attack2_16to45':True,
        'body_and_sword_pixels_grip_angle_sorting_exact':True,
        'both_vfx_start24_finish35_absent36onward':True,'purple_stage2_starts26_blue_starts27':True,
        'all_nonoverlap_pixels_preserved_no_half_clipping':True,'left_overlap_blue_right_overlap_purple_front':True,
        'all_source_images_and_previous_revisions_unchanged':True}
manifest={'date':'2026-11-16 Asia/Seoul','scope':'Reset attack3 preview; no Unity code/clips changed',
          'frame_numbering':'one-based, first drawing1 then left-to-right and next row','frame_count':54,
          'motion_splice':[{'output':[1,24],'attack':1,'source_frames':[1,24]},
                           {'output':[25,54],'attack':2,'source_frames':[16,45]}],
          'vfx_schedule':{'purple':{'V1':[24,25],'V2':[26,29],'V3':[30,32],'V4':[33,35]},
                          'blue':{'V1':[24,26],'V2':[27,29],'V3':[30,32],'V4':[33,35]},'absent_before':24,'absent_from':36},
          'occlusion':'Preserve complete purple and mirrored blue originals. Only shared opaque pixels: x<35 blue on top; x>=35 purple on top.',
          'checks':checks,'overlap_proof':proof,'frames':rows,
          'sources':[{'path':p.relative_to(ROOT).as_posix(),'sha256':value} for p,value in sorted(before.items())]}
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
with (OUT/'vfx-timing.csv').open('w',newline='',encoding='utf-8-sig') as f:
    writer=csv.DictWriter(f,['frame','source_attack','source_frame','purple_image','blue_image'])
    writer.writeheader()
    writer.writerows({'frame':r['frame'],'source_attack':r['source_attack'],'source_frame':r['source_frame'],
                     'purple_image':r['fx_purple_image'],'blue_image':r['fx_blue_image']} for r in rows)

# Snapshot the already verified GIF renderer and change only its caption for this new schedule.
gif_source=OUT.parent/'player-combo-preview-r12/render_gif.py'
gif_code=gif_source.read_text(encoding='utf-8').replace('수정 VFX 30~42번째','검기 24~35번째 / 보라26 선행')
(OUT/'render_gif.py').write_text(gif_code,encoding='utf-8')
print(json.dumps({'frames':len(rows),'vfx':manifest['vfx_schedule'],'frame26_shape_proof':proof[25],'checks':checks},ensure_ascii=False))
