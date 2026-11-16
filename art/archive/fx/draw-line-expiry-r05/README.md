# Draw 선 소멸 R05 — 알파 왕복 후 같은 FX 분해

- 사용자 요청(2026-11-16): 선이 투명해졌다 원래 알파로 복귀하는 동작을 반복한 뒤, 해당 FX 자체가 분해되는 듯 사라진다. 기존 R04의 중앙 발광·두께·양끝 오라·길이/판정·점 미표시는 유지한다.
- 구현 원본: [DrawImageFx.shader](DrawImageFx.shader)는 게임의 `Assets/Shaders/DrawImageFx.shader`와 같은 소스다. DrawLineVfx가 Awake에 캐시한 MaterialPropertyBlock으로 개별 `_Opacity`·`_DissolveProgress`를 전달하며 공용 재질을 매번 만들거나 바꾸지 않는다.
- 경고: 기존 경고 수명 구간에서 `0.5+0.5*cos(2π×LifePhase)`로 알파를 연속 왕복한다. 객체는 계속 활성 상태이며 경고 구간 마지막12%에는 원본 알파로 연결해 분해 시작의 갑작스러운 밝기 변화를 줄인다.
- 분해: 같은 R04 PNG와 흰 tint를 유지하고 폭·길이 Transform을 축소하지 않는다. 분해 진행률0에서는 원본 모습,1에서는 완전 소멸이며 중간에는 월드 좌표 노이즈와 미세 UV 흐트러짐으로 작은 빛 조각이 사라진다. shader의 `_Time`을 쓰지 않아 Skill의 같은 게임 시간 정지·복구를 따른다.
- 기본 적용값: LineLifetime8초·WarningFraction0.35·왕복 주기0.5→0.08초는 유지하고 분해 시간은0.65초다. 테스트 영상은 빠른 확인용 수명2.4초·경고0.9·주기0.4→0.08초를 사용하며 실제 기본값 변경과 구분한다.
- [실제 Play 미리보기 GIF](draw-expiry-preview.gif)는54개 기록 PNG의 game-time 간격을 사용한다. 반복 재생 전 마지막 빈 화면만0.5초 유지하며 이 여백은 게임의 추가 지연이 아니다. [원본 시간·상태 CSV](frames.csv)·[인코딩 정보](preview-info.json)·[재출력 도구](Encode-Preview.py)를 보관한다.
- 정지 화면: [원본 알파](alpha-full.png)·[투명 구간](alpha-clear.png)·[분해 중간](decomposing.png)·[완료](expired.png). 실제 프레임을 읽어 알파 왕복·같은 색의 조각화·최종 빈 화면을 확인했고 GIF의54프레임/시간과 원본 PNG 대조도 확인했다. 사용자 최종 미술 확인과 구분한다.
- 검증: 같은 Unity6000.3.21f1의 독립 사본 `project/Temp/FxV`에서 현재 변경 컴파일/어셈블리 재로드와 관련23묶음 Play Mode 성공, 배치 종료0. 알파의 중간값·양방향 변화·가속·정지/재Draw 보존, MPB 반영, 동일 PNG/색·폭 고정·분해 단조 진행·완료, 교체/사망/재사용 정리를 확인했다.
- 반영 범위: 새 shader/컴포넌트 API·DrawVfx 표현 로직·기존 DrawImageFX.mat/DrawExpired 프리팹·Player의 분해 시간만 반영했다. 새 Slash 필수 참조는 검증 사본에서만 기존 SlashPresentationSetup으로 준비했으며 그 연출 자산·Player/Renderer 설정을 원본으로 복사하지 않았다. 현재 원본 runtime C#46개와 이번 소유6파일이 검증본과 일치한다. 총75개 검증 C# 중 병행 Slash 작업의 Editor 파일은 별도이므로 전체 최신 일치로 기록하지 않는다.
- 근거: `project/Logs/DrawExpiryVerification.log`, `DrawExpiryVerification-result.json`, `DrawExpiryVerification-source.json`(실제 검증 사본), `DrawExpiryVerification-scope.json`. 원본 Editor의 이번 직접 Play·재임포트 완료와 제출 빌드는 미확인이다. 현재 사양·기존6Case 시퀀스는 [GAME_SPEC 3.3.13](../../../../docs/GAME_SPEC.md#3313-draw-이미지-fx-적용--draw-line-image-r01)을 따른다.
