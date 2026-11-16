# 실험실 맵·복도·문 최종 전달본

현재 형태·배치는 corridor-r15, 색상은 사용자 확정 **palette-r02-c(2026-11-16, C 절충안)** 기준입니다. 이후 편집은 이 폴더의 현재 Aseprite에서 시작합니다.

## 검흔 대비 팔레트

밝은 벽과 웜그레이 바닥 `#B0ADA1`로 연구시설의 밝기를 유지하면서 검흔과 구분합니다. 바닥 무늬·크랙·대리석 위치는 유지하고 최초 바닥의 명도 편차를0.25배로 낮췄습니다. 문은 밝은 회색 계열이며 상태 표시등은 그대로입니다. 실제 검흔14개 PNG의 연보라 `#E5D2FF`·연하늘 `#C3EAFF`와 형태·알파·외곽선은 전혀 바꾸지 않았습니다. 역할별 RGB 치환과 확정 값은 [palette.json](palette.json)을 따릅니다.

기본 바닥과 검흔의 원본 sRGB 상대휘도 대비는 보라/하늘 순서로 **1.606/1.771**입니다. 실게임 가독성 실측값은 아닙니다. [1920×1080 화면](preview/palette-vfx-1920x1080.png)은 사용자가 선택한 C 비교 화면과 픽셀이 같습니다. [이전/수정 비교](preview/palette-before-after.png)는 왼쪽이 어두운 R01·오른쪽이 현재 C입니다. 실제 맵·검흔 PNG를 합성한 정적 검토판이며 새 플레이 캡처가 아닙니다.

## Unity에서 사용

Unity용 결과는 `project/Assets/Art/Environment/Laboratory/`에 있습니다. `Prefabs/LaboratoryLayout.prefab`을 씬에 배치하면 두 방과 복도가 배치된 Tilemap을 사용할 수 있습니다. 입구와 출구는 `LaboratoryEntrance.prefab`, `LaboratoryExit.prefab`으로 별도 배치할 수 있습니다.

- `Textures`: 원본 크기 환경 아틀라스와 입구·출구 시트.16PPU, Point, 무압축, Mipmap 없음.
- `Tiles`: 환경 타일43개. 방 바닥·크랙·벽·복도를11개 Tilemap 레이어로 재배치할 수 있습니다.
- `Animations`: 프레임 시간과 태그를 반영한 클립 및 Animator Controller.
- `Data`: 타일 배치·문 좌표·프레임과 크기 메타데이터.
- `Prefabs`: 연결 맵과 입구·출구 문.

문 Animator는 `Open`, `Close` Trigger를 받습니다. 입구는 `closed → opening → open`, 출구는 `closed → activation → opening → open`으로 연결됩니다. 출구 activation은 초록/검정100ms씩3회 점멸한 뒤 초록300ms 점등합니다. 점멸·점등 중 문판은 정지합니다. 닫기는 `closing → closed`이며, closed/open은 마지막 그림을 유지합니다. 미리보기 GIF가 자동 반복되는 것은 게임의 개방 조건이 아닙니다.

연결 맵 프리팹의 문은 처음에 모두 닫혀 있습니다. Aseprite 연결 맵은 편집 비교용으로 가운데 문이 열린 기존 상태를 보존했습니다. 최초 전달 후 실제 게임의 Startup/Map·Boss 사건·벽/문 Collider·카메라 연결이 추가됐으며 현재 상태는 GAME_SPEC 3.3.10과 이후 연결 기록을 따릅니다. 이번 팔레트 작업에서는 기존 연결을 보존했습니다. 환경 Tile 자체의 ColliderType은 None이며 실제 게임의 충돌체와 구분합니다.

## 크기는 이후 조정 가능

**현재 방·복도·문 크기와 PPU는 이후 조정할 수 있는 제작 기준값이며 영구 고정 사양이 아닙니다.** 현재는16px 셀, 방 바닥400×288px(25×18셀), 통로80px(5셀), 벽16px(1셀)입니다. 원본을6배로 확대 저장하지 않습니다.

16PPU와 Transform scale1에서1셀=1Unity unit입니다.1920×1080에서 원본1px을6×6px로 표시하려면320×180 원본 픽셀 시야가 필요하며, 직교 카메라 크기는5.625입니다. 기존 게임 카메라 설정은 변경하지 않았습니다.

방·복도 크기를 바꿀 때는 이미지를 늘이기보다 정수 셀을 추가/제거해 패턴을 재배치하고, 출입구·벽·바닥 재질 경계와 문 앵커를 함께 옮깁니다. PPU를 바꾸면 타일·문 Sprite·Grid·카메라·이후 연결할 충돌/배치를 함께 확인합니다. `unity/layout.json`의 현재 치수와 전체 타일 배치를 기준으로 수정할 수 있습니다. 기존 타일의 `id`·`name`과 문 프레임 이름은 유지하고, 새 타일은 새 번호로 추가해야 재가져오기 때 참조를 보존할 수 있습니다.

문 Sprite의 피벗은 아래 중앙(0.5,0)입니다. 문 배치의 원본 좌표는 위에서 아래로 증가하며 legacy rect `[x,y,112,64]`를 씁니다. 연결 맵의 문 위치는 `((x+56)/PPU, (canvasHeight-y-64)/PPU)`입니다. 출구112×128 시트의 위쪽 상승 여백은 이 앵커에 이미 포함되므로 추가64px 보정을 하지 않습니다.

## 편집 원본과 재생성

`aseprite/`에는 현재 팔레트의 연결 맵·문 없는 복도·입구·출구·환경 타일셋5개가 있습니다. R15의 형태·알파·레이어·cel·태그·시간을 보존하면서 RGB만 변경했습니다. 타일셋에는 현재 환경에서 사용하는43타일만 있습니다.

`unity/`의 PNG3개·JSON3개가 Unity 가져오기 입력입니다. **색상만 바꿀 때는 현재 Aseprite에서 PNG3개를 내보내 이 폴더와 프로젝트 Textures의 같은 이름 파일만 갱신합니다.** `.meta`·GUID·Sprite 잘라내기·프리팹은 유지합니다. 현재 `map.spriteatlasv2`의4px padding/Bilinear 설정은 타일 간격 보정을 위한 사용자 설정이므로 변경하지 않습니다. 공유 `LaboratoryUnlit.mat`도 다른 아트가 사용하므로 전체 색상 보정에 쓰지 않습니다.

전체 구조 재생성 메뉴 **Tools/Chrono/Import Laboratory Art**는 현재 게임의 수동 배치·충돌체·참조 수정에 영향을 줄 수 있으므로 팔레트 수정에는 실행하지 않습니다. 구조 변경에 필요하다면 먼저 현재 프리팹 변경을 입력/연결 과정에 반영해야 합니다. Aseprite 저장만으로 PNG/JSON이 자동 동기화되지는 않습니다.

`preview/`에는 현재 맵·출구 개방 GIF와 검흔 대비판이 있습니다. `validation/PaletteR02Review.json`·`PaletteR02Publish.json`·`PaletteR02Import.json`이 현재 C안의 원본/출판/가져오기 검사입니다. `PaletteReview.json`·`PalettePublish.json`·`PaletteImport.json`·`palette-r01.json`은 이전 어두운 R01 이력입니다. `art-review.json`·`art-preparation.json`·`UnityImport.json`과 정리 내역은2026-11-15 최초 전달 이력입니다. 새 게임 플레이·빌드는 이번 색상 작업에서 실행하지 않았습니다. 사용자 ETG 참고 이미지와 분석 자료, Player·Title·자수정 원본은 변경 대상이 아닙니다.

최종/Unity PNG3개는 바이트가 일치하고 검흔14개·메타·Atlas·공유 재질의 내용은 보존됐습니다. 이번 검사에서 Editor.log 증가가0bytes여서 PNG3개의 실제 자동 가져오기 완료는 미확인입니다. 에디터 Asset Database 갱신 후 실제 게임에서 움직이는 검흔을 확인해야 하며, 파일 복사·정적 프리뷰와 실제 게임 화면 검증은 구분합니다. 상세는 `validation/PaletteR02Import.json`을 따릅니다.
