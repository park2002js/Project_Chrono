# Draw 이미지 FX R03 — 발광 중심과 광륜의 두께 수정

> 사용자에게 발광 스타일의 긍정 확인을 받은 제작 이력이다. 양끝 오라 마감까지 반영한 현행은 [R04](../draw-line-image-r04/README.md)를 따른다.

- 사용자 정정(2026-11-16): 처음 선택한 IMAGE FX 자체가 더 굵어지는 것이 요청 의도다. R02의 단색 띠·검은 고정 테두리는 AI 해석 오류로 반려됐으며 당시23검사 성공은 기술 이력일 뿐 미술 승인이 아니다.
- [원본 PNG](draw-line-profile.png): 내장 image_gen으로 제작한1536×1024 RGBA. 처음 비교판의 B를 스타일 참조로 삼아 흰 발광 중심→밝은 aqua/cyan→푸른 코로나→부드러운 외곽 알파가 이어지는 실제 투명 텍스처를 제작했다. 검은 불투명 테두리는 없다. 정확한 생성·보정 프롬프트는 [prompts.json](prompts.json)에 보존한다.
- 게임 연결: 기존 `Assets/Art/FX/Draw/draw-line-profile.png`와 GUID·DrawImageFX.mat를 유지한다. bilinear/clamp·무압축·밉맵 없음이며 이미지 자체의 발광 몸통과 광륜을 Draw3종 프리팹의 표시 배율3으로 넓혔다. SlashFlash는 기존 배율1, 판정 폭0.25는 유지한다. 새 렌더러나 URP Bloom 설정 변경은 없다.
- 길이/입력: 기존 LineLength→다음 Draw의 미리보기/확정/Slash 인계 구조와 점 미생성을 유지한다. 기본 길이는6이며12는 검증용 값이다.
- 실제 Play 화면: [길이6 편집](preview-length6.png)·[길이12 확정 후 밝은 바닥](confirmed-length12.png). 흰 발광 중심과 청록/푸른 광륜이 타일 위에 부드럽게 표시되는 것을 확인했다. 사용자 최종 미술 검토와는 구분한다.
- 확인: 같은 Unity6000.3.21f1의 격리 프로젝트에서 현재 코드 컴파일/어셈블리 재로드와 관련23묶음 Play Mode 성공, 배치 종료0. 흰 중심·cyan/blue 색·다양한 중간 알파·불투명 검정 외곽 부재, 길이6→12 메시/Slash·판정폭0.25·점 없음·기존 수명/만료/중단/Title 복귀를 확인했다.
- 본 프로젝트 반영: 검증한3개 선 프리팹을 기존 GUID로 복사하고 해시를 확인했다. 현재 원본/검증본 C#69개와 원본/게임/검증본 PNG도 일치한다. 근거는 `project/Logs/DrawImageFxGlowVerification.log`, `DrawImageFxGlowVerification-result.json`, `DrawImageFxGlowVerification-source.json`이다.
- 확인 한계: 원본 Editor 직접 Play·재임포트 완료와 제출 빌드는 미확인이다. 현행 사양·시퀀스는 [GAME_SPEC 3.3.13](../../../../docs/GAME_SPEC.md#3313-draw-이미지-fx-적용--draw-line-image-r01)을 따른다.
