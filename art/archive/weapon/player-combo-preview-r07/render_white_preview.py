"""Render the user-specified R07 frame sheet on white without changing animation content."""
from pathlib import Path
import hashlib, json, math
from PIL import Image, ImageDraw, ImageFont

HERE=Path(__file__).resolve().parent
manifest_path=HERE/'manifest.json'
manifest=json.loads(manifest_path.read_text(encoding='utf-8'))
rows=[r for r in manifest['frames'] if r['attack']==3]
assert [r['frame'] for r in rows]==list(range(1,55))
sources=[HERE/'attack-03-all-frames.png',manifest_path]+[HERE/r['file'] for r in rows]
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
before={p:digest(p) for p in sources}
font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',20)
small=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',15)
sheet=Image.new('RGB',(2100,105+math.ceil(len(rows)/7)*340),'white')
draw=ImageDraw.Draw(sheet)
draw.text((20,12),'3타 — Player + 검 + 현재 검기 / 전체 54장',font=font,fill='black')
draw.text((20,45),'54프레임 / 40 첨부 교체 · 41~43 양쪽 긴 검기 · 44~46 작은 꼬리 · 47~49 마지막 잔광',font=small,fill='#333333')
draw.text((20,70),'위쪽 조준 · 기존 몸체 Sprite 고정 · 첫 그림부터 왼쪽→오른쪽→다음 행 순서',font=small,fill='#333333')
opaque_count=0
for i,row in enumerate(rows):
    x,y=(i%7)*300,105+(i//7)*340
    draw.text((x+8,y+4),f"{row['frame']}번째",font=small,fill='black')
    image=Image.open(HERE/row['file']).convert('RGBA')
    view=image.crop((16,18,112,114)).resize((288,288),Image.Resampling.NEAREST)
    assert set(view.getchannel('A').get_flattened_data())<={0,255}
    panel=Image.new('RGB',view.size,'white')
    panel.paste(view,(0,0),view)
    for source,actual in zip(view.get_flattened_data(),panel.get_flattened_data()):
        assert actual==(source[:3] if source[3] else (255,255,255))
        opaque_count+=source[3]>0
    sheet.paste(panel,(x+6,y+30))
    draw.text((x+8,y+318),f"검 Z {row['angle_degrees']:.1f}°",font=small,fill='#333333')
output=HERE/'attack-03-all-frames-white.png'
sheet.save(output)
assert all(digest(p)==old for p,old in before.items())
report={'date':'2026-11-16 Asia/Seoul','source':'attack-03-all-frames.png','output':output.name,
        'scope':'Background white and captions dark only; no motion/VFX correction or approval',
        'size':list(sheet.size),'frame_count':54,'background':'#FFFFFF','preserved_opaque_display_pixels':opaque_count,
        'checks':{'all_source_pixels_and_positions_preserved':True,'all_transparent_panel_pixels_white':True,
                  'original_dark_sheet_manifest_and_54_native_files_unchanged':True},
        'source_hashes':[{'path':p.relative_to(HERE).as_posix(),'sha256':old} for p,old in before.items()],
        'output_sha256':digest(output)}
(HERE/'white-preview-validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'output':str(output),'size':list(sheet.size),'frames':54,'checks':report['checks']},ensure_ascii=False))
