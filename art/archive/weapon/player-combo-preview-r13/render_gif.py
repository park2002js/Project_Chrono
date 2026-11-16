"""Make a labeled slow GIF from the revised saved transparent frames."""
from pathlib import Path
import hashlib, json
from PIL import Image, ImageDraw, ImageFont

HERE=Path(__file__).resolve().parent
manifest_path=HERE/'manifest.json'
manifest=json.loads(manifest_path.read_text(encoding='utf-8'))
sources=[manifest_path,HERE/'attack-03-all-frames.png']+[HERE/r['file'] for r in manifest['frames']]
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
before={p:digest(p) for p in sources}
font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',20)
small=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',15)
frames=[]
for row in manifest['frames']:
    image=Image.open(HERE/row['file']).convert('RGBA').crop((16,18,112,114))
    view=image.resize((576,576),Image.Resampling.NEAREST)
    frame=Image.new('RGB',(624,672),'white')
    draw=ImageDraw.Draw(frame)
    draw.fontmode='1'
    draw.text((18,10),f"3타 — {row['frame']}번째 / 54",font=font,fill='black')
    draw.text((18,40),'검기 24~35번째 / 보라26 선행 · 느린 순서 검토용',font=small,fill='#333333')
    frame.paste(view,(24,72),view)
    assert len(frame.getcolors(maxcolors=256) or [])>0, 'GIF palette would change sprite pixels'
    frames.append(frame)
output=HERE/'attack-03-preview.gif'
frames[0].save(output,save_all=True,append_images=frames[1:],duration=80,loop=0,disposal=2,optimize=False)
gif=Image.open(output)
assert gif.n_frames==54
for index,expected in enumerate(frames):
    gif.seek(index)
    assert gif.info['duration']==80
    assert gif.convert('RGB').tobytes()==expected.tobytes(),index+1
assert all(digest(p)==value for p,value in before.items())
proof={'date':'2026-11-16 Asia/Seoul','output':output.name,'frames':54,'duration_per_frame_ms':80,
       'scope':'Slow inspection only, not Unity timing','white_background':True,
       'all54_decoded_RGB_frames_exact':True,'source_png_and_manifest_unchanged':True,
       'output_sha256':digest(output)}
(HERE/'gif-validation.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(proof,ensure_ascii=False))
