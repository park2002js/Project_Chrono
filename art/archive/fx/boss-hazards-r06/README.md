# 일반탄·황금탄 최종 원본

[일반탄 Aseprite](normal-bullet.aseprite) · [황금탄 Aseprite](gold-bullet.aseprite)

두 탄환은 같은 크기·형태·alpha를 사용하고 빨강과 금색으로 구분한다. 별도 바깥 금색 링은 없다. 원본은32×32px·Halo/DangerRim/BrightCore의3레이어이며 몸체 출력은18×18px 셀·불투명16×16px다.

일반탄의 붉은 테두리 RGB(255,38,48)만 황금탄 RGB(255,208,52)로 바꿨다. 흰 중심과 광륜의 형상은 공통이다. NormalBullet·GoldBullet은 몸체와 Halo의 두 SpriteRenderer를 사용하며16PPU·몸체Point·광륜Bilinear로 표시한다.

[export-vfx.lua](export-vfx.lua)에 현재 원본과 출력 경로, name=normal/gold를 전달해 몸체·광륜·합성 PNG를 다시 출력한다. PNG 출력과 meta/GUID를 함께 보존한다.

게임 표시·기존 투사체 동작을17검사로 확인했다. 금색탄은 현재 Line 법선의 반사 방향으로 직진하며 상세 규칙은 [GAME_SPEC](../../../../docs/GAME_SPEC.md)의 F-B06·F-P07을 따른다.
