# Title 화면·별도 칼날 빛 R01

2026-11-16 사용자 요청. 회빛 검정 바탕·로고 아래 글자3메뉴와 확대/회전/소멸하는 십자 빛을 연결한다. 후속 명확화대로 **원본 로고와 원본 안의 하이라이트를 그대로 유지하고, 별도 VFX를 위에 겹친다.**

- 원본: `../logo_17_natural_cut_r06/title-logo-17-natural-cut.aseprite`와 같은 이름의 PNG. 원본 바이트는 수정하지 않는다.
- VFX 편집 원본: `generate-vfx.py`. 외부 라이브러리 없이 독립적인 흰/청록4방향 십자와 soft halo를 생성한다. 로고 파일을 읽거나 원본 픽셀을 추출하지 않는다.
- 출력/실사용: `title-blade-glint-vfx.png`(256×256 RGBA) → `project/Assets/Art/UI/Title/title-blade-glint-vfx.png`. Point·무압축·mipmap 없음·alpha 사용이다.
- 재생성: 저장소 루트에서 `py -3.11 art/archive/title/title-screen-r01/generate-vfx.py`를 실행하고 해당 출력만 같은 이름의 실사용 PNG에 복사한다. 기존 `.meta`를 보존한다.
- 연결: `UI_Title.TitleLogo`는 기존 원본, `TitleLogoGlint`는 별도 VFX다. 기준 화면1920×1080의 칼날 중심(1143,330)에320×320 오버레이를 둔다.
- 첫 구현값: #151518 배경, Intro0.25초+0.6초 뒤 시작,1.25초 표시/3.2초 반복·90도 회전·0.15~1.15배 확대·sin alpha. 이 값은 AI 튜닝이며 사용자 최종 미술·박자 승인과 구분한다.
- 검사: 원본 보존 해시는 `source-validation.json`. 같은 Unity6000.3.21f1 격리 일반 Editor의 최종5묶음·컴파일/종료0·소유14파일일치·원본 해시 보존을 확인했다. `title-preview.gif`는 실제1080p60캡처를960×540로 줄인7.3초 영상이고 `title-peak.png`/`title-idle.png`는 원본크기 캡처다. 실제 메뉴6빈영역의 #151518과 게임정지중 칼날만 변하는 결과는 `preview-validation.json`을 따른다. 동시4런타임 차이와 엔진Search/종료경고·원본직접Play/빌드 한계는 GAME_SPEC3.3.14를 따른다.

현재 사양·연결·검증 상태의 원본은 `docs/GAME_SPEC.md`3.3.14와 F-U01 SequenceDiagram이다. 설정 패널의 기존 미구현과 제출 빌드는 이번 시각 변경 완료에 포함하지 않는다.
