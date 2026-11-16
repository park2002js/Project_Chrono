# 실제 Player 최초 입장 R01

[입장 GIF](player-entrance.gif) · [시작 화면](player-entrance-start.png) · [조작 인계 화면](player-entrance-handoff.png) · [프레임 시트](player-entrance-contact-sheet.png)

격리 Unity6000.3.21f1의 실제 Title 새 게임에서, Player가 `(14.5,-4.6)`에서 `(14.5,1.4)`까지 위로 6world 걸어온 장면입니다. 기본 월드 카메라 줌을 유지하며 입력 차단/정지 검사 뒤의 깨끗한 보행부터 정상 도착·조작 인계까지 촬영했습니다. OnGUI HUD와 벽/취소 검사 fixture는 GIF에 포함하지 않습니다.

27프레임·총 2.29초이며 끝의 800ms는 도착 상태를 보기 위한 정지 hold입니다. 촬영된 실제 경과는 약 1.488초이고, 이를 고정 FPS 애니메이션 길이로 해석하지 않습니다. 원본 PNG→GIF의 프레임 양자화 왕복 일치를 확인했습니다. 기존 GIF/PNG 바이트는 보존하고 파일·원본 프레임 SHA256을 [manifest.json](manifest.json)에 기록했습니다.

자동 보행은 실제 충돌/몸체/발소리 경로를 사용합니다. 해당 실행에서 도착 오차 0·걷기 Sprite 6종·발소리 5회와 인계 후 새로운 입력에 따른 실제 이동을 확인했습니다. 최초 입장 완료는 도착 시 저장하며 구저장/완료 저장은 기존 체크포인트 흐름을 유지합니다. [복도 확장 수치·편집 원본](../../environment/entrance-corridor-r01/README.md)과 `project/Logs/TutorialEntry-{result,metrics,verification,publication,outputs}.json`이 근거입니다.

최종 격리 7검사가 통과했고 Map/GameSystems/WorldCamera 세 프리팹이 GUID를 유지해 원본에 출판됐습니다. Player 프리팹은 변경하지 않았습니다. 이전 Guard/Map fixture는 원본으로 재동기화했으며, Temp 세이브만 사용했습니다. 원본 프로젝트 직접 Play와 빌드는 미실행입니다.
