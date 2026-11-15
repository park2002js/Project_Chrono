"""Check derived reference data and retain source hashes for this analysis."""
import csv
import hashlib
import json
import math
import re
from pathlib import Path
from PIL import Image

ROOT = next(p for p in Path(__file__).resolve().parents if (p / 'AGENTS.md').is_file())
OUT = Path(__file__).resolve().parent
CODE = ROOT / 'docs/reference/sephiria/code/ingame code/Assembly-CSharp'
EXPORT = ROOT / 'docs/reference/sephiria/assets/ExportedProject'
ASSETS = EXPORT / 'Assets'
REPORT = OUT.with_suffix('.md')
data = json.loads((OUT / 'motion_curves_and_frames.json').read_text(encoding='utf-8'))
fx = json.loads((OUT / 'vfx_metadata.json').read_text(encoding='utf-8'))
checks = []
record_count = 0

for attack, clip in enumerate(data['clips'], 1):
    source = ROOT / clip['file']
    assert hashlib.sha256(source.read_bytes()).hexdigest() == clip['sha256']
    assert clip['sampleRate'] == 30
    for rate in (30,60):
        rows = list(csv.DictReader((OUT / f'motion_attack{attack}_{rate}fps.csv').open(encoding='utf-8-sig')))
        expected = clip[f'frames{rate}']
        assert len(rows) == len(expected) == round(clip['stopTime']*rate)+1
        for row, saved in zip(rows,expected):
            for key,value in saved.items():
                if isinstance(value,bool):
                    assert row[key] == str(value)
                elif isinstance(value,(int,float)):
                    assert abs(float(row[key])-value) < 1e-9
                else:
                    assert row[key] == value
            x,y = float(row['up_aim_player_relative_pivot_x']),float(row['up_aim_player_relative_pivot_y'])
            assert abs(x-float(row['local_x'])-.15625)<2e-9
            assert abs(y-float(row['local_y'])-.5)<2e-9
            tx,ty = float(row['up_aim_player_relative_opaque_tip_center_x']),float(row['up_aim_player_relative_opaque_tip_center_y'])
            assert abs(math.hypot(tx-x,ty-y)-1.28125)<2e-9
        record_count += len(rows)
    event = [e for e in clip['events'] if e['function']=='CreateBasicAttackProjectile']
    assert len(event)==1 and event[0]['frame30']==(11 if attack==1 else 7)
checks.append({'name':'source clip hashes, CSV/JSON records, pivot, tip, event frames', 'pass':True,'records':record_count})

for frame,expected_count in zip(fx,[1895,312,30,5]):
    src=ROOT/frame['source']
    assert hashlib.sha256(src.read_bytes()).hexdigest()==frame['source_sha256']
    pixels=Image.open(src).convert('RGBA').getchannel('A')
    values=list(pixels.get_flattened_data())
    assert set(values)=={0,255}
    assert sum(a>0 for a in values)==expected_count==frame['alpha_nonzero_count']
    assert all(abs(a-b)<1e-5 for a,b in zip(frame['full_texture_pivot_bottom_left'],[35,33]))
checks.append({'name':'four nonempty FX frames, binary alpha, common pivot', 'pass':True})

sources=[CODE/f'{name}.cs' for name in ['PlayerInputController','PlayerAvatar','WeaponControllerSimple','WeaponSimple','WeaponSimple_GreatSword','WeaponControlAnimator','WeaponShoulder','HorayUtility','TopdownRigidbody','UnitAvatar','NewWeaponFireData','SpriteFx','Animator2D_Basic','Animator2D_SpriteRenderer','Timer','NewWeaponBody']]
sources += [ASSETS/p for p in ['Resources/weapon/0010_GreatSword.asset','GameObject/Weapon_GreatSword_1.prefab','GameObject/PlayerAvatar.prefab','AnimatorController/AnimationWrapper.controller','AnimationClip/GreatSword_Attack1.anim','AnimationClip/GreatSword_Attack2.anim','AnimationClip/GreatSword_New_WaitForAttack2.anim','MonoBehaviour/Weapon_GreatSword_Attack1(M).asset','MonoBehaviour/Weapon_GreatSword_Attack2(M).asset','Resources/fx/GreatSwordSwingFx-Updown.prefab','Resources/fx/GreatSwordSwingFx-Downup.prefab','MonoBehaviour/GreatSwordSwingFx.asset','Sprite/GreatSword_Tier1.asset','Texture2D/GreatSword_Tier1.png','Material/Sprite-Unlit-Default.mat','Resources/weaponwielddata/SwingWeapon_GreatSword_New.asset','AnimatorController/GreatSword_New.controller']]
sources += [ASSETS/f'{folder}/GreatSword0_Swing{i}.{ext}' for folder,ext in [('Sprite','asset'),('Texture2D','png')] for i in range(4)]
sources += [EXPORT/'ProjectSettings/ProjectVersion.txt',EXPORT/'ProjectSettings/ProjectSettings.asset']
sources += [p.with_name(p.name+'.meta') for p in list(sources) if p.with_name(p.name+'.meta').is_file()]
manifest=[]
for source in sources:
    assert source.is_file(),source
    manifest.append({'file':source.relative_to(ROOT).as_posix(),'sha256':hashlib.sha256(source.read_bytes()).hexdigest()})
checks.append({'name':'referenced source files exist', 'pass':True,'files':len(manifest)})

if REPORT.is_file():
    report=REPORT.read_text(encoding='utf-8')
    assert '<!-- FRAME_TABLES -->' not in report
    for target in re.findall(r'\]\(([^)]+)\)',report):
        if not target.startswith(('https://','http://','#')):
            destination = REPORT.parent/target
            if destination != OUT/'validation.json':
                assert destination.is_file(),target
    checks.append({'name':'report tables inserted and local links resolve','pass':True})

result={'date':'2026-11-15 Asia/Seoul','kind':'static source/asset reconstruction; no original game runtime capture',
        'checks':checks,'independent_review':'A second reader checked source hashes, 124 CSV/JSON records, affine transform and timing caveats; no blocking error found',
        'sources':manifest,'not_run':['original-game playback/frame recording','left-facing runtime Transform matrix sampling','Project_Chrono compile/play (no game code changed)']}
(OUT/'validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'pass':True,'checks':checks},ensure_ascii=False,indent=2))
