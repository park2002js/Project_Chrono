"""User clarified: start with the large arc at36; preserve the entire visible VFX sequence."""
from pathlib import Path
import copy

helper = Path(__file__).resolve().parent.parent / 'player-combo-preview-r10/build_frames.py'
exec(compile(helper.read_text(encoding='utf-8').split("\nfor sub in ('native', '4x', 'vfx-only', 'body-sword-only'):")[0], str(helper), 'exec'))
visible_sequence = [r['frame'] for r in source_rows if original_fx[r['frame']].getbbox()]
assert visible_sequence == list(range(31, 50))
destinations = {36 + i: frame for i, frame in enumerate(visible_sequence)}
assert max(destinations) == 54
rows = copy.deepcopy(source_rows)
images, mapping = [], []
for sub in ('native', '4x', 'vfx-only', 'body-sword-only'):
    (OUT / 'attack-03' / sub).mkdir(parents=True, exist_ok=True)

for row in rows:
    frame = row['frame']
    src = frame if frame < 36 else destinations[frame]
    fx = original_fx[src].copy()
    result = Image.alpha_composite(bases[frame], fx)
    bases[frame].save(OUT / f'attack-03/body-sword-only/frame-{frame:02}.png')
    fx.save(OUT / f'attack-03/vfx-only/frame-{frame:02}.png')
    if frame < 36:
        for sub in ('native', '4x'):
            file = f'attack-03/{sub}/frame-{frame:02}.png'
            (OUT / file).write_bytes((source_folder / file).read_bytes())
    else:
        result.save(OUT / row['file'])
        result.resize((512, 512), Image.Resampling.NEAREST).save(OUT / f'attack-03/4x/frame-{frame:02}.png')
        assert fx.tobytes() == original_fx[src].tobytes()
        mapping.append({'source_frame': src, 'destination_frame': frame,
                        'rgba_sha256': hashlib.sha256(fx.tobytes()).hexdigest(),
                        'visible_pixels': sum(alpha > 0 for alpha in fx.getchannel('A').getdata())})
    for field in ('fx_image', 'fx_purple_image', 'fx_blue_image'):
        row[field] = source_rows[src - 1][field]
    row['fx_rgba_sha256'] = hashlib.sha256(fx.tobytes()).hexdigest()
    row['vfx_source_frame'] = src
    for field in ('grip_pixels', 'angle_degrees', 'weapon_order', 'sword_rgba_sha256', 'time_seconds', 'source_attack', 'source_frame'):
        assert row[field] == source_rows[frame - 1][field]
    assert Image.open(OUT / row['file']).convert('RGBA').tobytes() == result.tobytes()
    assert Image.open(OUT / f'attack-03/body-sword-only/frame-{frame:02}.png').convert('RGBA').tobytes() == bases[frame].tobytes()
    images.append(result)

assert [r['source_frame'] for r in mapping] == visible_sequence
assert [r['destination_frame'] for r in mapping] == list(range(36, 55))
assert rows[35]['fx_purple_image'] == rows[35]['fx_blue_image'] == 1
assert rows[53]['fx_purple_image'] == rows[53]['fx_blue_image'] == 4
assert all(sha(p) == digest for p, digest in protected.items())

BG = '#505968'
sheet = Image.new('RGB', (2100, 2825), BG)
draw = ImageDraw.Draw(sheet)
draw.text((20, 12), '3타 — 36번째 큰 호 시작 → 원본 VFX 전체 순서 → 마지막 잔광', font=FONT, fill='white')
draw.text((20, 45), 'Player·검 54포즈와 1~35번째 그림 유지 · VFX 원본 픽셀·순서·노출 간격 유지', font=SMALL, fill='white')
draw.text((20, 70), '원본의 큰 호부터 잔광까지 19개 VFX 노출 칸을 36~54번째에 배치 · 투명 PNG 유지', font=SMALL, fill='white')
for i, (row, rendered) in enumerate(zip(rows, images)):
    x, y = (i % 7) * 300, 105 + (i // 7) * 340
    draw.text((x + 8, y + 4), f"{row['frame']}번째", font=FONT, fill='white')
    sheet.paste(panel(row, rendered), (x + 6, y + 30))
    label = f"VFX 원본 {row['vfx_source_frame']} → 현재 {row['frame']}" if row['frame'] >= 36 else '기존 상태 유지'
    draw.text((x + 8, y + 319), label, font=SMALL, fill='white')
sheet.save(OUT / 'attack-03-all-frames.png')
manifest = {'date': '2026-11-16 Asia/Seoul',
    'user_confirmed': 'Frame36 starts with the LARGE arc, not the small tails; preserve following VFX sequence and player/sword',
    'implementation_choice': 'Use all19 visible source slots31..49 at36..54, preserving1..35; exact19-slot arrangement is implementation proposal, not a separately approved timing decision',
    'source': '../player-combo-preview-r07/manifest.json', 'mapping': mapping,
    'checks': {'frame36_is_both_large_arcs': True, 'all19_source_visible_vfx_slots_copied_once_in_order': True,
               'last_frame54_is_original_final_afterglow49': True, 'all54_body_sword_pixels_and_pose_fields_preserved': True,
               'frames1_through35_native_and4x_byte_preserved': True, 'all_source_files_unchanged': True}, 'frames': rows}
(OUT / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
with (OUT / 'vfx-timing.csv').open('w', newline='', encoding='utf-8-sig') as file:
    writer = csv.DictWriter(file, ['frame', 'vfx_source_frame', 'fx_purple_image', 'fx_blue_image'])
    writer.writeheader()
    writer.writerows({k: r[k] for k in writer.fieldnames} for r in rows)
print(json.dumps({'sequence': visible_sequence, 'destinations': [36,54], 'checks': manifest['checks']}))
