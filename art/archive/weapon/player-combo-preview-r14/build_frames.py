"""Change only attack3 frames23/24 to purple image1, keeping R13's other52 frames."""
from pathlib import Path
import copy, shutil

helper=Path(__file__).resolve().parent.parent/'player-combo-preview-r07/build_frames.py'
exec(compile(helper.read_text(encoding='utf-8').split('\nrecords=[]\n')[0],str(helper),'exec'))
previous=OUT.parent/'player-combo-preview-r13'
baseline=json.loads((previous/'manifest.json').read_text(encoding='utf-8'))
rows=copy.deepcopy(baseline['frames'])
protected=set(sources)-{WORKSPACE/'png-attack3/frames.json'}
protected.update(p for p in previous.rglob('*') if p.is_file())
before={p:sha(p) for p in protected}
for sub in ('native','4x'): (OUT/'attack-03'/sub).mkdir(parents=True,exist_ok=True)
images=[]
changed=[]
for row,old in zip(rows,baseline['frames']):
    frame=row['frame']
    if frame in (23,24):
        row.update({'fx_image':1,'fx_purple_image':1,'fx_blue_image':None})
    old_image,old_sword,_=compose(old)
    assert old_image.tobytes()==Image.open(previous/old['file']).convert('RGBA').tobytes()
    image,sword,fx=compose(row)
    assert sword.tobytes()==old_sword.tobytes()
    assert all(row[k]==old[k] for k in old if k not in ('fx_image','fx_purple_image','fx_blue_image','fx_rgba_sha256'))
    row['fx_rgba_sha256']=hashlib.sha256(fx.tobytes()).hexdigest()
    if frame in (23,24):
        assert fx.tobytes()==place(FX[1][0],FX_PIVOT,FX_OFFSET,0).tobytes()
        assert sum(a>0 for a in fx.getchannel('A').get_flattened_data())==1895
        assert image.tobytes()!=old_image.tobytes()
        changed.append(frame)
        image.save(OUT/row['file'])
        image.resize((512,512),Image.Resampling.NEAREST).save(OUT/f'attack-03/4x/frame-{frame:02}.png')
    else:
        assert image.tobytes()==old_image.tobytes()
        for sub in ('native','4x'):
            relative=f'attack-03/{sub}/frame-{frame:02}.png'
            shutil.copyfile(previous/relative,OUT/relative)
            assert (previous/relative).read_bytes()==(OUT/relative).read_bytes()
    images.append(image)
assert changed==[23,24]
sheet=Image.new('RGB',(2100,2825),'white')
draw=ImageDraw.Draw(sheet)
draw.text((20,12),'3타 — 1타 1~24 + 2타 16~45 / 전체 54장',font=FONT,fill='black')
draw.text((20,45),'23·24번째 보라 1단계만 · 25번째 두 검기 · 보라26에서2단계 · 35까지 잔광 / 36부터 없음',font=SMALL,fill='#333333')
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
assert all(sha(p)==v for p,v in before.items())
manifest={'date':'2026-11-16 Asia/Seoul','scope':'Only frames23/24 purple image1; static preview, no Unity changes',
          'baseline':'../player-combo-preview-r13/manifest.json','frame_count':54,
          'motion_splice':baseline['motion_splice'],
          'vfx_schedule':{'purple':{'V1':[23,25],'V2':[26,29],'V3':[30,32],'V4':[33,35]},
                          'blue':{'V1':[25,26],'V2':[27,29],'V3':[30,32],'V4':[33,35]},'absent_before':23,'absent_from':36},
          'occlusion':baseline['occlusion'],'changed_frames':[23,24],
          'checks':{'23_24_exact_complete_purple_stage1_no_blue':True,'all54_sword_poses_non_fx_fields_preserved':True,
                    'other52_native_4x_files_byte_identical':True,'frame25_onward_unchanged':True,'all_input_images_preserved':True},
          'frames':rows,'sources':[{'path':p.relative_to(ROOT).as_posix(),'sha256':v} for p,v in sorted(before.items())]}
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
with (OUT/'vfx-timing.csv').open('w',newline='',encoding='utf-8-sig') as f:
    writer=csv.DictWriter(f,['frame','purple_image','blue_image'])
    writer.writeheader()
    writer.writerows({'frame':r['frame'],'purple_image':r['fx_purple_image'],'blue_image':r['fx_blue_image']} for r in rows)
gif_code=(previous/'render_gif.py').read_text(encoding='utf-8').replace('검기 24~35번째 / 보라26 선행','23·24 보라1 단독 / 25부터 두 검기')
(OUT/'render_gif.py').write_text(gif_code,encoding='utf-8')
print(json.dumps({'frames':54,'changed':changed,'checks':manifest['checks']},ensure_ascii=False))
