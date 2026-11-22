# Project_Chrono

DKU 2026 캡스톤 디자인 작품. 고정된 검으로 싸우며, 시간을 멈춰 선을 그리는 **Draw**와 그 선을 따라 베는 **Slash**를 사용하는 싱글 플레이 보스 러시다. 데모는 경비 로봇의 다섯 공격 패턴과 처치 후 엔딩까지 담고 있다. [게임 사양](docs/GAME_SPEC.md) · [개발 작업 규칙](AGENTS.md)

## 실행과 조작

Unity Hub에서 [project](project/)를 열고 Unity **6000.3.21f1**을 사용한다. [Title 씬](project/Assets/Scenes/Title.unity)에서 Play를 누른 뒤 **시작 → 저장 슬롯**을 선택하면 [MainWorld 씬](project/Assets/Scenes/MainWorld.unity)으로 이동한다.

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

편집 원본은 [art/archive](art/archive/)에, 게임에 연결한 파일은 [project/Assets](project/Assets/)에 있다. 게임용 파일을 갱신할 때 기존 `.meta`와 스프라이트 피벗을 보존한다. GIF와 프레임표의 재생 속도는 검토용이다.

| 대상 | 편집 원본과 반영 방법 |
| --- | --- |
| 플레이어·검 | [플레이어](art/archive/player/)의 Idle·Move·Attack·Dash 각 4개와 [검](art/archive/weapon/)의 `sword_0~3.aseprite`를 편집한다. Unity Editor가 열려 있으면 [PlayerArtSourceSync](project/Assets/Editor/PlayerArtSourceSync.cs)가 저장을 감지해 프로젝트 사본과 참조를 갱신한다. 대시는 독립 원본이며, 현재 검은 검기 크기 비교를 위해 선택한 `sword_0`이다. 사망·기상은 [전용 원본](art/archive/player/final/aseprite/), 발도·납도는 [R02](art/archive/player/iaido-r02/)를 쓴다. |
| 검기 | [검기 원본](art/archive/weapon/greatsword-vfx-edit/aseprite/)의 `VFX_EDIT` 또는 `SLASH_A_EDIT`·`SLASH_B_EDIT` 레이어를 편집한 뒤 공격별 명령으로 PNG를 출력하고 [게임용 검기](project/Assets/Art/Weapons/AttackVfx/)에 반영한다. 로컬 Aseprite 경로는 각 명령의 `CHRONO_ASEPRITE`에 있다. [1타](art/archive/weapon/greatsword-vfx-edit/Export-PNG.cmd) · [2타](art/archive/weapon/greatsword-vfx-edit/Export-PNG-Attack2.cmd) · [3타](art/archive/weapon/greatsword-vfx-edit/Export-PNG-Attack3.cmd) · [3타 타이밍](art/archive/weapon/player-combo-preview-r14/vfx-timing.csv) |
| 보스·환경 | [경비 정면 원화](art/archive/boss/guard-r05/aseprite/)에서 분리한 몸체와 양손은 [분리 원본과 출력 스크립트](art/archive/boss/guard-hand-r01/)로 갱신한다. 군용 로봇의 맵 배치는 정적이다. [군용 3D 원본](art/archive/boss/military-3d-r05/) · [8방향 스프라이트](art/archive/boss/military-8dir-r04/) · [실험실·문](art/archive/environment/laboratory/final/) · [자수정 원본](art/archive/environment/amethyst/final/source/) |
| 맵 편집 | 실험실 크기는 16px 셀 단위로 바꾸고 문 앵커·충돌·카메라 경계를 함께 옮긴다. 타일 Atlas는 패딩 4px와 Bilinear를 유지한다. PNG 재출력 때 [게임 텍스처](project/Assets/Art/Environment/Laboratory/Textures/)도 갱신한다. [실험실 배치](art/archive/environment/laboratory/final/unity/layout.json) · [자수정 바닥 앵커와 부유값](art/archive/environment/amethyst/final/unity/animation.json) · [첫 입장 배치](art/archive/environment/entrance-corridor-r03/manifest.json) |
| 화면·입력 안내 | [Title](art/archive/title/title-screen-r02/)은 사용자 원본과 흰 테두리 1px를 보존하고, 푸른 광 3장은 검흔에서 약 ±3px 안쪽에 양끝이 투명한 별도 이미지로 둔다. [HUD·조준점 배치값](art/archive/ui/final/layout.json) · [저장 슬롯 프레임 출력](art/archive/ui/window-frame-r01/Export-PNG.cmd). [슬롯 예시 화면](art/archive/ui/window-frame-r01/info-r03/slots-bold.png)의 기록은 촬영용 표본이다. 키보드 PNG는 [사용자 제공 시트](art/archive/ui/final/keyboard/source/kb_dark_all.png)의 90종을 4가지 외형으로 분리한 자료다. 게임 안내에는 Shift와 F를 쓴다. [키맵](art/archive/ui/final/keyboard/key-map.json) |
| 전투 효과·음향 | Draw 선을 수정할 때 [원본 PNG](art/archive/fx/draw-line-image-r04/draw-line-profile.png)의 크기·끝마감을 보존하고 [입자 팔레트](art/archive/fx/draw-line-grains-r09/palette-samples.json)와 같은 텍스처를 참조하게 한다. 적중 섬광 7장과 공격·적중·발소리 WAV 6개는 Sephiria에서 가져왔다. [탄환 출력](art/archive/fx/boss-hazards-r06/export-vfx.lua) · [각성 오라](art/archive/fx/player-fever-r01/) · [Slash 합성음 생성기](art/archive/audio/slash-r01/generate.py) · [섬광 출처·해시](art/archive/fx/player-hit-r01/manifest.json) · [음원 출처·해시](art/archive/audio/player-feedback-r01/manifest.json) · [이벤트와 표본](art/archive/audio/player-feedback-r01/source-events.json) |
