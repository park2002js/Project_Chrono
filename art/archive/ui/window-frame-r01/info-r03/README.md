# SaveSlot 정보·입력 R03 실제 화면

2026-11-16 사용자 요청의 Esc 메뉴 복귀·정보 빈 줄·Title/슬롯 Bold를 적용한 실제 Unity6000.3.21f1 격리1080p 캡처다. 기존 프레임 원본/PNG/meta와 카드144×206·12px 경계는 변경하지 않았다.

- `slots-bold.png`: 빈 슬롯·진행 중72분/13회·완료125분/27회의 메모리 표본. 실제 저장 파일을 만들거나 덮어쓰지 않았다.
- `menu-bold.png`·`esc-returned-title.png`: 최종 Title 메뉴와 실제 Input System Esc 복귀 결과.
- `slots-confirmation-bold.png`: 동일 Bold가 적용된 완료 기록 확인창.
- `menu-normal.png`·`slots-normal.png`: 같은 실행에서 굵기 비교만을 위해 잠깐 Normal로 표시한 검사 캡처. 실제 최종 스타일은 Bold다.
- `text-metrics.json`: 동일 영역의 글자 면적 증가와4개 정보 행의 빈 간격. 일반적인 폰트 두께 배율이나 성능 측정이 아니다.
- `verification.json`: focused5검사 success=true/종료0, 실제 키보드 상태 이벤트·저장 참조 보존·준비/로딩 차단·재활성화 검증과 소유12파일 일치 기록.

현재 사양은 docs/GAME_SPEC.md3.3.15 R03을 따른다. 동시6런타임은 이 실행 범위 밖이며 원본 Editor 직접Play·월드 진입/복귀·제출 빌드는 미실행이다.
