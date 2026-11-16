"""Move the user-selected R07 VFX frames37..49 to30..42, without retiming sword/body."""
from pathlib import Path
import copy, shutil

helper=Path(__file__).resolve().parent.parent/'player-combo-preview-r07/build_frames.py'
# Reuse only source loading/compositing helpers; do not execute the old export/mutation loop.
exec(compile(helper.read_text(encoding='utf-8').split('\nrecords=[]\n')[0],str(helper),'exec'))

source_folder=OUT.parent/'player-combo-preview-r07'
source_manifest=json.loads((source_folder/'manifest.json').read_text(encoding='utf-8'))
source_rows=[r for r in source_manifest['frames'] if r['attack']==3]
assert [r['frame'] for r in source_rows]==list(range(1,55))
rows=copy.deepcopy(source_rows)
fx_fields=('fx_image','fx_purple_image','fx_blue_image')

# Capture the source range before overwriting overlapping destination slots.
mapping={src['frame']-7:copy.deepcopy(src) for src in source_rows if 37<=src['frame']<=49}
assert list(mapping)==list(range(30,43))
for row in rows:
    frame=row['frame']
    if frame>=30:
        for field in fx_fields:
            row[field]=mapping[frame][field] if frame in mapping else None

protected=set(sources)
for folder in (source_folder,OUT.parent/'player-combo-preview-r09',OUT.parent/'player-combo-preview-r11'):
    protected.update(p for p in folder.rglob('*') if p.is_file())
before={p:sha(p) for p in protected}
for sub in ('native','4x'):
    (OUT/'attack-03'/sub).mkdir(parents=True,exist_ok=True)

images=[]
proof=[]
changed=[]
for row,old in zip(rows,source_rows):
    frame=row['frame']
    old_composite,old_sword,old_fx=compose(old)
    source_saved=Image.open(source_folder/old['file']).convert('RGBA')
    assert old_composite.tobytes()==source_saved.tobytes(),('R07 source mismatch',frame)
    image,sword,fx=compose(row)
    assert sword.tobytes()==old_sword.tobytes()
    assert all(row[k]==old[k] for k in old if k not in fx_fields+('fx_rgba_sha256',)),frame
    row['fx_rgba_sha256']=hashlib.sha256(fx.tobytes()).hexdigest()
    if frame in mapping:
        src=mapping[frame]
        source_fx=compose(src)[2]
        assert fx.tobytes()==source_fx.tobytes(),('VFX move',src['frame'],frame)
        assert row['fx_rgba_sha256']==src['fx_rgba_sha256']
        proof.append({'source_frame':src['frame'],'destination_frame':frame,
                      'purple_image':row['fx_purple_image'],'blue_image':row['fx_blue_image'],
                      'rgba_sha256':row['fx_rgba_sha256'],
                      'opaque_pixels':sum(a>0 for a in fx.getchannel('A').get_flattened_data())})
    elif frame>=43:
        assert not fx.getbbox(),('duplicate VFX not cleared',frame)
    else:
        assert image.tobytes()==source_saved.tobytes(),('unrelated frame',frame)
    assert all(a==b or oa[3] or na[3] for a,b,oa,na in zip(
        old_composite.get_flattened_data(),image.get_flattened_data(),old_fx.get_flattened_data(),fx.get_flattened_data()))
    if image.tobytes()==source_saved.tobytes():
        for sub in ('native','4x'):
            name=f'attack-03/{sub}/frame-{frame:02}.png'
            shutil.copyfile(source_folder/name,OUT/name)
    else:
        changed.append(frame)
        image.save(OUT/row['file'])
        image.resize((512,512),Image.Resampling.NEAREST).save(OUT/f'attack-03/4x/frame-{frame:02}.png')
    images.append(image)

assert len(proof)==13 and [p['source_frame'] for p in proof]==list(range(37,50))
assert [(r['fx_purple_image'],r['fx_blue_image']) for r in rows[29:42]]==[
    (1,None),(1,None),(1,1),(2,1),(2,2),(2,2),(2,2),(3,3),(3,3),(3,3),(4,4),(4,4),(4,4)]

# Rebuild the full sheet on the requested white background, preserving source image pixels.
sheet=Image.new('RGB',(2100,2825),'white')
draw=ImageDraw.Draw(sheet)
draw.text((20,12),'3타 — Player + 검 + 현재 검기 / 전체 54장',font=FONT,fill='black')
draw.text((20,45),'R07의37~49번째 VFX → 30~42번째 / 43번째부터 VFX 없음',font=SMALL,fill='#333333')
draw.text((20,70),'Player·검 54포즈 유지 · 첫 그림부터 왼쪽→오른쪽→다음 행 순서',font=SMALL,fill='#333333')
for i,(row,image) in enumerate(zip(rows,images)):
    x,y=(i%7)*300,105+(i//7)*340
    draw.text((x+8,y+4),f"{row['frame']}번째",font=SMALL,fill='black')
    view=image.crop(CROP).resize((288,288),Image.Resampling.NEAREST)
    display=Image.new('RGB',(288,288),'white')
    display.paste(view,(0,0),view)
    assert all(actual==(original[:3] if original[3] else (255,255,255))
               for original,actual in zip(view.get_flattened_data(),display.get_flattened_data()))
    sheet.paste(display,(x+6,y+30))
    draw.text((x+8,y+318),f"검 Z {row['angle_degrees']:.1f}°",font=SMALL,fill='#333333')
sheet.save(OUT/'attack-03-all-frames.png')

checks={'R07_all54_source_composites_match_saved_native':True,'all13_moved_vfx_RGBA_exact':True,
        'all54_player_and_sword_pixels_pose_time_sorting_preserved':True,
        'frames1_through29_unchanged':True,'frames43_through54_vfx_empty':True,
        'all_pixel_changes_inside_old_or_new_vfx':True,'white_background_and_original_display_pixels':True}
assert all(sha(p)==digest for p,digest in before.items())
checks['R07_R09_R11_and_input_files_unchanged']=True
manifest={'date':'2026-11-16 Asia/Seoul','scope':'Attack3 VFX move only; static white preview, no Unity changes',
          'source':'../player-combo-preview-r07/manifest.json','frame_numbering':'first drawing1, left-to-right then next row',
          'edit':{'source_frames':[37,49],'destination_frames':[30,42],'shift':-7,'clear_from':43},
          'changed_from_R07':changed,'checks':checks,'mapping':proof,'frames':rows,
          'sources':[{'path':p.relative_to(ROOT).as_posix(),'sha256':value} for p,value in sorted(before.items())]}
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
with (OUT/'vfx-timing.csv').open('w',newline='',encoding='utf-8-sig') as f:
    writer=csv.DictWriter(f,['frame','purple_image','blue_image'])
    writer.writeheader()
    writer.writerows({'frame':r['frame'],'purple_image':r['fx_purple_image'],'blue_image':r['fx_blue_image']} for r in rows)
print(json.dumps({'frames':54,'mapping_count':len(proof),'changed_from_R07':changed,'checks':checks},ensure_ascii=False))
