# Player 공격·적중·발소리

사용자가 선택한 Sephiria 음원을 추출하여 Unity 내장 AudioSource에 연결한 PCM WAV 6개다.

| 파일 | 출처·용도 |
| --- | --- |
| [AttackSwingA.wav](AttackSwingA.wav) | attackSwish07, 일반공격 A |
| [AttackSwingB.wav](AttackSwingB.wav) | attackSwish06, 일반공격 B |
| [AttackHit.wav](AttackHit.wav) | CrashSlice 이벤트의 hitSlash01 표본, 성공 적중 |
| [FootstepTile01.wav](FootstepTile01.wav) · [02](FootstepTile02.wav) · [03](FootstepTile03.wav) | footstepRoad의 세 표본, 현재 타일 보행 |

원본은 Sephiria의 Master.bank·Master.strings.bank이며, 48kHz·stereo·PCM16으로 길이·피치·음량을 변경하지 않고 추출했다. 2타 소리는 같은 swish군에서 선택한 별도 표본이며 원작 기본2타의 전용 소리라는 뜻은 아니다. 3타는 A→B 순서로 재생한다.

게임 위치는 `project/Assets/Audio/PlayerFeedback/`다. 실제 발동·프레임·접점·걷기와 정리는 PlayerFeedback·AttackController가 담당한다. [manifest.json](manifest.json)과 [source-events.json](source-events.json)에 원본 표본·이벤트·길이·해시와 출처를 보관한다.

게임 로드·재생 시작·공격/적중/걷기 연결을 확인했다. 최종 음량 믹스와 주관적 청감 승인은 별도다.
