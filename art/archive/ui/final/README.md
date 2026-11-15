# UI 최종 전달

사용자가 수정한 HUD5종·조준점과 제공한 키보드 시트를 기준으로 정리한 결과다. 원본 픽셀과 Aseprite 레이어를 보존하며, 이전 R01~R03 출력·중간 제작 자료 대신 이 폴더를 사용한다.

| 위치 | 내용 |
| --- | --- |
| `hud/aseprite/` | 사용자 수정 원본5개 |
| `hud/png/` | Boss/Player HP·Groggy·Draw·Slash PNG5개 |
| `aim-cursor/aseprite/`, `aim-cursor/png/` | 31×31 조준점 원본·PNG |
| `keyboard/source/` | 제공된 원본 시트 |
| `keyboard/png/` | 90종×4변형, 총360개 개별16×16 PNG |
| `keyboard/key-map.json` | 키 이름·변형·PNG·시트 좌표·입력 매칭 참고값 |
| [PLACEMENT.md](PLACEMENT.md), `layout.json` | 1920×1080 위치 비율·Unity 앵커·pivot·오프셋 |
| [최종 프리뷰](preview/1920x1080.png), `preview/preview.html` | 현재 배치 결과·배치 소스 |

최종 배치는 Boss를 원본2px 위로, 나머지를 원본1px 아래로 옮긴 결과다. 화면 확대율6배, 스킬 간격6원본px, HP/스킬 상단 정렬은 유지한다. 키 안내는 별도 스프라이트로 준비했으며 프리뷰에 임의로 추가하지 않았다.

## Unity 사용

Unity 에셋 경로는 `project/Assets/Art/UI/` 아래 `HUD/`, `Cursor/`, `Keyboard/`다. 모두 **Sprite(Single), Point, 16PPU, Full Rect, 압축 없음, Mipmap 없음**을 사용한다. 조준점은 `Cursor.SetCursor`용으로도 읽을 수 있게 Read/Write를 켜며, hotspot은(15,15)이다. HUD 크기·배치는 PPU로 맞추는 대신 PLACEMENT의 RectTransform 값을 사용한다. `HUD/layout.json`과 `Keyboard/key-map.json`은 TextAsset으로도 준비했다.

키보드는 `key-f-dark.png`, `key-shift-dark.png`처럼 파일명으로 선택할 수 있고, `light`, `light-outline`, `dark`, `dark-outline`4종은 외형 변형이다. 해당 이름을 눌림/비활성 동작으로 해석하지 않는다. 키와 스킬의 실제 연결은 별도이며, 모호한 문장부호에는 입력 키를 임의로 지정하지 않았다.

HP/그로기 PNG는 사용자 저장 상태의 합성 이미지다. 수치에 따른 채움 제어가 필요하면 보존된 Aseprite 레이어를 이용해 게임 UI에 연결한다. 이번 작업은 에셋과 위치 준비이며 씬 배치·실제 HP/그로기/쿨타임·마우스/키 입력 연결은 수행하지 않았다.

## 검증 결과

- `native-export-validation.json`: HUD·조준점6개 PNG와 Aseprite 합성의 전 픽셀 일치
- `keyboard/validation.json`: 360개 파일·원본셀 픽셀·시트 전체 포함 확인
- `preview/validation.json`, `layout.json`: 최종 이동·간격·정렬·화면 경계와 앵커 좌표 복원 확인
- `source-preservation.json`: 사용자 원본6개와 키보드 시트의 보존 확인
- `unity-validation.json`: 실제 Unity 임포트 결과
- `cleanup.json`: 최종 보존 목록 확인 후 제거한 이전 UI 자료

화면은 별도 디코더로 재합성한2,073,600픽셀과 모두 일치한다. 같은Unity6000.3.21f1 격리프로젝트에서366개Sprite·2개TextAsset의import/로드·크기·GUID·키맵·5앵커복원을확인했고,배치2회종료0·컴파일오류0이다. 실제생성`.meta`를포함한740파일을본프로젝트에복사하고해시를대조했다. 본프로젝트Editor재임포트·Unity게임플레이·기능연결검증은수행하지않았다.
