"""Extract original GreatSword slash evidence; no game assets are changed."""
from pathlib import Path
import hashlib
import json
import re
from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT = next(path for path in Path(__file__).resolve().parents if (path / "AGENTS.md").is_file())
ASSETS = ROOT / 'docs/reference/sephiria/assets/ExportedProject/Assets'
OUT = Path(__file__).parent
FONT = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 19)
SMALL = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 16)
rows = []
for i in range(4):
    stem = f'GreatSword0_Swing{i}'
    asset = ASSETS / 'Sprite' / f'{stem}.asset'
    texture = ASSETS / 'Texture2D' / f'{stem}.png'
    text = asset.read_text(encoding='utf-8')
    rect = re.search(r'm_Rect:\s+serializedVersion: \d+\s+x: (\S+)\s+y: (\S+)\s+width: (\S+)\s+height: (\S+)', text)
    x, y, w, h = map(float, rect.groups())
    pivot = re.search(r'm_Pivot: \{x: (\S+), y: ([^}]+)\}', text)
    px, py = map(float, pivot.groups())
    im = Image.open(texture).convert('RGBA')
    alpha = list(im.getchannel('A').get_flattened_data())
    rows.append(dict(index=i, name=stem,
        guid=re.search(r'^guid: (\w+)', asset.with_suffix('.asset.meta').read_text(), re.M).group(1),
        source=str(texture.relative_to(ROOT)).replace('\\', '/'),
        source_sha256=hashlib.sha256(texture.read_bytes()).hexdigest(),
        texture_size=list(im.size), rect_bottom_left=[x,y,w,h], pivot_normalized=[px,py],
        full_texture_pivot_bottom_left=[x+w*px,y+h*py], pixels_per_unit=16,
        alpha_nonzero_count=sum(a>0 for a in alpha), alpha_bbox_top_left=list(im.getbbox()),
        nominal_fx_seconds=[i*.05,(i+1)*.05],
        nominal_attack1_frame30_interval=[11+i*1.5,11+(i+1)*1.5],
        nominal_attack2_frame30_interval=[7+i*1.5,7+(i+1)*1.5]))

canvas = Image.new('RGB',(1280,1635),(22,27,36))
draw=ImageDraw.Draw(canvas)
draw.text((24,17),'SEPHIRIA: original GreatSword slash sprites (4 frames, 20 fps)',font=FONT,fill='white')
draw.text((24,47),'Cyan cross = spawn pivot. Red arrow = attack aim. Source pixels enlarged 4x nearest-neighbor.',font=SMALL,fill=(190,202,218))
draw.text((24,70),'The fourth frame has 5 nonzero-alpha pixels; it is a real tail frame, not an empty frame.',font=SMALL,fill=(255,196,120))
for row_idx,(flip,clockwise,label) in enumerate([
    (False,False,'Attack 1 / UP / Updown / rotation 0 deg'),
    (True,False,'Attack 2 / UP / Downup / SpriteRenderer.flipX'),
    (False,True,'Attack 1 / RIGHT / Updown / rotation -90 deg'),
    (True,True,'Attack 2 / RIGHT / Downup / flipX then rotation -90 deg')]):
    y0=110+row_idx*370
    draw.text((24,y0),label,font=FONT,fill='white')
    for i,rec in enumerate(rows):
        im=Image.open(ROOT / rec['source']).convert('RGBA')
        if flip: im=ImageOps.mirror(im)
        if clockwise: im=im.transpose(Image.Transpose.ROTATE_270)
        im=im.resize((im.width*4,im.height*4),Image.Resampling.NEAREST)
        center=(160+i*320,y0+178)
        pos=(center[0]-im.width//2,center[1]-im.height//2)
        draw.rectangle((i*320+6,y0+29,i*320+314,y0+347),fill=(42,48,60),outline=(65,75,92))
        canvas.paste(im,pos,im)
        draw.line((center[0]-7,center[1],center[0]+7,center[1]),fill=(0,240,250),width=2)
        draw.line((center[0],center[1]-7,center[0],center[1]+7),fill=(0,240,250),width=2)
        if clockwise:
            draw.line((center[0],center[1],center[0]+26,center[1]),fill=(245,90,100),width=2)
            draw.line((center[0]+26,center[1],center[0]+21,center[1]-5),fill=(245,90,100),width=2)
        else:
            draw.line((center[0],center[1],center[0],center[1]-26),fill=(245,90,100),width=2)
            draw.line((center[0],center[1]-26,center[0]-5,center[1]-21),fill=(245,90,100),width=2)
        draw.text((i*320+18,y0+325),f"FX{i}: {i*50}-{(i+1)*50} ms | alpha pixels {rec['alpha_nonzero_count']}",font=SMALL,fill='white')
draw.text((24,1610),'Timing is authored/ideal (scaled time), not an observed gameplay capture.',font=SMALL,fill=(190,202,218))
canvas.save(OUT/'vfx_contact_sheet.png')
(OUT/'vfx_metadata.json').write_text(json.dumps(rows,indent=2,ensure_ascii=False),encoding='utf-8')
print(json.dumps(rows,indent=2))
