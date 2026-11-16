# Player 성공 적중 섬광

`Slash_Player0.png`~`Slash_Player6.png`는 Sephiria SwordSlashFx의7장을 원본 바이트 그대로 보존한 결과다. 각5×57px·16PPU·중앙 피벗(2.5,28.5)이며 자르기·색 변경·확대를 하지 않았다.

출처는 `docs/reference/sephiria/assets/ExportedProject/Assets/Texture2D/Slash_PlayerN.png`다. 원작 이미지를 새로 창작한 원화로 표시하지 않는다. 출처·원본 해시는 [manifest.json](manifest.json)을 따른다.

게임의 `project/Assets/Art/Player/HitFx/`에서 성공한 피해 접점에만 재생한다. PlayerFeedback이 배치·무작위 각도·재사용·정리를 담당하고 절대 게임 경과시간 기준7장/0.35초로 표시한다.

실제 import·접점 배치·월드 고정·프레임·정리를 관련23검사로 확인했다. 전체 Player 빌드 시연과는 구분한다.
