# 경비 몸체·양손 분리 원본

[guard-separated.aseprite](guard-separated.aseprite)는 [경비 정면 원화](../guard-r05/README.md)의6레이어를 보존한 편집 원본이다. [export.lua](export.lua)로 몸체·양손을 분리 출력한다.

| 출력 | 크기 |16PPU 연결 |
| --- | --- | --- |
| [몸체](guard-body.png) |192×120 |pivot(0.5,16/120) |
| [왼손](guard-hand-left.png) |34×40 |bottom-center,home(-4.5,0) |
| [오른손](guard-hand-right.png) |34×40 |bottom-center,home(4.5,0) |

세 PNG를 재합성하면 원화와 같은 그림이다. 실제 `project/Assets/Art/Enemy/Guard/`와 경비 프리팹에 연결했다. 원본 편집 후 같은 출력 크기·피벗·GUID를 유지한다.

손의 현재 추적·충전·낙하와 중앙 양손 공격은 [최종 패턴 결과](../guard-patterns-r02/README.md)를 따른다.
