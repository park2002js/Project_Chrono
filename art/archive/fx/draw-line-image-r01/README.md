# Draw 이미지 FX 선

> R01 제작 이력이다. 현재 발광 FX 원본과 적용 결과는 [R04](../draw-line-image-r04/README.md)를 따른다. R02의 단색 띠·검은 테두리는 사용자 반려 이력이다.

- 사용자 확정(2026-11-16): 30° 비교의 IMAGE FX 방식을 현재 Draw에 적용하고 입력 점의 시각적 표시를 남기지 않는다. 구체 선호 이유는 미기록이며 두 클릭의 논리 좌표·길이·폭·판정 규칙은 유지한다.
- 편집 원본: [Render-Profile.ps1](Render-Profile.ps1). System.Drawing으로 새 PNG를 출력하며 외부 패키지 설치가 필요하지 않다.
- 출력: [draw-line-profile.png](draw-line-profile.png), 96×64 RGBA. 흰 중심·청록 가장자리·연속 알파 잔광과 양 끝의 짧은 페이드다. 색상·두께·알파 강도는 선택한 스타일에 맞춘 적용값이며 별도 수치 확정과 구분한다.
- 게임 연결: `project/Assets/Art/FX/Draw/`의 같은 PNG·DrawImageFX.mat를 기존 DrawPreview/DrawLine/DrawExpired 프리팹에서 공유한다. Bilinear·Clamp·밉맵 없음·무압축이며 기존 LineRenderer의 단위 기하/폭 보정을 유지한다. SlashFlash는 기존 재질을 유지한다.
- 입력 점: DrawVfx에서 점 프리팹의 필수조건·생성·표시·보관 API를 제거했다. LineDrawer의 StartPoint/EndPoint/PointCount와 두 클릭·R/F/Shift는 유지한다.
- 확인: 같은 Unity6000.3.21f1의 격리 프로젝트에서 컴파일·assembly reload와 관련22묶음 Play Mode 검사 통과, 배치 종료0. 30° 실제 표시 시각 검토, 점 객체 부재·초기화/확정/취소·수명/만료·Slash·Title 정리 확인. 본 프로젝트로 검증 자산을 복사하고 C#69개와 자산 해시 일치를 확인했다.
- 검사 근거: `project/Logs/DrawImageFxVerification.log`, `DrawImageFxVerification-result.json`, `DrawImageFxVerification-source.json`. 원본 Editor의 이번 직접 Play·재임포트와 제출 빌드는 미확인이다. 전체 사양·시퀀스는 [GAME_SPEC 3.3.13](../../../../docs/GAME_SPEC.md#3313-draw-이미지-fx-적용--draw-line-image-r01)을 따른다.
