# Draw 이미지 FX R04 — 양끝의 오라 마감

- 사용자 요청(2026-11-16): R03의 발광 스타일은 긍정 확인했으며, 빨대처럼 잘린 시작·끝을 중앙과 같은 오라가 감싸도록 마감한다.
- [원본 PNG](draw-line-profile.png): 내장 image_gen으로 R03을 수정한1536×1024 RGBA. 중앙의 흰 발광 중심·청록/푸른 코로나·광륜 스타일을 유지하고, 양끝의 흰 중심은 이미지 안쪽에서 부드럽게 닫은 뒤 그 바깥까지 유색 중간 알파 광륜이 이어지게 했다. [정확한 수정 프롬프트](prompts.json)를 보관한다.
- 양끝 투명도: 전체 왼쪽 끝 열의 최대 알파2/255, 오른쪽 끝 열3/255다. 중앙 행의 흰 중심은 대략x87~1447 안쪽에서 종료되며 양쪽 바깥에 cyan/blue 중간 알파 영역이 남아 메시 경계의 잘린 단면을 숨긴다. 중앙 단면은 R03의 흰 중심78행→75행·코로나741행→746행으로 유사하다.
- 기존 연결 유지: 같은 게임 PNG 경로·GUID/재질, Stretch·numCapVertices0·표시 배율3, 판정 폭0.25·기본 길이6·다음 Draw의 길이 연동·점 미표시를 유지한다. 새 렌더러나 끝점 객체는 만들지 않는다.
- 길이 의미: 데이터와 메시의 시작/끝은 그대로다. 흰 core가 살짝 안쪽에서 닫히고 광륜이 끝을 감싼다. 끝 페이드의 UV 폭은 길이에 비례하므로12에서는6보다 월드상의 마감 구간도 길어지며, 고정 월드 반경 마감으로 구현한 것은 아니다.
- 실제 Play: [길이6 편집](preview-length6.png), [길이12 확정](confirmed-length12.png). 확대는 [6 시작](length6-start.png)·[6 끝](length6-end.png)·[12 시작](length12-start.png)·[12 끝](length12-end.png)이다. 같은 밝은 바닥에서 실제 카메라로 촬영했고 root가6장 모두 읽어 잘린 단면 제거와 광륜 마감을 확인했다. 사용자 최종 미술 검토와 구분한다.
- 확인: 같은 Unity6000.3.21f1의 격리 프로젝트에서 현재 코드 컴파일/어셈블리 재로드·관련23묶음 Play Mode 성공·배치 종료0. 양끝 알파·흰 core 안쪽 종료/바깥 광륜, 길이6→12 메시/Slash·판정폭·점 미표시와 기존 수명/만료/중단/Title 정리를 확인했다. 현재 원본/검증 C#69개·3프리팹/GUID와 PNG3곳 해시가 일치한다.
- 근거: `project/Logs/DrawImageFxCapsVerification.log`, `DrawImageFxCapsVerification-result.json`, `DrawImageFxCapsVerification-source.json`. 원본 Editor 직접 Play·재임포트 완료와 제출 빌드는 미확인이다. 현행 사양과 시퀀스는 [GAME_SPEC 3.3.13](../../../../docs/GAME_SPEC.md#3313-draw-이미지-fx-적용--draw-line-image-r01)을 따른다.
