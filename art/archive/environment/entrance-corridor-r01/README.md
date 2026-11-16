# 남쪽 입구 복도 확장 R01

기존 실험실의 남쪽만 6셀 늘리고, 실제 Player가 아래에서 위로 6world 걸어와 기존 시작점에서 조작을 인계하는 구성입니다. 새 튜토리얼 콘텐츠를 추가하지 않습니다.

| 항목 | 기존 | 확장 후 |
| --- | --- | --- |
| 첫 입구문 통과 차단선의 물리 중심 | `(14.5, 4.5)` | 유지 |
| 최초 출발점 | `(14.5, 1.4)` | `(14.5, -4.6)` |
| 입장 정지점·저장 체크포인트 | `(14.5, 1.4)` | 유지 |
| 출발점부터 입구문까지 세로 거리 | `3.1world` | `9.1world`, 약 `2.94배` |
| 자동 보행 | 없음 | 위쪽으로 `6world` |
| 카메라 세로 제한 | `[0, 77]` | `[-6, 77]` |

1셀은 1world이며 기존 타일의 16PPU를 유지합니다. 거리 기준은 문 통과를 막는 Collider의 물리 중심 `y4.5`입니다. 문 Sprite 전체 bounds의 중심은 `y5`, 문 루트 원점은 `y3`이며, 조우 영역의 아래 끝은 `y8.5`입니다.

편집 원본은 [authoring/MapEntranceSetup.cs](authoring/MapEntranceSetup.cs), 수치·원본 해시는 [manifest.json](manifest.json)에 보관합니다. Unity의 `Tools/Chrono/Extend Entrance Corridor` 메뉴는 Map, WorldCamera, GameSystems 세 프리팹만 대상으로 합니다. 기존 전체 아트 생성기의 프리팹 저장 전에도 같은 Map/Camera API가 연결되어 있습니다.

기존 `LaboratoryLayout`의 0~3행 Tile 참조를 반복해 `EntranceExtension` 아래에 남쪽 6행을 보충합니다. 행당 바깥 29개·바닥 5개·벽 2개, 총 216개입니다. 새로운 텍스처나 팔레트를 만들지 않습니다. 바닥 아래·입구 양옆·외곽 양옆의 기존 Collider 5개만 늘리고 다른 방, 문, Boss, 수정, 저장 체크포인트 위치와 GUID를 유지합니다. 재적용 시 전용 레이어와 마커를 재사용합니다.

`EntranceSpawnPoint`만 아래쪽에 추가하고 `EntranceStopPoint`는 원래 `start` Transform을 참조합니다. Player의 실제 배치는 저장된 `SpawnPoint` 참조와 분리되어 부활 위치를 바꾸지 않습니다. GameSystems에는 TutorialController가 없을 때만 추가합니다. 카메라 줌 `5.625`·추적 설정 `0.1283` 등 다른 수치는 유지합니다.

같은 Unity6000.3.21f1의 격리 Play에서 7개 검사가 모두 통과했습니다. 남쪽 216셀·경계 5개·약 2.94배 거리, 6world 실제 보행·도착 오차 0·걷기 Sprite 6종·발소리 5회·도착 후 새 입력의 실제 이동을 확인했습니다. 입력 차단/시간 정지·실제 벽 충돌과 제거 후 재개·Title 중단/재시도·구저장 건너뛰기·완료 표식 디스크 재로드·이전 코루틴의 뒤늦은 권한 해제 방지도 검사했습니다.

컴파일 2.766172초·C#/Shader 오류 0·배치 종료 0이며, 기존 Editor SearchDB 예외 1회는 별도로 남아 있습니다. 검증 시 C# 100개·런타임 55개·관련 22파일이 원본과 일치했습니다. 기준 해시 확인 뒤 Map/GameSystems/WorldCamera 세 프리팹이 GUID를 유지해 출판됐고, 출판 후 프리팹과 meta 총 6파일이 검증본과 일치했습니다. Player 원본은 변경하지 않았습니다. 해시와 시점은 manifest 및 `project/Logs/TutorialEntry-{result,metrics,verification,publication,outputs}.json`을 따릅니다.

[실제 입장 GIF·시작/인계 화면](../../map/player-entrance-r01/README.md)을 함께 보관합니다. GIF는 기본 월드 카메라 줌에서 촬영한 27프레임이며, 2.29초 중 마지막 800ms는 관찰용 정지 hold입니다. 원본 프레임의 GIF 양자화 왕복 일치를 확인했습니다.

이전 오라 검증용 Guard/Map fixture는 원본으로 다시 동기화했으며 재생성하지 않았습니다. 초기 두 실행은 문 좌표 기준 검사 정정이며 Map이나 런타임 실패로 기록하지 않습니다. 사용자 문서 세이브를 건드리지 않고 Temp 세이브로 검사했습니다. 이 아트 담당은 코드/원본 프리팹을 추가 변경하지 않았으며, 원본 프로젝트 직접 Play와 빌드는 미실행입니다.
