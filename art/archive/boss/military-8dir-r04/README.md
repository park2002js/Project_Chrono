# 군용 로봇 현재8방향 픽셀

[Aseprite](aseprite/02-military-robot-8dir.aseprite) · [시트](military-8dir-sheet.png) · [방향별 PNG](png/) · [방향 전환 GIF](directions-turntable.gif)

국방색·모래색·청회색의 재질을 구분하고 넓은 기본색과 한 단계 그림자로 명암을 단순화했다. 몸통 기본색`#74834E`,하단·접촉 그림자`#50613D`를 사용하며 형태·외곽·방향별 크기를 유지한다.

144×144px·투명 배경·이진alpha,순서S/SE/E/NE/N/NW/W/SW의8개 정지 프레임이다. 지면 피벗은 원본 좌상단 기준(72,120),16PPU이며 가로 시트는1152×144px다. 프레임100ms와 GIF500ms는 방향 확인용이다. [manifest.json](manifest.json)에 방향·팔레트·규격을 보관한다.

사용자의 배치 요청으로 [Map 두 번째 영역](../military-map-placement-r01/README.md)에 정면 고정 Sprite로 연결했다. 자동 방향 전환·이동/공격·피격·진행 보스 등록은 미구현이다.
