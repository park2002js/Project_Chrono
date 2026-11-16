# 탄환 동일 크기 색상 변형 R06

2026-11-16 사용자 정정: R05의 덧씌운 금색 링은 두꺼워 보여 반려했다. 일반탄의 빨강을 금색으로 바꾸어 같은 크기·형태에서 구분한다. 별도 바깥 링은 제거한다.

`normal-bullet.aseprite`·`gold-bullet.aseprite`는 모두32×32px·3레이어(Halo/DangerRim/BrightCore)다. 몸체 출력은18×18px 셀·불투명16×16px·네 변 중앙8px 평면, 흰 중심은12px/중앙6px 평면으로 같다. 일반탄과 황금탄의 몸체·광륜 alpha/형태가 완전히 같다.

일반탄의 붉은 테두리 RGB(255,38,48)만 황금탄 RGB(255,208,52)로 바꿨다. 흰 중심은 공통이며, 광륜도 같은 모양에서 빨강/금색으로 구분한다. 일반탄은 R05 그대로다. 별도1~2px를 더 두르는 링·자식·runtime참조와 프로젝트의 gold-ring.png/.meta는 제거했다. 이전 R05 편집 원본/이미지는 이력으로 보존한다.

`build-projectile-vfx.lua`는 이번 제작 근거이며 이후 사용자 편집본을 덮어쓰는 재생성 용도로 사용하지 않는다. 원본 수정 후 `export-vfx.lua`에 input/output/name=normal 또는 gold를 전달하면 몸체/광륜/합성 이미지를 분리 출력한다. `vfx-validation.json`은 원본 재열기·빨강만금색치환·흰 중심/형태/alpha/크기 동일·일반탄 R05 보존 결과다.

Unity의 NormalBullet·GoldBullet과 시험 Normal·Gold는 모두 본체와 Halo 두 SpriteRenderer만 사용한다. 기존16PPU·몸체Point/광륜Bilinear·판정0.39·광륜0.7~1/1Hz scaled pulse와 금색의 반사/각성/회복/게이지 동작을 유지한다. 장판·순수 예고4종도 유지한다.

`preview-white.png`·`preview-laboratory.png`는 Player와 같은6배 실제 Unity 화면이고, `preview-dark-closeup.png`는16배 근접 비교다. 두 탄환은 색만 다르며 금색에 덧붙은 링은 없다.

2026-11-16 09:48:48 KST에 같은 Unity6000.3.21f1의 격리 사본에서 컴파일/어셈블리 재로드·기존17묶음 Play 성공·배치 종료0을 확인했다. 두 Sprite 구성과 동일 경계/링 없음·기존펄스/반사/각성·예고 회귀를 확인했다. 전체85 C#/런타임50개와 이번15대상이 검증 사본과 같고, 예고16대상·기존 Area6대상은 이전과 바이트가 같다. 기존GUID를 보존했다. `unity-validation.json`·`integration-validation.json`을 따른다.

Editor 시작의 SearchDatabase 인덱스 예외는 관찰했지만 컴파일/기능 검사는 통과했다. 원본 Editor 직접 Play/최종 import·제출 빌드는 미실행이다. 현재 사양·시퀀스·선택 기록은 GAME_SPEC B05.3·2.2.6과 Boss_Hazard_Visuals_20261116 선택6을 따른다.
