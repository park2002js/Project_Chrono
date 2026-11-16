# 문이 안 보이는 실제 Player 입장 R03

[입장 GIF](player-entrance.gif) · [첫 빈 화면](player-entrance-start.png) · [중앙 도착·조작 인계](player-entrance-handoff.png) · [프레임 시트](player-entrance-contact-sheet.png)

격리 Unity6000.3.21f1의 실제 새 게임에서 Player가 `(14.5,-11.6)`부터 별도 종료점 `(14.5,-4.6)`까지 위로 7world 걸어오는 장면입니다. 카메라는 종료점에 고정하고 기본 줌 `5.625`를 유지합니다. 기존 저장 체크포인트 `(14.5,1.4)`는 이동하지 않습니다. 첫 화면은 Player 몸체와 검 전체가 화면 아래에 숨고, 촬영 전 구간에서 첫 입구문도 화면 위쪽에 가려집니다.

원본 PNG 36장 중 연속으로 동일한 빈 프레임 하나를 시간을 보존해 병합하여 GIF는 35프레임·2.46초입니다. 관측한 게임 시간은 약 1.664650초이며 마지막 800ms는 도착 상태를 보기 위한 hold입니다. 이를 고정 FPS 애니메이션 길이로 해석하지 않습니다. GIF 양자화 왕복 일치를 확인했고 기존 GIF/PNG 바이트를 보존한 채 파일·원본 프레임 SHA256을 [manifest.json](manifest.json)에 기록했습니다. 정지 검사 시간·경계/벽/취소 fixture와 OnGUI HUD는 GIF에서 제외합니다.

실제 7개 검사가 모두 통과했습니다.

1. 새 게임의 입력 잠금과 첫 화면 몸체/검 완전 비가시, 고정 카메라 준비
2. 기존 체크포인트 보존·12행/432셀·별도 종료점과 촬영 내내 첫 문 비가시
3. 차단 중 눌린 행동의 누출 방지·정지 중 입장 이동과 권한 유지
4. 실제 보행·root viewport `(0.5,0.5)` 도착·정상 추적 복구·인계 후 보행/대시의 남쪽/양옆 경계 제한
5. 완료 표식 디스크 재로드·완료 저장과 키 없는 구저장의 체크포인트 복귀
6. 실제 벽 충돌 정지·벽 제거 후 재개·Title 중단 시 미완료 저장 방지
7. 중단 후 재시도·명시적 취소·옛 코루틴의 새 세션 입력 해제 방지

첫 화면 몸체 viewport 위쪽은 `-0.01111114`, 검은 `-0.03982657`, 모든 캡처에서 첫 문 viewport 최소Y는 `1.17555559`였습니다. 실제 도착과 `0.0002world` 잔여 이동 오차는 0이며 걷기 Sprite 6종·발소리 5회를 관측했습니다. 중앙 도착은 발 pivot인 Player root 기준이고 Sprite bounds 중심과 구분합니다.

Map와 WorldCamera 두 프리팹만 GUID를 유지해 출판했고 프리팹/meta 총 4파일이 검증본과 일치했습니다. Player·GameSystems는 동일하여 다시 복사하지 않았습니다. [R03 편집 원본과 기하](../../environment/entrance-corridor-r03/README.md), `project/Logs/TutorialEntryR03-{result,metrics,verification,outputs,publication,gif-verification}.json`과 `TutorialEntryR03Final.log`가 근거입니다. R01/R02는 보존하고 Temp 세이브만 사용했습니다. 원본 프로젝트 직접 Editor Play와 빌드는 미실행입니다.
