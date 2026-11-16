# 실제 Player 화면 밖 입장 R02

[입장 GIF](player-entrance.gif) · [첫 빈 화면](player-entrance-start.png) · [중앙 도착·조작 인계](player-entrance-handoff.png) · [프레임 시트](player-entrance-contact-sheet.png)

격리 Unity6000.3.21f1의 실제 새 게임에서 Player가 `(14.5,-5.6)`부터 `(14.5,1.4)`까지 위로 7world 걸어오는 장면입니다. 카메라는 도착점에 고정하고 기본 줌 `5.625`를 유지합니다. 첫 화면에서 몸체와 검 전체가 화면 아래에 숨고, root가 viewport `(0.5,0.5)`에 도착하면 조작을 인계하며 정상 Player 추적을 복구합니다. root는 발 pivot이며 Sprite bounds 중심과 구분합니다.

실제 원본 PNG 39장으로 만들었습니다. 연속으로 동일한 빈 프레임 하나는 시간을 보존해 병합하여 GIF는 38프레임·2.42초입니다. 관측한 게임 시간은 약 1.624673초이며 마지막 800ms는 도착 상태를 보기 위한 hold입니다. 이를 고정 FPS 애니메이션 길이로 해석하지 않습니다. 원본 프레임의 GIF 양자화 왕복 검증을 통과했고 기존 GIF/PNG 바이트를 보존한 채 SHA256을 [manifest.json](manifest.json)에 기록했습니다.

첫 렌더의 몸체 viewport 최대Y `-0.01111108`·검 최대Y `-0.03982651`로 완전 비가시 상태를 확인했습니다. 실제 도착 오차 0·root 정중앙·`0.0002world` 잔여 이동 오차 0과 걷기 Sprite 6종·발소리 5회도 관측했습니다. 인계 뒤 보행/대시는 남쪽·양옆 경계 안에 머물렀습니다. GIF에는 깨끗한 입장·인계만 담고 정지 검사 시간·경계/벽/취소 fixture와 OnGUI HUD는 제외합니다.

최종 7개 검사가 통과했고 Map 마커만 GUID를 유지해 출판됐습니다. GameSystems/WorldCamera/Player 프리팹은 변경하지 않았습니다. [맵 편집 원본과 전체 수치](../../environment/entrance-corridor-r02/README.md), `project/Logs/TutorialEntryR02-{result,metrics,verification,outputs,publication,gif-verification}.json`이 근거입니다. R01과 R02 첫 허용오차 성공 실행은 이전 이력으로 보존하고 최종 정밀 검사를 현행 근거로 사용합니다. Temp 세이브만 사용했으며 원본 프로젝트 직접 Play와 빌드는 미실행입니다.
