"""Copy actual source VFX pixels44..end to36..end-8, preserving all body/sword pixels."""
from pathlib import Path
import copy
import shutil

helper = Path(__file__).resolve().parent.parent / 'player-combo-preview-r08/build_frames.py'
exec(compile(helper.read_text(encoding='utf-8').split('\nimport copy, shutil\n')[0], str(helper), 'exec'))

source_folder = OUT.parent / 'player-combo-preview-r07'
previous_folder = OUT.parent / 'player-combo-preview-r09'
source_rows = [r for r in json.loads((source_folder / 'manifest.json').read_text(encoding='utf-8'))['frames'] if r['attack'] == 3]
rows = copy.deepcopy(source_rows)
protected_files = [p for folder in (source_folder, previous_folder) for p in folder.rglob('*') if p.is_file()]
protected = {p: sha(p) for p in protected_files + sources}

# Separate the actual saved artwork into the unmodified body/sword and VFX pixels.
bases, original_fx = {}, {}
colors = {(229, 210, 255), (195, 234, 255)}
for row in source_rows:
    frame = row['frame']
    without_fx = dict(row, fx_image=None, fx_purple_image=None, fx_blue_image=None)
    base, sword, _ = compose(without_fx)
    assert hashlib.sha256(sword.tobytes()).hexdigest() == row['sword_rgba_sha256']
    actual = Image.open(source_folder / row['file']).convert('RGBA')
    effect = Image.new('RGBA', SIZE)
    for y in range(SIZE[1]):
        for x in range(SIZE[0]):
            pixel = actual.getpixel((x, y))
            if pixel[3] and pixel[:3] in colors:
                effect.putpixel((x, y), pixel)
    assert Image.alpha_composite(base, effect).tobytes() == actual.tobytes(), ('direct extraction', frame)
    bases[frame], original_fx[frame] = base, effect

for sub in ('native', '4x', 'vfx-only', 'body-sword-only'):
    (OUT / 'attack-03' / sub).mkdir(parents=True, exist_ok=True)

images, mapping = [], []
last_source = max(r['frame'] for r in source_rows)
for row in rows:
    frame = row['frame']
    src = frame if frame < 36 else frame + 8
    fx = original_fx[src].copy() if src <= last_source else Image.new('RGBA', SIZE)
    result = Image.alpha_composite(bases[frame], fx)
    bases[frame].save(OUT / f'attack-03/body-sword-only/frame-{frame:02}.png')
    fx.save(OUT / f'attack-03/vfx-only/frame-{frame:02}.png')
    result.save(OUT / row['file'])
    result.resize((512, 512), Image.Resampling.NEAREST).save(OUT / f'attack-03/4x/frame-{frame:02}.png')
    if 36 <= frame <= last_source - 8:
        assert fx.tobytes() == original_fx[frame + 8].tobytes()
        mapping.append({'source_frame': frame + 8, 'destination_frame': frame,
                        'rgba_sha256': hashlib.sha256(fx.tobytes()).hexdigest(),
                        'visible_pixels': sum(a > 0 for a in fx.getchannel('A').getdata())})
    reference = source_rows[src - 1] if src <= last_source else {}
    for field in ('fx_image', 'fx_purple_image', 'fx_blue_image'):
        row[field] = reference.get(field)
    row['fx_rgba_sha256'] = hashlib.sha256(fx.tobytes()).hexdigest()
    row['vfx_source_frame'] = src if src <= last_source else None
    assert all(row[field] == source_rows[frame - 1][field] for field in
               ('grip_pixels', 'angle_degrees', 'weapon_order', 'sword_rgba_sha256', 'time_seconds', 'source_attack', 'source_frame'))
    # The previous output is also audited honestly: direct extraction must agree.
    assert result.tobytes() == Image.open(previous_folder / row['file']).convert('RGBA').tobytes(), ('previous output comparison', frame)
    images.append(result)

assert [r['source_frame'] for r in mapping] == list(range(44, last_source + 1))
assert [r['destination_frame'] for r in mapping] == list(range(36, last_source - 7))
assert all(sha(p) == digest for p, digest in protected.items())

# Only the review background and labels change; transparent asset pixels stay exact.
BG = '#505968'
sheet = Image.new('RGB', (2100, 2825), BG)
draw = ImageDraw.Draw(sheet)
draw.text((20, 12), '3타 — VFX 원본 44번부터 끝까지 → 36번부터 / Player·검 유지', font=FONT, fill='white')
draw.text((20, 45), '각 칸 아래 VFX 원본 번호 표시 · 꼬리·잔광·빈 노출 칸도 순서와 간격 그대로 이동', font=SMALL, fill='white')
draw.text((20, 70), '밝은 배경은 검토표 전용 · 실제 PNG는 기존 투명 배경 유지', font=SMALL, fill='white')
for i, (row, rendered) in enumerate(zip(rows, images)):
    x, y = (i % 7) * 300, 105 + (i // 7) * 340
    draw.text((x + 8, y + 4), f"{row['frame']}번째", font=FONT, fill='white')
    sheet.paste(panel(row, rendered), (x + 6, y + 30))
    label = f"VFX 원본 {row['vfx_source_frame']} → 현재 {row['frame']}" if 36 <= row['frame'] <= last_source - 8 else ('기존 상태 유지' if row['frame'] < 36 else '이동 후 빈 VFX')
    draw.text((x + 8, y + 319), label, font=SMALL, fill='white')
sheet.save(OUT / 'attack-03-all-frames.png')

# Show the first source/destination pair at large size without moving the body/sword.
compare = Image.new('RGB', (1200, 650), BG)
draw = ImageDraw.Draw(compare)
draw.text((24, 16), '원본 VFX를 직접 분리·복사 — 같은 픽셀, 같은 위치', font=FONT, fill='white')
saved_destination36 = Image.open(OUT / 'attack-03/vfx-only/frame-36.png').convert('RGBA')
for x, title, im in ((24, '원본 44번의 VFX만', original_fx[44]), (624, '새 36번의 VFX만', saved_destination36)):
    draw.text((x, 62), title, font=FONT, fill='white')
    scaled = im.resize((512, 512), Image.Resampling.NEAREST)
    compare.paste(scaled, (x + 20, 110), scaled)
compare.save(OUT / 'source44-to-destination36-vfx.png')

manifest = {'date': '2026-11-16 Asia/Seoul', 'scope': 'Direct saved-PNG VFX extraction and timeline shift only; no runtime change',
            'source': '../player-combo-preview-r07/manifest.json', 'shift': -8, 'mapping': mapping,
            'checks': {'all_saved_source_frames_reconstructed_from_base_and_extracted_fx': True,
                       'every_source_tail_frame_preserved_once_in_order': True, 'all54_body_sword_poses_unchanged': True,
                       'all54_transparent_composites_identical_to_r09': True, 'original_files_unchanged': True},
            'frames': rows}
(OUT / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
with (OUT / 'vfx-timing.csv').open('w', newline='', encoding='utf-8-sig') as file:
    writer = csv.DictWriter(file, ['frame', 'vfx_source_frame', 'fx_purple_image', 'fx_blue_image'])
    writer.writeheader()
    writer.writerows({k: r[k] for k in writer.fieldnames} for r in rows)
print(json.dumps({'source_tail': [44, last_source], 'destination': [36, last_source - 8], 'checks': manifest['checks']}))
