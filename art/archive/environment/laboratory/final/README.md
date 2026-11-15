# 실험실 맵·복도·문 최종 전달본

현재 미술은 corridor-r15 기준입니다. 이전 리비전과 생성 중간물은 사용자 요청으로 정리하며, 이후 편집은 이 폴더의 현재 Aseprite에서 시작합니다.

## Unity에서 사용

Unity용 결과는 `project/Assets/Art/Environment/Laboratory/`에 있습니다. `Prefabs/LaboratoryLayout.prefab`을 씬에 배치하면 두 방과 복도가 배치된 Tilemap을 사용할 수 있습니다. 입구와 출구는 `LaboratoryEntrance.prefab`, `LaboratoryExit.prefab`으로 별도 배치할 수 있습니다.

- `Textures`: 원본 크기 환경 아틀라스와 입구·출구 시트.16PPU, Point, 무압축, Mipmap 없음.
- `Tiles`: 환경 타일43개. 방 바닥·크랙·벽·복도를11개 Tilemap 레이어로 재배치할 수 있습니다.
- `Animations`: 프레임 시간과 태그를 반영한 클립 및 Animator Controller.
- `Data`: 타일 배치·문 좌표·프레임과 크기 메타데이터.
- `Prefabs`: 연결 맵과 입구·출구 문.

문 Animator는 `Open`, `Close` Trigger를 받습니다. 입구는 `closed → opening → open`, 출구는 `closed → activation → opening → open`으로 연결됩니다. 출구 activation은 초록/검정100ms씩3회 점멸한 뒤 초록300ms 점등합니다. 점멸·점등 중 문판은 정지합니다. 닫기는 `closing → closed`이며, closed/open은 마지막 그림을 유지합니다. 미리보기 GIF가 자동 반복되는 것은 게임의 개방 조건이 아닙니다.

연결 맵 프리팹의 문은 처음에 모두 닫혀 있습니다. Aseprite 연결 맵은 편집 비교용으로 가운데 문이 열린 기존 상태를 보존했습니다. 현재는 **미술 배치와 애니메이션을 사용할 수 있는 에셋 패키지**입니다. 기존 MainWorld/Startup Map 교체, Boss 사망 조건 연결, 벽·문 Collider와 Trigger, Player와 벽의 최종 그리기 순서 연결은 후속 작업입니다. 환경 Tile의 ColliderType은 None입니다.

## 크기는 이후 조정 가능

**현재 방·복도·문 크기와 PPU는 이후 조정할 수 있는 제작 기준값이며 영구 고정 사양이 아닙니다.** 현재는16px 셀, 방 바닥400×288px(25×18셀), 통로80px(5셀), 벽16px(1셀)입니다. 원본을6배로 확대 저장하지 않습니다.

16PPU와 Transform scale1에서1셀=1Unity unit입니다.1920×1080에서 원본1px을6×6px로 표시하려면320×180 원본 픽셀 시야가 필요하며, 직교 카메라 크기는5.625입니다. 기존 게임 카메라 설정은 변경하지 않았습니다.

방·복도 크기를 바꿀 때는 이미지를 늘이기보다 정수 셀을 추가/제거해 패턴을 재배치하고, 출입구·벽·바닥 재질 경계와 문 앵커를 함께 옮깁니다. PPU를 바꾸면 타일·문 Sprite·Grid·카메라·이후 연결할 충돌/배치를 함께 확인합니다. `unity/layout.json`의 현재 치수와 전체 타일 배치를 기준으로 수정할 수 있습니다. 기존 타일의 `id`·`name`과 문 프레임 이름은 유지하고, 새 타일은 새 번호로 추가해야 재가져오기 때 참조를 보존할 수 있습니다.

문 Sprite의 피벗은 아래 중앙(0.5,0)입니다. 문 배치의 원본 좌표는 위에서 아래로 증가하며 legacy rect `[x,y,112,64]`를 씁니다. 연결 맵의 문 위치는 `((x+56)/PPU, (canvasHeight-y-64)/PPU)`입니다. 출구112×128 시트의 위쪽 상승 여백은 이 앵커에 이미 포함되므로 추가64px 보정을 하지 않습니다.

## 편집 원본과 재생성

`aseprite/`에는 연결 맵·문 없는 복도·입구·출구·현재 환경 타일셋5개가 있습니다. 연결 맵/복도/두 문 원본은 R15와 바이트가 같고, 타일셋은 현재 환경에서 실제 쓰는 타일만 다시 묶었습니다. 과거 문의 잔여 타일은 포함하지 않습니다.

`unity/`의 PNG3개·JSON3개가 Unity 가져오기 입력입니다. 원본 수정 후 해당 PNG/JSON을 갱신하고 Unity 메뉴 **Tools/Chrono/Import Laboratory Art**를 실행하면 프로젝트의 에셋과 프리팹을 재생성합니다. 메뉴는 입력 폴더가 아니라 생성 결과를 갱신하므로, 생성된 프리팹만 수동 수정했다면 재실행 전에 그 변경을 배치 데이터에 반영해야 합니다. Aseprite 저장만으로 PNG/JSON이 자동 동기화되지는 않습니다.

`preview/`에는 현재 모습과 출구 개방 GIF만 남겼습니다. `validation/`은 최종 전달 검증과 삭제 내역입니다. 사용자 ETG 참고 이미지와 분석 자료, Player·Title 원본은 이 정리의 삭제 대상이 아닙니다.
