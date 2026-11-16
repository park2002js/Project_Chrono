# HUD·조준점·키보드 최종 패키지

사용자 수정 HUD5종·조준점과 제공한 키보드 시트를 편집 기준으로 보존한다.

| 위치 | 내용 |
| --- | --- |
| [HUD 원본](hud/aseprite/) · [PNG](hud/png/) | Boss/Player HP·Groggy·Draw·Slash |
| [조준점](aim-cursor/) | 31×31 원본·PNG |
| [키보드](keyboard/README.md) | 원본 시트·90종×4변형의360 PNG·키맵 |
| [PLACEMENT](PLACEMENT.md) · [layout.json](layout.json) | 1920×1080 배치·앵커·pivot·오프셋 |
| [현재 프리뷰](preview/1920x1080.png) | 최종 배치 설명 |

게임의 HUD·Cursor·Keyboard 에셋에 연결했다. Sprite는 Point·16PPU·Full Rect·무압축·Mipmap 없음이며 조준점 hotspot은(15,15)다. HP/Groggy 채움·쿨타임·Shift/F 안내·커서의 실제 연결은 [GAME_SPEC](../../../../docs/GAME_SPEC.md)을 따른다.

원본 수정 후 PNG를 다시 내보내고 기존 meta·GUID를 보존한다. 키보드360종은 재사용 가능한 외형 라이브러리이며 모든 키가 게임 안내에 동시에 표시되는 것은 아니다.
