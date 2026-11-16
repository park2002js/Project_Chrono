"""Retiming only: R07 VFX 44..54 -> 36..46; keep each original sword/body pose."""
from pathlib import Path
import copy
import shutil

# Reuse the established compositor without running the previous revision's edits.
helper = Path(__file__).resolve().parent.parent / 'player-combo-preview-r08/build_frames.py'
exec(compile(helper.read_text(encoding='utf-8').split('\nimport copy, shutil\n')[0], str(helper), 'exec'))

previous = OUT.parent / 'player-combo-preview-r08'
fx_source = OUT.parent / 'player-combo-preview-r07'
baseline = json.loads((previous / 'manifest.json').read_text(encoding='utf-8'))
original = baseline['frames']
source = [r for r in json.loads((fx_source / 'manifest.json').read_text(encoding='utf-8'))['frames'] if r['attack'] == 3]
rows = copy.deepcopy(original)
fx_fields = ('fx_image', 'fx_purple_image', 'fx_blue_image')
source_tail = [r for r in source if r['frame'] >= 44]
tail_destinations = {r['frame'] - 8: r for r in source_tail}
assert len(source_tail) == len(tail_destinations)

# Preserve every non-VFX field; the screenshot supplies the complete pre-edit tail.
for row in rows:
    frame = row['frame']
    if frame >= 36:
        for field in fx_fields:
            row[field] = tail_destinations[frame][field] if frame in tail_destinations else None

input_files = set(sources)
for folder in (previous, fx_source, OUT.parent / 'player-combo-preview-r07/attack-01', OUT.parent / 'player-combo-preview-r07/attack-02'):
    input_files.update(p for p in folder.rglob('*') if p.is_file())
preserved = {p: sha(p) for p in input_files}
for sub in ('native', '4x'):
    (OUT / 'attack-03' / sub).mkdir(parents=True, exist_ok=True)

images = []
changed = []
for row, old in zip(rows, original):
    frame = row['frame']
    old_image, old_sword, old_fx = compose(old)
    saved_old = Image.open(previous / old['file']).convert('RGBA')
    assert old_image.tobytes() == saved_old.tobytes(), ('baseline', frame)
    rendered, sword, fx = compose(row)
    assert sword.tobytes() == old_sword.tobytes(), ('sword', frame)
    assert hashlib.sha256(sword.tobytes()).hexdigest() == old['sword_rgba_sha256']
    row['fx_rgba_sha256'] = hashlib.sha256(fx.tobytes()).hexdigest()
    assert all(row[k] == old[k] for k in old if k not in fx_fields + ('fx_rgba_sha256',)), frame
    if 36 <= frame <= 46:
        assert row['fx_rgba_sha256'] == source[frame + 7]['fx_rgba_sha256'], ('moved VFX', frame)
    elif frame >= 42:
        assert not fx.getbbox(), ('cleared', frame)
    if frame >= 42:
        assert not fx.getbbox(), ('tail empty', frame)

    # Any changed pixel must be inside the old/new VFX footprint.
    assert all(a == b or old_fx.getpixel((i % 128, i // 128))[3] or fx.getpixel((i % 128, i // 128))[3]
               for i, (a, b) in enumerate(zip(old_image.getdata(), rendered.getdata()))), frame
    if rendered.tobytes() == saved_old.tobytes():
        for sub in ('native', '4x'):
            rel = f'attack-03/{sub}/frame-{frame:02}.png'
            shutil.copyfile(previous / rel, OUT / rel)
    else:
        changed.append(frame)
        rendered.save(OUT / row['file'])
        rendered.resize((512, 512), Image.Resampling.NEAREST).save(OUT / f'attack-03/4x/frame-{frame:02}.png')
    images.append(rendered)

assert changed == [36, 37, 39, 40, 41, 42, 43, 45, 46, 47, 48, 49], changed
for sub in ('native', '4x'):
    assert [n for n in range(1, 55) if (OUT / f'attack-03/{sub}/frame-{n:02}.png').read_bytes() != (previous / f'attack-03/{sub}/frame-{n:02}.png').read_bytes()] == changed

# Compare the complete tail as pixel data, including faint remnants and empty slots.
tail_proof = []
for src in source_tail:
    destination = rows[src['frame'] - 9]
    source_render, _, source_fx = compose(src)
    destination_fx = compose(destination)[2]
    assert source_render.tobytes() == Image.open(fx_source / src['file']).convert('RGBA').tobytes()
    assert source_fx.tobytes() == destination_fx.tobytes()
    count = sum(alpha > 0 for alpha in source_fx.getchannel('A').getdata())
    tail_proof.append({'source_frame': src['frame'], 'destination_frame': destination['frame'],
                       'nontransparent_pixels': count, 'rgba_sha256': hashlib.sha256(source_fx.tobytes()).hexdigest()})
assert [p['nontransparent_pixels'] for p in tail_proof] == [60, 60, 60, 10, 10, 10, 0, 0, 0, 0, 0]
(OUT / 'vfx-preservation.json').write_text(json.dumps({'source': '../player-combo-preview-r07',
    'shift': -8, 'every_available_source_tail_slot_preserved': True,
    'omitted_source_tail_slots': [], 'mapping': tail_proof}, indent=2), encoding='utf-8')

sheet = Image.new('RGB', (2100, 2825), BG)
draw = ImageDraw.Draw(sheet)
draw.text((20, 12), '3타 — Player + 검 + 현재 검기 / 전체 54장', font=FONT, fill='white')
draw.text((20, 45), 'VFX만 이동: 기존 44~46 → 36~38 · 기존 47~49 → 39~41 · 42부터 VFX 없음', font=SMALL, fill=MUTED)
draw.text((20, 70), 'Player·검 54포즈 유지 · 첫 그림부터 왼쪽→오른쪽→다음 행 순서', font=SMALL, fill=MUTED)
for i, (row, rendered) in enumerate(zip(rows, images)):
    x, y = (i % 7) * 300, 105 + (i // 7) * 340
    draw.text((x + 8, y + 4), f"{row['frame']}번째", font=FONT, fill='white')
    sheet.paste(panel(row, rendered), (x + 6, y + 30))
    draw.text((x + 8, y + 319), f"검 Z {row['angle_degrees']:.1f}°", font=SMALL, fill=MUTED)
sheet.save(OUT / 'attack-03-all-frames.png')

checks = {
    'all54_baseline_composites_match_saved_inputs': True,
    'all54_sword_rgba_and_non_vfx_fields_preserved': True,
    'body_source_pixels_and_placement_preserved': True,
    'all11_shifted_fx_match_r07_source_rgba': True,
    'frames42_through54_fx_empty': True,
    'all_pixel_differences_inside_old_or_new_fx': True,
    'other42_native_and4x_outputs_byte_identical_to_r08': True,
    'all_input_files_and_attack1_attack2_preserved': True,
}
manifest = {
    'date': '2026-11-16 Asia/Seoul', 'scope': 'Attack3 VFX retiming only; no runtime change',
    'review_status': 'Every available source tail slot44..54 moved to36..46 with exact RGBA and interval preservation; source55/56 and total17 are not present in supplied files',
    'baseline': '../player-combo-preview-r08/manifest.json',
    'vfx_source': '../player-combo-preview-r07/manifest.json',
    'frame_numbering': 'First drawing1; left-to-right then next row',
    'edit': {'source_frames': [44, 54], 'destination_frames': [36, 46], 'shift': -8,
             'both_image3': [36, 38], 'both_image4': [39, 41], 'absent_from': 42},
    'changed_frames_from_r08': changed, 'checks': checks, 'frames': rows,
    'sources': [{'path': p.relative_to(ROOT).as_posix(), 'sha256': digest} for p, digest in sorted(preserved.items())],
}
(OUT / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
with (OUT / 'vfx-timing.csv').open('w', newline='', encoding='utf-8-sig') as file:
    writer = csv.DictWriter(file, ['frame', 'purple_image', 'blue_image'])
    writer.writeheader()
    writer.writerows({'frame': r['frame'], 'purple_image': r['fx_purple_image'], 'blue_image': r['fx_blue_image']} for r in rows)
assert all(sha(p) == digest for p, digest in preserved.items())
print(json.dumps({'frames': 54, 'changed_from_r08': changed, 'checks': checks}, ensure_ascii=False))
