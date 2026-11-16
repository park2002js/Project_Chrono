# Draw 이미지 FX R02 — 두께·길이 연동·바닥 대비

> 사용자 반려 이력이다. 단색 띠·검은 고정 테두리는 FX 자체를 굵게 하라는 요청을 잘못 해석한 제작안이며, 기술 검사 성공을 미술 승인으로 보지 않는다. 현재 원본·화면은 [R04](../draw-line-image-r04/README.md)를 따른다.

- 사용자 요청(2026-11-16): 표시 선을 더 두껍게 하고, 길이 수치 증가를 실제 표시에도 반영하며, Draw 종료 후 밝은 바닥과 구분할 진한 색/테두리를 적용한다. 점 미표시는 유지한다.
- 편집 원본: [Render-Profile.ps1](Render-Profile.ps1), 출력 [draw-line-profile.png](draw-line-profile.png). 96×64 RGBA의 불투명 청록 중심 `#11CEE9`·짙은 청록 외곽 `#073044`·부드러운 외부 알파다. 길이 방향 끝 페이드를 제거해 경로 끝점까지 같은 단면을 사용한다. R01 원본은 이전 폴더에 보존한다.
- 표시 폭: DrawLineVfx.WidthMultiplier는 표시 전용 배율이다. 미리보기/확정/만료 프리팹은2배, SlashFlash는 기존1배다. 기본 판정 폭0.25는 유지하고 표시 메시 폭은0.5가 된다. 표시 색상·정확한 두께 수치는 이번 요청에 맞춘 적용값이다.
- 길이: SkillController.LineLength는 새 Draw 시작 때 보관하며 미리보기·확정 Line·실제 선 메시·Slash가 같은 길이를 사용한다. 길이 설정 변경은 다음 Draw부터 반영하며 이전 확정 Line은 유지한다. 게임 기본 길이는6으로 유지했고12는 검증용 값이다.
- 실제 Play 캡처: [길이6의 편집 화면](preview-length6.png), [길이12의 Draw 종료 후 정상 화면](confirmed-length12.png). 이미지 합성이 아닌 동일 Unity 버전 격리 프로젝트의 실제 화면이다.
- 확인: Unity6000.3.21f1 컴파일·어셈블리 재로드·관련23묶음 Play Mode 성공 및 배치 종료0. 길이6→12의 미리보기/확정 메시 끝점·길이, 길어져도 일정한 두께, Slash의 길이12/판정 폭0.25 인계, 점 객체 부재와 기존 수명/만료/중단/Title 정리를 확인했다. 정상 화면에서 밝은 타일과의 대비와 선 양 끝을 시각 검토했다.
- 본 프로젝트 반영: 검증한3개 선 프리팹을 기존 GUID로 복사하고 해시를 대조했다. 현재 원본/검증본 C#69개와 archive/게임/검증본 PNG3곳의 SHA256도 일치한다. 근거는 `project/Logs/DrawImageFxContrastVerification.log`, `DrawImageFxContrastVerification-result.json`, `DrawImageFxContrastVerification-source.json`이다.
- 확인 한계: 원본 Editor 직접 Play·재임포트 완료와 제출 빌드는 미확인이다. 현재 사양과 시퀀스는 [GAME_SPEC 3.3.13](../../../../docs/GAME_SPEC.md#3313-draw-이미지-fx-적용--draw-line-image-r01)을 따른다.
