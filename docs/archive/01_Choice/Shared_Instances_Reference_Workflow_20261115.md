# 선택 주제
주제 : 공유 입력·UI·카메라의 Instance 접근과 원작 코드 기반 구현 절차
- 2026-11-15 사용자는 PlayerInputController를 Player 프리팹에서 분리하고 static Instance로 제공하도록 요청했다. GameCamera가 Unity Camera를 보유하고 Instance를 통해 외부의 추적 대상 요청을 받으며, PlayerSpawner가 UI·카메라·입력을 직접 조회하도록 명시했다.
- 사용자에게 필요한 객체를 매번 누군가 전달하는 구조를 줄이고, Sephiria를 기반으로 Dungreed를 참고하여 게임 구조를 구성하려는 목적이다.

# 선택 사안 1
- 선택 : 사용자 확정 — PlayerInputController·UIController·GameCamera의 static Instance를 제공하고 PlayerSpawner.Bind()가 이를 직접 조회한다. 기존 CameraController는 GameCamera로 이름을 바꾸며 스크립트 GUID를 유지한다.
- 이유 : 공유 객체를 사용하는 외부 코드가 많으며 매번 주입하는 형태를 원하지 않음. Player 제거·복제로 입력 객체의 수명이 영향을 받는 문제를 방지하려는 요청임.
- 적용 : Sephiria의 독립 MainWorld 배치를 참고해 씬 수명으로 구현했다. Awake는 자체 등록과 자기 컴포넌트 준비, 씬 로드 뒤 Stage의 준비 확인을 거쳐 대상 연결, Title 씬 전환 시 정리한다. 앱 수명 유지와 범용 Singleton 레지스트리는 필요하지 않아 도입하지 않는다.
- 제시된 다른 선택 사안들
  - 이전 구조는 Player 프리팹에 입력을 붙이고 StageManager가 HUD·카메라를 생성하여 Spawner에 전달했다. 이번 사용자 요청으로 대체한다.
  - Dungreed의 카메라는 DontDestroyOnLoad를 사용하지만 이번 게임 카메라를 앱 전체 수명으로 유지하라는 요구는 없으므로 그대로 옮기지 않는다.

# 선택 사안 2
- 선택 : 사용자 확정 — 구현할 클래스와 그 기능을 특정한 뒤, 대응표에 기록된 Sephiria·필요한 Dungreed 코드를 실제로 읽고 구조·구현 방식을 현재 규모와 목적에 맞게 변형한다.
- 이유 : 원작을 근거로 게임 구조를 구성하고 불필요한 참조 전달을 줄이려는 사용자의 개발 방식임. Instance·MonoBehaviour의 초기화 순서와 준비 전 조회 검사를 특히 요구했다.
- 현재 절차 원본 : [AGENTS](../../../AGENTS.md)의 레퍼런스 코드 활용, [DESIGN 2.4](../../agents/DESIGN.md#24-레퍼런스-기반-구현과-초기화-검토).
- 대응 코드 원본 : [REFERENCE_CODE_MAP](../../REFERENCE_CODE_MAP.md). 현재 구현·수명·검증·시퀀스는 [GAME_SPEC](../../GAME_SPEC.md)를 따른다.

# 작업 중 확인
- 사용자는 열린 Unity MainWorld 씬의 미저장 변경을 버려도 된다고 답했다. 해당 씬의 미저장 변경만 폐기한 뒤 저장된 씬을 기준으로 연결 작업을 진행했다. 다른 사용자 파일·아트 편집의 폐기 승인은 아니다.

# 선택 사안 3 — 고정 수명 참조의 초기화 순서와 재사용
- 선택: 2026-11-15 후속 요청으로 한 번 생성해 유지할 객체는 생성 우선순위·참조 시점을 명시하고 Awake/Start 또는 명시적 완료 순서로 일회 확보한다. PlayerInputController의 매 프레임 Instance 재확보를 제거하고 다른 반복 호출부도 검사한다.
- 이유: 초기화 순서를 정하지 못한 탓에 유지되는 객체를 반복 조회하는 불필요한 처리를 피하려는 사용자 요청.
- 적용: MainWorld 서비스는 Awake에서 자기 등록, PIC·HUD는 Start에서 관리자·카메라 참조 확보, Stage는 다음 프레임 이후 Player 생성·연결. Title GUIStyle은 GUI.skin을 사용할 수 있는 첫 OnGUI에서 생성·재사용하고 로딩 문구는 고정 네 문자열을 재사용한다.
- 경계: static 참조 읽기 자체는 새 객체 생성이 아니다. 상태값·슬롯 재선택·씬 재입장과 일회 생성/해제 조회를 구분하며 측정하지 않은 성능 향상을 수치로 주장하지 않는다. 원작 Sephiria PIC.Update도 카메라 Instance를 반복 조회하므로 이번 캐시는 사용자 요구에 맞춘 변형이다.
- 기준 원본: AGENTS의 레퍼런스·Unity 코드 작업과 DESIGN 2.4. 실제 검사 결과와 초기화 흐름은 GAME_SPEC의 해당 Case를 따른다.

# 선택 사안 4 — Player 복제 시 자동 초기화와 몸체의 이동 적용
- 선택: 2026-11-15 후속 요청으로 PlayerSpawner.playerInit(SaveData)를 제거하고 Awake에서 저장 체력·기본 상태·최초 체크포인트 배치를 처리한다. playerSpawn은 인자 없이 MapManager.Instance의 배치 목록과 현재 저장 CheckpointId를 사용한다.
- 이유: Player 프리팹 복제 순간 최초 초기화가 자동 실행되고 외부 호출자가 SaveData·Transform을 전달할 필요를 줄이려는 요청.
- 순서: 선택 SaveData 준비 → Map 복제·Awake Instance 등록 → Map.Initialize 검증 → Boss·Player 복제 → PlayerSpawner.Awake → 서비스 Bind. Map을 검증하기 전에 Player를 복제하지 않는다.
- 향후 메모: 체크포인트 상호작용 구현 시 그때마다 로컬 값을 갱신하는 방식으로 최적화하라는 사용자 요청을 playerSpawn 조회부 주석에 기록한다. 이번 작업에 체크포인트 상호작용 기능을 추가한 것은 아니다.
- 선택: PlayerCharacter는 계산한 이동량을 RequestMove로 전달하고 PlayerBodyController가 누적 요청을 자체 FixedUpdate에서 소비·충돌 보정·적용한다. 실제 이동량은 Character로 반환하지 않고 Body가 모션에 전달한다.
- 이유: 이동 의도·수치 계산과 충돌·위치 적용의 역할을 더 분명히 나누려는 사용자 판단. 제공한 Sephiria 흐름을 로컬 UnitAvatar·TopdownRigidbody 코드와 대조한다.
- 기준 원본: 현재 API·실행 순서·원작 적용 차이·검사는 GAME_SPEC P03·P04·P09·S02·S03과 해당 시퀀스를 따른다.
