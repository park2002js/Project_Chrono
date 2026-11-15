"""Create diagnostic figures from extracted reference curves; no Unity assets are edited."""
import csv
import json
import math
import sys
from pathlib import Path

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from PIL import Image, ImageDraw, ImageFont, ImageOps
sys.dont_write_bytecode = True
from motion_extract import evaluate

ROOT = next(p for p in Path(__file__).resolve().parents if (p / 'AGENTS.md').is_file())
OUT = Path(__file__).resolve().parent
ASSETS = ROOT / 'docs/reference/sephiria/assets/ExportedProject/Assets'
DATA = json.loads((OUT / 'motion_curves_and_frames.json').read_text(encoding='utf-8'))
FONT = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 16)
SMALL = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 14)
WEAPON = Image.open(ASSETS / 'Texture2D/GreatSword_Tier1.png').convert('RGBA')
assert WEAPON.size == (11, 28)
assert WEAPON.getchannel('A').getbbox() == (1, 1, 10, 27)
assert [x for x in range(11) if WEAPON.getpixel((x, 1))[3]] == [5]


def fx_index(time, attack):
    age = time - (11 if attack == 1 else 7) / 30
    return math.floor((age + 1e-7) * 20) if -1e-7 <= age < .2 - 1e-7 else None


def screen(x, y):
    return 128 + x * 48, 214 - y * 48


def panel(row, attack):
    im = Image.new('RGBA', (256, 320), (27, 33, 44, 255))
    draw = ImageDraw.Draw(im)
    for x in range(-2, 3):
        sx, _ = screen(x, 0)
        draw.line((sx, 35, sx, 280), fill=(46, 54, 68), width=1)
    for y in range(0, 5):
        _, sy = screen(0, y)
        draw.line((10, sy, 246, sy), fill=(46, 54, 68), width=1)
    idx = fx_index(row['clip_time_seconds'], attack)
    if idx is not None:
        fx = Image.open(ASSETS / f'Texture2D/GreatSword0_Swing{idx}.png').convert('RGBA')
        if attack == 2:
            fx = ImageOps.mirror(fx)
        fx = fx.resize((210, 198), Image.Resampling.NEAREST)
        fxp = screen(.15625, 1.5)
        im.alpha_composite(fx, (round(fxp[0] - 105), round(fxp[1] - 99)))
        draw = ImageDraw.Draw(im)
        draw.line((fxp[0]-4, fxp[1], fxp[0]+4, fxp[1]), fill='#7ae8ff', width=1)
        draw.line((fxp[0], fxp[1]-4, fxp[0], fxp[1]+4), fill='#7ae8ff', width=1)
    px, py = screen(row['up_aim_player_relative_pivot_x'], row['up_aim_player_relative_pivot_y'])
    phi = math.radians(row['euler_z_degrees'])
    a, b, d, e = math.cos(phi)/3, -math.sin(phi)/3, math.sin(phi)/3, math.cos(phi)/3
    transformed = WEAPON.transform(im.size, Image.Transform.AFFINE,
        (a, b, 5.5-a*px-b*py, d, e, 22-d*px-e*py), Image.Resampling.NEAREST)
    im.alpha_composite(transformed)
    draw = ImageDraw.Draw(im)
    draw.ellipse((124, 210, 132, 218), fill='#ef6c73')
    sx, sy = screen(.15625, .5)
    draw.rectangle((sx-2, sy-2, sx+2, sy+2), fill='#6ed6bd')
    draw.ellipse((px-2, py-2, px+2, py+2), fill='#ffcc76')
    fxtext = '-' if idx is None else f'FX{idx}'
    draw.text((9, 8), f"F{row['frame']:02}  {row['clip_time_seconds']:.3f}s  {fxtext}", font=FONT, fill='white')
    draw.text((9, 27), f"Z {row['euler_z_degrees']:.2f} deg", font=SMALL, fill='#ffcc76')
    draw.text((9, 296), f"pivot px ({row['up_aim_player_relative_pivot_x']*16:.2f}, {row['up_aim_player_relative_pivot_y']*16:.2f})", font=SMALL, fill='#c4cedd')
    return im


for attack, clip in enumerate(DATA['clips'], 1):
    rows = clip['frames30']
    columns, height = 5, math.ceil(len(rows)/5)
    sheet = Image.new('RGB', (columns*256, 90+height*320), '#151a23')
    draw = ImageDraw.Draw(sheet)
    draw.text((14, 10), f'Attack {attack}: 30 Hz authoring samples / aim UP / speed 1 / nominal 20 Hz FX', font=FONT, fill='white')
    draw.text((14, 34), 'Red: player origin. Green: shoulder. Gold: weapon pivot. Cyan: FX pivot. Grid = 16 source pixels.', font=SMALL, fill='#c4cedd')
    draw.text((14, 56), 'Static reconstruction; FX drawn behind sword for inspection. Endpoint is a boundary sample, not a held frame.', font=SMALL, fill='#c4cedd')
    for n, row in enumerate(rows):
        sheet.paste(panel(row, attack).convert('RGB'), ((n%5)*256, 90+(n//5)*320))
    sheet.save(OUT / f'attack{attack}_frames.png')

plt.rcParams.update({'font.size': 10})
fig, axes = plt.subplots(2, 2, figsize=(13, 11))
fig.subplots_adjust(left=.08, right=.97, bottom=.07, top=.88, hspace=.32, wspace=.25)
for col, clip in enumerate(DATA['clips']):
    attack = col+1
    rows = []
    for frame in range(round(clip['stopTime']*600)+1):
        t = frame/600
        x = .15625 + evaluate(clip['position']['keys'], 'x', t)
        y = .5 + evaluate(clip['position']['keys'], 'y', t)
        phi = evaluate(clip['euler']['keys'], 'z', t)
        angle = math.radians(phi)
        rows.append({'clip_time_seconds':t, 'euler_z_degrees':phi,
            'up_aim_player_relative_pivot_x':x, 'up_aim_player_relative_pivot_y':y,
            'up_aim_player_relative_opaque_tip_center_x':x-1.28125*math.sin(angle),
            'up_aim_player_relative_opaque_tip_center_y':y+1.28125*math.cos(angle)})
    time = [r['clip_time_seconds'] for r in rows]
    axes[0,col].plot([r['up_aim_player_relative_opaque_tip_center_x']*16 for r in rows],
        [r['up_aim_player_relative_opaque_tip_center_y']*16 for r in rows], color='#d28321', label='opaque blade-tip pixel center')
    axes[0,col].plot([r['up_aim_player_relative_pivot_x']*16 for r in rows],
        [r['up_aim_player_relative_pivot_y']*16 for r in rows], color='#237baf', label='weapon pivot')
    axes[0,col].scatter([0], [0], marker='+', s=130, color='#be394b', label='player origin')
    axes[0,col].scatter([2.5], [8], marker='+', s=130, color='#328b61', label='shoulder')
    keys = [0,6,9,10,11,12,18] if attack == 1 else [0,7,8,9,10,22]
    for key in keys:
        row = clip['frames30'][key]
        x,y = row['up_aim_player_relative_opaque_tip_center_x']*16, row['up_aim_player_relative_opaque_tip_center_y']*16
        axes[0,col].scatter([x],[y], s=18, color='#d28321')
        axes[0,col].annotate(f'F{key}', (x,y), xytext=(4,4), textcoords='offset points', fontsize=9)
    axes[0,col].set(title=f'Attack {attack}: player-relative paths (aim up)', xlabel='source pixels right', ylabel='source pixels up', aspect='equal')
    axes[0,col].grid(alpha=.25)
    axes[0,col].margins(.13)
    axes[1,col].plot(time, [r['euler_z_degrees'] for r in rows], color='#237baf')
    event = (11 if attack==1 else 7)/30
    colors = ['#a7d9eb','#78b8d6','#4b8aaf','#24577c']
    for i in range(4):
        axes[1,col].axvspan(event+i*.05,event+(i+1)*.05, color=colors[i], alpha=.6)
        axes[1,col].text(event+(i+.5)*.05, 194, f'FX{i}', ha='center', fontsize=8)
    axes[1,col].axvline(event, color='#be394b', linestyle='--', label='FX creation event')
    axes[1,col].set(title=f'Attack {attack}: rotation and nominal FX intervals', xlabel='clip seconds (speed 1)', ylabel='unwrapped local Z degrees', ylim=(-190,215))
    axes[1,col].grid(alpha=.25)
fig.suptitle('Sephiria greatsword 1 / 2: static curve reconstruction, not runtime capture', fontsize=15, y=.98)
fig.legend(*axes[0,0].get_legend_handles_labels(), loc='upper center', bbox_to_anchor=(.5,.95), ncol=4, frameon=False, fontsize=9)
fig.savefig(OUT / 'motion_and_timing.png', dpi=160)
plt.close(fig)

tables = []
for attack, clip in enumerate(DATA['clips'],1):
    tables.extend([f'### {attack}타 전체 30fps 표', '', '| F | 시간(s) | 피벗 X(px) | 피벗 Y(px) | 로컬 Z(°) | 직전 대비(°) | 최초 대비(°) | 검기 | 키 |', '| ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |'])
    for r in clip['frames30']:
        idx = fx_index(r['clip_time_seconds'], attack)
        values = [r['frame'], f"{r['clip_time_seconds']:.4f}", f"{r['up_aim_player_relative_pivot_x']*16:.3f}", f"{r['up_aim_player_relative_pivot_y']*16:.3f}", f"{r['euler_z_degrees']:.3f}", f"{r['angle_from_previous_degrees']:.3f}", f"{r['angle_from_first_degrees']:.3f}", '-' if idx is None else f'FX{idx}', 'K' if r['is_authored_position_key'] else '보간']
        tables.append('| '+' | '.join(map(str,values))+' |')
    tables.append('')
(OUT / 'frame_tables.md').write_text('\n'.join(tables),encoding='utf-8')
print(json.dumps({'panels': [len(c['frames30']) for c in DATA['clips']], 'tip_pixel_verified': True, 'figures': ['attack1_frames.png','attack2_frames.png','motion_and_timing.png']}, indent=2))
