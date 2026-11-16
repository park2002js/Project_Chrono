# 위험 탄환 발광·금색 상호작용 링 R05

2026-11-16 사용자 요청: 첨부의 밝은 중심·붉은 발광처럼 피해야 하는 탄환을 명확히 표시하고, 금색이 둘러진 탄환은 상호작용 대상으로 알아볼 수 있게 한다. `reference-projectile.png`는 사용자 제공 참고 이미지다.

## 현재 표현과 편집 원본

- 일반탄: 넓은 밝은 흰 중심·붉은 위험 테두리·부드러운 붉은 광륜
- 황금탄: 같은 흰 중심/붉은 위험 몸체에 바깥 금색 링과 금빛 광륜을 추가
- 기존 몸체16×16px·네 변 중앙8px 평면과 Collider/CircleCast 반경0.39는 유지한다. 흰 중심도12px/중앙6px 평면으로 넓혀 마름모 인상이 강해지지 않게 했다.

`normal-bullet.aseprite`는32×32px·3레이어(Halo/DangerRim/BrightCore), `gold-bullet.aseprite`는 같은 크기의4레이어(InteractionRing 추가)다. 몸체 PNG는 중심18×18px 셀로 출력하며 실제 불투명16px는 R04와 같은 alpha다. 두 몸체는 같은 흰/빨강이며 황금탄 구분은 바깥 링이 담당한다.

`normal-halo.png`·`gold-halo.png`·`gold-ring.png`는32×32px 공통 중심이다. 광륜은 중간 alpha18단계를 가진 부드러운32px 이미지이고 금색 링의 불투명 바깥 크기는20px(몸체 밖 각2px)다. 링은 몸체 안을 덮지 않는다. 모든 출력은16PPU, 몸체/링은Point, 광륜만Bilinear다. 이 표시 크기를 피해 범위로 사용하지 않는다.

`build-projectile-vfx.lua`는 최초 제작 근거다. 이후 편집 원본을 덮어쓰는 재생성에 사용하지 않는다. 원본 수정 후 `export-vfx.lua`에 `input`·`output`·`name=normal 또는 gold`를 전달해 현재 몸체/광륜/링/합성 PNG를 분리 출력한다. `normal-composite.png`·`gold-composite.png`는 광륜 최대 알파의 합성 비교다.

## Unity 연결

실사용 `Assets/Prefabs/Enemy/NormalBullet.prefab`·`GoldBullet.prefab`과 기존 시험 Normal/Gold에 `ProjectileVfx`를 연결했다. Halo는몸체뒤(sort9), 몸체sort10, 금색 링은sort11이다. 기존 공유 비조명 재질을 사용하며 새 shader·Bloom·렌더 설정을 도입하지 않았다.

`ProjectileVfx.Halo`와 선택적 `GoldRing`은 한 번 연결해 재사용한다. 기본 광륜은MinimumOpacity0.7~1/1Hz로 알파만 왕복한다. 게임 시간이 멈추면 위상/알파도 멈추며, 비활성화/제거와 `ResetPulse()`는 원색과 위상을 복원한다. 몸체의 크기/알파·금색 링은 이 왕복으로 바꾸지 않는다. 기존 일반 제거·금색 Owner 반사/각성/게이지·피해·수명은 유지한다.

## 실제 화면·검사

`preview-white.png`·`preview-laboratory.png`는 실제 Unity1024×768 화면이며 Player/두 탄환은 같은16PPU·원본1px당 화면6px다. `preview-dark-closeup.png`는 같은 카메라의 어두운 배경16배 근접 비교다. 기존 네 종류 예고 프리팹과 원형 Area는 그대로다.

2026-11-16 09:37:48 KST에 같은 Unity6000.3.21f1의 격리 사본에서 컴파일/어셈블리 재로드·17묶음 Play Mode 성공·배치 종료0을 확인했다. 기존15검사에 펄스 정지/재개·참조/재질 재사용과 비활성화 원색/위상 복원2묶음을 추가했다. 실제 몸체/광륜/링·금색 반사/각성·예고4종 회귀를 확인했다.

현재 소유18대상이 검증 사본과 해시가 같고, 순수 예고16대상·기존 원형 Area6대상은 이전과 바이트가 같다. 동시 생성된 원본 광륜/링 GUID3개를 검증 사본에도 맞춰 최종 재검사한 뒤 참조를 보존해 반영했다. `vfx-validation.json`·`unity-validation.json`·`integration-validation.json`을 따른다.

전체85 C# 중 다른 작업의 Editor PlayerArtSetup·런타임 AttackController2개가 검증 뒤 달라 현재 전체 코드 최종 실행으로 확대하지 않는다. Editor 시작의 SearchDatabase 인덱스 예외는 관찰했으나 컴파일/기능 검사는 통과했다. 원본 Editor의 직접 Play/최종 import·제출 빌드와 경비 패턴의 금색 배치는 이번 범위가 아니다. 현재 사양/호출 흐름/기록은 GAME_SPEC B05·2.2.6을 따른다.
