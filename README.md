# Project_Chrono

DKU 2026 캡스톤 디자인 작품. 고정된 검으로 싸우며, 시간을 멈춰 선을 그리는 **Draw**와 그 선을 따라 베는 **Slash**를 사용하는 싱글 플레이 보스 러시다. 데모는 경비 로봇의 공격 패턴 여섯 가지(패턴 2~7)와 처치 후 엔딩까지 담고 있다. [게임 사양](docs/GAME_SPEC.md) · [개발 작업 규칙](AGENTS.md)

## 실행과 조작

Unity Hub에서 [project](project/)를 열고 Unity **6000.3.21f1**을 사용한다. [Title 씬](project/Assets/Scenes/Title.unity)에서 Play를 누른 뒤 **시작 → 저장 슬롯**을 선택하면 [MainWorld 씬](project/Assets/Scenes/MainWorld.unity)으로 이동한다. 새 게임은 시작 통로에서 깨어나 경비를 처치하고, 출구 위 통로 끝의 어둠의 문으로 들어가면 로딩 화면 뒤에서 최종 보스 맵으로 바뀐다. 최종 보스를 처치하고 위쪽 다리 끝의 빛의 문으로 들어가면 데모 종료 문구 뒤 Title로 돌아간다. 새 게임이 시작할 맵은 [GameSystems 프리팹](project/Assets/Prefabs/Startup/GameSystems.prefab)의 `Stage Manager`에서 `Stage Index`로 고른다. 0은 경비 맵부터 이어지는 정식 흐름이고, 1로 바꾸면 새 게임이 최종 보스 맵의 입구에서 바로 시작한다. 이어하기는 슬롯에 저장된 맵을 따른다. 시연용으로 두 보스는 모든 패턴을 한 번씩 돌아가며 쓴다. 원래의 조건·가중 추첨으로 되돌리려면 [BossPatternSelector.cs](project/Assets/Scripts/Enemy/BossPatternSelector.cs)의 `CycleAllPatterns`를 `false`로 바꾼다. 같은 프리팹에서 기상 연출의 누운 시간과 화면 복귀 시간은 `Tutorial Controller`의 `Wake Hold Seconds`·`Wake Return Seconds`, 맵 전환 로딩의 최소 표시 시간은 `Game Manager`의 `Stage Loading Seconds`, 엔딩의 덮는 색과 문구 색은 `Temp Demo Ending`의 `Fade Color`·`Message Color`로 바꾼다.

| 상황 | 키보드·마우스 |
| --- | --- |
| 전투 | WASD 또는 방향키로 이동하고 마우스로 조준한다. 왼쪽 클릭 또는 Enter로 일반 공격, Space로 대시한다. |
| Draw | Shift로 진입하고 왼쪽 클릭으로 두 점을 찍은 뒤 F로 선을 확정한다. R로 점을 지우거나 Shift로 취소한다. |
| Slash·상호작용 | 일반 상태에서 F로 Slash를 사용한다. 자수정 근처에서는 F로 상호작용한다. |

## 제출 자료

| 자료 | 제출본 | 편집 원본 |
| --- | --- | --- |
| 결과보고서 | [PDF](docs/report/32211853_결과보고서.pdf) | [Word](docs/report/Project_Chrono_Report.docx) |
| 포스터 | [PDF](<docs/report/32211853 박지성_포스터.pdf>) | [PowerPoint](docs/report/포스터.pptx) |

## 아트 편집

편집 원본은 [art/archive](art/archive/)에, 게임에 연결한 파일은 [project/Assets](project/Assets/)에 있다. `Assets/Art/`에 새로 가져온 스프라이트와 Aseprite의 기본값은 16 PPU다. 이후 Inspector에서 수정한 PPU는 재임포트 때 유지된다. 개별 VFX의 원본 PNG는 [텍스처 폴더](docs/reference/sephiria/assets/Assets/Texture2D/)에서 파일명으로 찾을 수 있다. VFX 수정은 작업 대상으로 지정한 PNG나 Aseprite 파일에 저장한다. 게임용 파일을 갱신할 때 기존 `.meta`와 스프라이트 피벗을 보존한다. `art/`에는 게임과 관련된 그림·음원 원본만 둔다. 내보내기·합성 스크립트와 배치 목록은 마무리하며 지웠으므로, 게임용 파일은 `project/Assets/`의 같은 대상에 직접 반영한다.

| 대상 | 편집 원본과 반영 방법 |
| --- | --- |
| 플레이어·검 | Idle·Move·Attack·Dash 각 4종, `player0_death`·`player0_wakeup`, `player0_baldo_OpenEyes`·`player0_baldo_CloseEyes`를 편집한다. 카타나는 `sword_0`, 칼집은 `sword_1`, 검을 넣어 합쳐진 모습은 `sword_2`, 그 흰 실루엣은 `sword_3`, 발도 중 손과 무기는 `sword_with_hand`다. Editor가 Aseprite 25개의 저장을 감지해 사본과 게임 참조를 갱신한다. 죽음·기상 속도는 원본의 프레임별 ms로 조정한다. [플레이어 원본](art/archive/player/) · [검 원본](art/archive/weapon/) · [동기화 코드](project/Assets/Editor/PlayerArtSourceSync.cs) |
| 검기 | `Katana_Attack1_Swing`·`Katana_Attack2_Swing`·`Katana_Attack3_Swing` PNG를 각각 5장·8장·8장 순서로 사용한다. 1·2타는 번호 0부터, 3타는 00부터다. 원본 PNG를 편집한 뒤 Unity의 같은 이름 사본을 갱신한다. [검기 원본](art/archive/weapon/fx/) · [게임용 검기](project/Assets/Art/Weapons/AttackVfx/) |
| 보스·환경 | 경비의 [부위·효과 원본](art/archive/boss/guard/)과 [음원](art/archive/sfx/guard/)을 보관한다. Unity 메뉴 **Project Chrono > Rebuild Guard Assets**·**Rebuild Guard Map**은 `project/Assets`에 있는 파일과 목록으로 클립·프리팹 연결을 다시 만든다. 조우·사망 컷씬의 길이는 `GameSystems` 프리팹 `Stage Manager`의 `First Encounter Duration`·`Reencounter Duration`·`Boss Death Duration`에서, 처치된 경비가 사라지는 시간은 `GuardRobot` 프리팹 `Boss Spawner`의 `Defeat Fade Delay`·`Defeat Fade Duration`·`Defeat Fade Hold`에서 바꾼다. 배경음 음량과 켜지고 꺼지는 시간은 `GameSystems` 프리팹의 `Battle Music`에서, 총구 소리의 음량과 발사음 시작 지점은 `GuardRobot` 프리팹의 `Muzzle Volume`·`Muzzle Fire Sound Start`에서, 패턴 7 폭발음의 간격과 음량은 같은 프리팹 `Guard Hand Attack`의 `Wave Blast Interval`·`Wave Blast Volume`에서 바꾼다. 충격파 자리 배열의 흐트러짐은 `GuardRobot` 프리팹 `Guard Hand Attack`의 `Wave Ring Twist`·`Wave Spot Jitter`로 조절하며 둘 다 0이면 줄 맞춘 부채꼴이 된다. 충격파의 중심각은 같은 곳의 `Wave Angle`, 닿는 거리와 폭발을 허용할 위쪽 한계는 `Boss_01`의 `Edge Wave Reach`(구역 폭 대비 비율)·`Edge Wave Top`(경비 바닥에서 잰 높이)으로 바꾼다. 프리팹에서 조정한 수치는 다시 만들어도 유지된다. 경비 전투 구역의 크기는 [Map 프리팹](project/Assets/Prefabs/Startup/Map.prefab)의 `GuardRobotEncounter`에 붙은 `Boss Arena`의 `Size`로 바꾼다. 플레이 중에는 Hierarchy에 생성된 맵의 같은 객체에서 바꾸면 바로 반영되고, 플레이를 끝내면 프리팹 값으로 돌아간다. 경비 맵은 시작 통로에서 어둠의 문까지 이어진 한 장의 그림이다. [경비 맵](art/archive/environment/lab-descent-map/) · [석실·개폐 문](art/archive/environment/stone-room-preview/) · [석조 문](art/archive/environment/ancient-gate/) · [실험실 입구 문](art/archive/environment/laboratory/final/) · [자수정 원본](art/archive/environment/amethyst/final/source/) |
| 최종 보스·석대 맵 | [동작·효과·기둥·수정 원본](<art/archive/boss/last boss/>), [석대 맵 원본](<art/archive/boss/last boss/warped-facility-map/>), [음원](<art/archive/sfx/last boss/>), [공통 피격·기절음](art/archive/sfx/)을 보관한다. Unity 메뉴 **Project Chrono > Rebuild Last Boss Assets**는 `project/Assets`에 있는 파일과 목록으로 클립과 보스·유도탄·맵 프리팹을 다시 만든다. 보스 프리팹에서 조정한 수치는 다시 만들어도 유지된다. 한 패턴만 반복해서 보려면 플레이 중 Hierarchy의 `LastBoss`에서 `Force Pattern`에 1·2·3·5·6·7 가운데 하나를 넣는다. 음량은 `LastBoss` 프리팹의 `Sound Volume`·`Rift Volume`에서 바꾼다. |
| 앞뒤 정렬 | 새 캐릭터·구조물·탄환 프리팹은 최상위에 `Depth Sorter`를 붙이면 발끝 높이에 따라 다른 대상의 앞뒤에 그려진다. 기준점이 발끝이 아니면 `Foot Offset`에 발끝까지의 세로 거리를 넣고(아래쪽이 음수), 몸이 떠서 움직이면 `Ground Point`에 바닥 지점 객체를 연결한다. 그림자처럼 바닥에 고정할 자식은 코드에서 `DepthSorter.DrawOnGround`로 뺀다. |
| 맵 편집 | 두 맵은 한 장짜리 그림을 쓴다. 타일로 짠 옛 실험실 배치는 맵 프리팹에서 뺐다. 타일 Atlas는 패딩 4px와 Bilinear를 유지한다. 맵 그림을 고치면 [게임 텍스처](project/Assets/Art/Environment/)도 갱신한다. |
| 화면·입력 안내 | [Title](art/archive/title/title-screen-r02/)은 사용자 원본과 흰 테두리 1px를 보존하고, 푸른 광 3장은 검흔에서 약 ±3px 안쪽에 양끝이 투명한 별도 이미지로 둔다. [HUD·조준점](art/archive/ui/final/) · [저장 슬롯 프레임](art/archive/ui/window-frame-r01/). 키보드 PNG는 [사용자 제공 시트](art/archive/ui/final/keyboard/source/kb_dark_all.png)의 90종을 4가지 외형으로 분리한 자료다. 게임 안내에는 Shift와 F를 쓴다. [키맵](project/Assets/Art/UI/Keyboard/key-map.json) |
| 전투 효과·음향 | Draw 선을 수정할 때 [원본 PNG](art/archive/fx/draw-line-image-r04/draw-line-profile.png)의 크기·끝마감을 보존하고 입자가 같은 텍스처를 참조하게 한다. [적중 섬광 폴더](art/archive/fx/player-hit-r01/)에는 PNG 7장, [플레이어 음원 폴더](art/archive/sfx/player/)에는 공격·대시·발소리 음원이 있다. [일반탄·특수탄 원본과 시트](art/archive/fx/탄환/)(Unity에는 일반탄의 `-12px` 판을 내보낸다) · [공격 예고 마스크 사용법](#공격-예고-vfx-사용법) · [각성 오라](project/Assets/Art/Player/FeverAura/) |

### 공격 예고 VFX 사용법

공격이 닿을 바닥 범위를 먼저 정하고 그 크기에 맞춰 아래 마스크를 조합한다. 흰색은 표시할 영역, 알파는 그 픽셀에 입힐 색의 농도다. 마스크를 수정할 때는 같은 이름의 Aseprite 원본을 열고 투명도를 유지해 PNG로 내보낸다. [원본과 PNG 폴더](art/archive/fx/공격예고/)

| 공격 범위 | 마스크 사용법 |
| --- | --- |
| 양 끝이 열린 직선 경로 | `열린경로-측선` 두 개를 진행 방향과 평행하게 놓고 그 사이를 `공통-채움`으로 채운다. 측선 폭은 1px이며, 1×5px 마스크의 가운데만 늘린다. 선과 채움 모두 끝에서 첫 픽셀은 투명, 둘째는 절반 농도, 셋째부터 기본 농도다. 예를 들어 80px 경로는 양 끝 2px씩과 가운데 76px로 구성한다. |
| 닫힌 사각형 | `사각-9슬라이스`의 상하좌우 테두리를 1px로 설정한다. 모서리는 그대로 두고 변은 길이 방향으로, 중앙은 가로·세로로 늘린다. Unity Sprite Editor의 Border 값은 네 방향 모두 1이다. |
| 원·타원 | 원하는 지름 또는 가로·세로 크기로 픽셀 격자를 잡고 도형 안쪽을 채운다. 경계 안쪽 한 겹은 1px 테두리로 처리한다. 미리보기의 원은 48×48px, 타원은 64×32px다. |
| 부채꼴 | `부채-반원채움`과 `부채-반원호`의 왼쪽 변 중앙을 공격 시작점에 맞춘다. 이를 좌우 대칭으로 배치하고 조준 방향 양쪽에 중심각의 절반씩 표시한다. 반지름을 바꾸면 최종 격자에서 원호와 양쪽 직선 경계를 1px로 잡는다. |

**색과 투명도.** 흰 마스크에 적대 예고색 **`#FF1559`**(RGB 255, 21, 89)를 입힌다. 한 곳에서 색을 지정하면 `RGBA(255, 21, 89, 168)`을 쓰고 나머지 재질 알파는 100%로 둔다. 그러면 원본 알파가 255인 테두리는 **약 66%**, 128인 내부는 **약 33%**로 보인다. 색과 재질을 따로 제어할 때는 색 알파 225/255와 재질 알파 190/255를 각각 한 번 적용하면 같은 농도가 나온다.

**재생 순서.** 예고A에서 경로를 표시한다. 예고B에서는 같은 경로를 점멸시키고 공격이 임박할수록 점멸과 비프음 간격을 줄인다. 예고C에서는 공격 순간부터 경로를 진동시키고, 그 경로의 마지막 탄환이나 위험이 완전히 지나간 뒤 기본 0.2초에 걸쳐 지운다. 피해 판정은 공격이 담당한다. [공격 예고 사양](docs/GAME_SPEC.md#공격-예고-f-b06)

**Unity에서의 사용.** [공격 예고 프리팹](project/Assets/Prefabs/Enemy/Warnings/AttackWarning.prefab)의 `AreaWarningVfx`가 `Warning Color`(기본 `#FF1559A8`)를 흰 마스크에 입힌다. 공격 코드는 `ShowOpenPath`·`ShowRectangle`·`ShowEllipse`·`ShowSector` 중 하나로 범위를 놓고 `BeginNotice`(예고A) → `BeginImminent`(예고B) → `BeginDanger`(예고C) → `EndDanger` 순서로 부른다. 열린 경로와 닫힌 사각형은 위 마스크를 9분할로 늘리고, 타원과 부채꼴은 최종 픽셀 격자에서 1px 테두리를 다시 계산한다. 예고는 실험실 바닥 레이어보다 위, 캐릭터보다 아래인 정렬 순서 12~13에 그린다. [표시 코드](project/Assets/Scripts/Enemy/AreaWarningVfx.cs) · [경비 손 공격 사용 예](project/Assets/Scripts/Enemy/GuardHandAttack.cs)
