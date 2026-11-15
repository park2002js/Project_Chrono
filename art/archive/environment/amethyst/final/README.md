# 자수정 최종 전달본

사용자 수정 형태와 승인된 어두운 보라색 비활성·선명한 보라색 활성 상태를 유지합니다. 활성은 몸체만2px 범위에서1.6초 주기로 움직이며 그림자는 고정됩니다.

## 편집 원본

- `aseprite/amethyst-inactive.aseprite`: 비활성 수정,1프레임
- `aseprite/amethyst-active-float.aseprite`: 활성 부유,8프레임×200ms
- `aseprite/placement-inactive.aseprite`: 배치 확인용 비활성 맵 크롭
- `aseprite/placement-active.aseprite`: 배치 확인용 활성 맵 크롭·동일8프레임
- `source/amethyst-user-edited.aseprite`: 사용자가 직접 수정한 원본의 바이트 보존본

배치본은 원본 Map의`(0,0,464,560)`만 남겨 첨부처럼 위쪽 방과 문 아래 수정·복도 구간을 보여줍니다. 크롭 밖 아래쪽 방의 cel 데이터와 빈6레이어도 제거했습니다. 원점과 수정 좌표는 유지됩니다.

## Unity용 입력과 배치

Unity 에셋 위치는 `project/Assets/Art/Environment/Amethyst/`입니다. `Prefabs/Amethyst.prefab`을 기존 LaboratoryLayout의 자식으로 놓고 아래 로컬 좌표를 적용합니다. 프리팹의 Animator Bool `Active`는 기본값false이며, true이면 활성 부유를 반복하고 false이면 정지한 비활성 그림으로 돌아갑니다. 코드에서 연결할 때는 해당 Animator의 `SetBool("Active", true/false)`를 사용합니다.

`Animations/Amethyst.controller`와 `AmethystInactive.anim`·`AmethystActiveFloat.anim`이 포함됩니다. 별도 런타임 스크립트 없이 SpriteRenderer와 Animator를 사용하며 기존 `LaboratoryUnlit.mat`을 공유합니다. 게임의 실제 수정 상호작용 사건과 이 Bool을 연결하는 작업은 별도입니다.

`unity/amethyst-inactive.png`는16×40, `unity/amethyst-active-float-sheet.png`는128×40의8프레임 시트입니다. 실제 수정 몸체는16×32이며 나머지는 부유·그림자 여백입니다.16PPU, Point, 무압축, Mipmap 없음, 각16×40 Sprite의 아래 기준 pivot은`(0.5,0.1)`입니다.

`unity/animation.json`에 프레임 시간·Y오프셋·바닥 앵커를 기록했습니다. 원본 전체 실험실 Map 기준 바닥 앵커는 픽셀`(200,464)`, 기존464×1232 LaboratoryLayout의 로컬 좌표는`(12.5,48,0)`입니다. **464×560 크롭 그림을 독립 배치할 때만** 로컬 좌표가`(12.5,6,0)`이 됩니다. 크롭 이미지는 위치 확인용이며 기존 Unity 맵을 대체하지 않습니다.

## 재출력

Aseprite 편집 후 같은16×40 프레임·바닥 앵커를 유지하여 비활성PNG와 활성 가로 시트를 다시 출력하고 `unity/` 및 Unity 에셋의 `Textures/`에 반영합니다. 기존 `.meta`는 유지해야 Sprite·애니메이션 참조가 보존됩니다. 프레임 수나 크기를 바꾸면 슬라이스·클립도 함께 수정해야 합니다. 색상·배치·동작 검증과 원본 보존 결과는 `validation/`에 있습니다. `preview/active-float.gif`는 부유 미리보기입니다.
