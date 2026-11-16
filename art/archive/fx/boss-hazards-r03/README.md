# 보스 탄환 1px 추가 확대·황금색 변형 R03

2026-11-16 사용자 수정: 탄환 주위에 각 방향1px을 더하고, 같은 모양의 황금색 변형을 하나 만든다. R02 내부 채움 장판은 유지한다.

- `normal-bullet.aseprite`·`.png`:14×14px 셀·실제 불투명12×12px·1프레임·5레이어. R02의12×12px 셀/불투명10×10px에서 각 방향1px 확장했다. 기존 흰 중심·빨간 몸체를 유지하고 외곽 윤곽을 이동했다.
- `gold-bullet.aseprite`·`.png`: 일반탄과 전체 형태·alpha·레이어 구성이 같고 세 가지 색만 치환했다. 갈색 윤곽 RGB(168,108,21), 금빛 몸체(255,196,61), 밝은 중심(255,250,224)은 제작 초기안이다.
- `expand-and-recolor.lua`: R02 편집 원본에서 이번 확대·색 치환을 수행한 제작 근거다. 이후 사용자 편집본을 덮어쓰는 재생성에 사용하지 않는다. PNG 출력은 R01의 `export.lua`에 현재 원본/출력 경로를 넘겨 저장한다.
- `bullet-validation.json`: 두 원본 재열기·14px 셀/12px 도트·대칭·이진 alpha·금색의 형태/alpha 동일·색만 변경·프로젝트 PNG 일치 결과다.

Unity는 두 PNG를 모두16PPU·Point·압축/mipmap 없음으로 사용한다. 일반탄은 기존 NormalBullet.prefab, 금색탄은 새 GoldBullet.prefab이며 기존 시험 Normal/Gold도 참조·GUID를 유지해 갱신한다. 실제 판정/CircleCast 반경0.29는 새 크기에 맞춘 초기값이다. 금색탄은 이미 구현된 GoldProjectile의 Owner 반사·각성·게이지 동작을 사용하며 경비 패턴의 금색 배치를 결정한 것은 아니다.

`preview-white.png`·`preview-laboratory.png`는 실제 Unity 카메라1024×768 캡처다. Player·일반탄·황금탄은 모두16PPU·원본1px당 화면6px이며 발밑 기준선을 맞췄다. 실제 탄환 높이는12px로 Player20px의 약60%다. 아래 채움 장판은 R02 표현 그대로다.

2026-11-16 08:56:38 KST에 같은 Unity6000.3.21f1의 격리 사본에서 컴파일/어셈블리 재로드·13묶음 Play Mode 성공·배치 종료0을 확인했다. 기존11묶음에 금색의 실제 Owner 반사·중복 Slash 거부·각성/회복/게이지1회와 Owner 도달 피해/종료2묶음을 추가했다. 관련11대상은 검증 사본과 해시가 같고, Area 코드/표시/원판/프리팹6대상은 R02와도 바이트가 같다. `unity-validation.json`·`integration-validation.json`을 따른다.

런타임48 C#은 검증 사본과 모두 같으며 전체83 C# 중 다른 작업의 Editor2개만 동시 변경됐다. 현재 전체 Editor 최종 검증으로 확대하지 않는다. Editor 시작의 SearchDatabase 인덱스 예외는 관찰했고 이후 컴파일·기능 검사는 통과했다. 원본 Editor 직접 Play/import·제출 빌드는 미실행이다. 전체 사양·근거는 GAME_SPEC B05.1·2.2.6을 따른다.
