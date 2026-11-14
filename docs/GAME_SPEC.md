# Project Chrono — 통합 기획·클래스 정의

최초 정리: 2026-11-14. 답변 반영: **2026-11-15 (Asia/Seoul)**. 현재 단계는 **문서 통합·사양 방향 정리 완료**이며 구현은 미착수이다. Q1~Q29 답변을 반영했다. 답변 대기는 없으며 실제 구현·연결·검증은 다음 단계다.

사용자 후속 확정: **문서 작업이 완전히 끝나고 Sprite·FX·animation 등의 연출을 제외한 로직의 방향이 확실해진 뒤 Unity 개발을 시작한다.** 따라서 지금은 프로젝트·코드를 생성하지 않는다. 2장은 케이스 누락 확인용 목록이며, 실제 API 호출 흐름은 1장 확정 후 해당 케이스 구현과 함께 작성한다.

현재 사양 원본은 이 문서이며 [Notion 정리 페이지](https://app.notion.com/p/41857a6d6d24809a8d15f1a5aa34d48a)는 같은 내용을 검토하기 위한 게시본이다. 질문·답변은 [QUESTIONS.md](QUESTIONS.md)에 관리한다. Notion과 로컬 파일의 자동 동기화는 없으며, 후속 답변을 반영할 때 함께 갱신한다.

### 읽는 방법과 출처

- **사용자 확정**: 이번 요청·후속 답변·현재 AGENTS에서 명확히 정한 내용.
- **원문**: Notion 메모를 보존한 내용. 모순이 없더라도 이번 제출 사양으로 모두 승인된 것은 아님.
- **보완 정의**: Q3에서 역할 배치 A를 채택했다. Q4에 따라 기존 확정명을 제외한 이름은 AI가 작성하고 사용자가 기능별 결과를 보고 변경할 수 있다. 새 게임 규칙을 대신 결정하지 않는다.
- **미정**: 출처 간 충돌 또는 필수 정보 누락. 관련 Q 번호로 답변한다.
- 현행 정의에는 이름을 하나로 적는다. Q2 확정명은 유지하고 신규 API는 Try보다 Apply/Handle/Get/Set처럼 역할이 분명한 이름을 쓴다. 질문지는 확정 답변·반영 위치로 압축하고 주요 대안·이유는 3장과 선택 기록에 보존한다. Awake/Update는 외부 API와 구별한다.
- Q4 확정: 공개 데이터는 단순한 public 필드, 전역 시스템 창구는 필요한 static으로 작성한다. 불필요한 readonly·sealed·방어용 래퍼를 추가하지 않는다. 프리팹마다 다른 상태까지 모두 static으로 공유하는 뜻은 아니다. 파일 오류·잘못된 데이터 처리와 행동 가능 판정은 유지한다.

| 출처 ID | 원문 | 이번 정리에 사용한 범위 |
| --- | --- | --- |
| N0 | [Project Chrono](<notes/notion 문서/Project Chrono 3f657a6d6d2480e8b3d7f3695c2a618d.md>) | 하위 문서 연결 |
| N1 | [Class 구조](<notes/notion 문서/Project Chrono/Class 구조 41357a6d6d24802182cde4fe53b5364f.md>) | 클래스 전체, Sequence D 전체, 비고 1·2 |
| N2 | [필요 기능](<notes/notion 문서/Project Chrono/필요 기능 40d57a6d6d248070aadddcc4a7e6dcc8.md>) | 시스템·플레이어 Case 전체, 입력, Mode/State/Condition, 적·시스템 |
| N3 | [적 컨셉](<notes/notion 문서/Project Chrono/적 컨셉 40257a6d6d248023a8a7f063107b6b92.md>) | 6개 보스 및 모든 패턴 |
| N4 | [디자인들](<notes/notion 문서/Project Chrono/디자인들 41857a6d6d2480f2a7bfdf1eaac3d398.md>) | Title, In Game, Map, Ending 및 원본 이미지 링크 |
| L1 | [핵심 기획 확정](notes/01_핵심_기획_확정.md) | 기존 규칙·튜토리얼·태엽 보스와의 충돌 |
| L2 | [게임계획서](notes/게임계획서.md) | 캡스톤/PlayX4 범위, 시간 테마·회피 자원 |

기존 02·03 기획 문서는 대부분 빈 양식이므로 구현·검증·범위 동결의 증거로 사용하지 않았다. 원문 파일과 이미지들은 수정하지 않는다.

### 제출 범위와 컨셉

- **사용자 확정(Q1·Q24)**: 게임 입장, 체크포인트와 Trigger, **경비 로봇 1명과 원문 5개 패턴 전체**, Ending의 짧은 흐름을 먼저 완성한다. 마지막 보스가 쓰러지는 순간 완료 판정, 사망 컷씬, 스탭롤, Title 복귀가 이어진다.
- **이후 순서**: 시간이 남으면 적을 하나씩 추가하고, 그 뒤 더 여유가 있으면 dialogue를 추가한다. 나머지 5보스·33패턴은 원문 목록으로 보존하며 현재 최소 제출 구현에 포함하지 않는다.
- **후속 튜토리얼**: 전투구역으로 가며 이동·공격·회피를 안내, 경비 로봇 전투 시작에 Draw & Slash, 전투 종료 후 체크포인트를 안내한다. 지금 dialogue 시스템을 먼저 만들지 않는다.
- 서버 없는 싱글 플레이, 하나의 고정 무기와 정해진 Draw/Slash·대시·각성 능력. 무기 교체·능력 획득/선택/교체는 추가하지 않는다.
- N3 연구소·마도공학수정 컨셉을 원문으로 보존한다. 과거 시간 소녀·자유 보스 선택과의 세계관 통합을 이번 짧은 흐름에 필수인 신규 과제로 만들지 않는다.
- 커서 기준 몸체·이동 모션 **왼앞·오앞·왼뒤·오뒤의 4방향** 우선(2026-11-15 Player 시안 후속 요청), 8방향 표현은 2차 목표. 실제 이동은 WASD 8방향·대각 속도 정규화. Player 이동/대시는 벽 통과 불가, Slash는 벽과 무관하게 Line 박스 영역 판정.
- 저장·Title UI 초기화는 UniTask, 나머지는 Unity 코루틴. 오디오는 FMOD, 카메라는 자체 CameraController. 사용자 조건과 호환 조사에 따라 **Unity 6.3 LTS 계열** 선택(3.4).
- DebugController는 Editor에서만 개입한다. 제출 소스에는 포함하고 디버그 제어 코드의 제작 효율 향상에 AI를 사용했음을 기록한다. 플레이어 빌드에서 디버그 입력을 활성화한다는 뜻은 아니다.
- Windows 10/11·키보드/마우스, 1920×1080 목표 60FPS는 기존 원문 기준으로 유지한다. 새 플랫폼 변경 요청은 없음. 과거 11/27·30일 일정은 현재 마감으로 사용하지 않는다.

### 프로젝트 완성 목표 — 콘텐츠·연출 추가에 집중할 수 있는 기반

**사용자 확정(2026-11-15)**: 세부적인 시스템 조정을 제외하면, 이후 작업을 **Boss 콘텐츠 추가와 FX·VFX 등 연출 추가에 집중할 수 있는 환경**으로 만드는 것을 현재 프로젝트의 완성 목표에 포함한다.

- 공통 전투·피격·Draw/Slash·버프·조우·사망/재도전·저장·UI 연결이 실제로 동작해야 한다. 경비 로봇 5패턴과 입장~Ending 흐름으로 이 기반을 확인한다.
- 새 Boss는 B01~B08의 보스/패턴/투사체 구성과 S02·S03의 생성·배치 연결을 통해 추가할 수 있어야 한다. 보스마다 공통 세이브·입력·재도전 시스템을 다시 만드는 작업이 반복되지 않도록 한다.
- FX·VFX·animation·음원은 U01·U02·U06~U08의 행동·타격·완료/중단 신호에 연결하고, 수치·프레임·피벗·타이밍 같은 세부 조정을 계속할 수 있어야 한다.
- 추가할 코드·데이터·프리팹·자산의 위치와 연결 방법을 실제 구현 결과와 함께 남긴다. 이름만 있는 API나 빈 함수, 미검증 연결을 기반 완성으로 보지 않는다.

현재는 **목표·정의만 정리된 상태**이며 기반 구현은 미착수다. 현재 경비 로봇 1명 우선 범위는 유지한다. 범용 에디터나 대규모 콘텐츠 프레임워크를 새 필수 기능으로 추가하는 뜻은 아니다. 이 기반이 동작한 뒤 3.1.1의 성능 비교로 선택 이유를 실측 근거까지 보강한다.

## 1. 클래스·구조체·enum 정의

카테고리 표시 순서는 **Player / Boss / UI·anim / 시스템·기타**이다. Q2 명칭·Q3 책임 배치를 채택했고 Q4는 신규 이름을 AI 작성에 맡겼다. 이름을 하나씩 선택받기 위해 멈추지 않는다. 실제 케이스 구현은 문서 확정 뒤 시작한다.

### 1.1 Player

#### P01. Character — 원문, MonoBehaviour

- **역할(목적)**: PlayerCharacter와 BossCharacter의 부모. 공통 Status·상태·함수·이벤트를 제공한다. 원문은 Character 하나로 조회하기 쉽고 확정 전에 한 곳에서 검토하기 위해 수치를 모아 둔다.
- **생명 주기**: 세션 시작에 StageManager가 생성한 Player/Boss 프리팹에 존재한다. 사망은 Destroy가 아니며 Player 부활·Boss 재도전 초기화에서 같은 인스턴스의 위치·수치를 복원한다. 처치된 Boss는 사망 상태를 유지한다. Title 복귀/앱 종료 때 인스턴스를 제거한다. UI·Input·Component 연결은 사망으로 끊지 않고, 전투 상대의 OnDie 상호 구독만 어느 한쪽 사망 때 해제한다(Q5).
- **보유 공개 API**: `ApplyDamage(DamageInstance)` — IsDodge/IsInvulnerable 등 피해 허용 여부 확인 후 적용하고 적용 여부 반환; `ApplyBuff`, `RemoveBuff` — 자기 CharacterBuff에 효과 요청. `OnDie`, `OnDamaged`, `OnCutScene`, `OnDash` — 관련 사건 알림; `OnRevive`는 Player 부활 알림. 비전투 중에도 유효한 피해를 받을 수 있다(Q11).
- **보유 공개 변수**: 아래 원문 수치·상태를 public으로 둔다. 데이터 원본은 한 곳에 두고 각 Controller가 갱신한다(Q4).
- **연관 class(상속, 보유)**: MonoBehaviour 상속; PlayerCharacter·BossCharacter가 상속한다. 각 프리팹의 CharacterBuff·CharacterRendering·Hitbox와 연결한다. DamageInstance로 피해를 받고 UI·Spawner는 프리팹 수명 동안 이벤트를 연결한다.

| 원문 변수 | 역할·용도 |
| --- | --- |
| `HP`, `maxHP` | 현재 체력 / 초기화·회복 상한 |
| `AD`, `basicAD` | 매 공격에 조회하는 현재 공격력 / 초기 공격력 |
| `AS`, `basicAS` | 현재 공격 애니메이션 속도·공격 사이 간격 계산 / 초기 공격속도 |
| `SP`, `basicSP` | 현재 이동속도 / 초기 이동속도 |
| `lineLength`, `lineLifeTime` | 고정 Line 길이 / 확정 후 유지시간 |
| `dashLength`, `dashDodgeTime`, `dashNoInputTime`, `dashCoolTime` | 이동거리 / 피격 무시 / 입력 차단 / 재사용 제한 시간, 각각 별개 |
| `attackNoInputTime`, `attackCoolTime`, `attackComboTime`, `attackSP` | 느린 이동 외 입력 차단 / 다음 공격 제한 / 콤보 유지 / 공격 중 이동속도 |
| `drawNormalCoolTime`, `drawCancelCoolTime`, `slashAfterDrawCoolTime` | Slash 후 긴 Draw 대기 / 취소·확정·자연 만료 후 짧은 Draw 대기 / 확정 직후 짧은 Slash 대기(Q8) |
| `attackComboCount` | 0·1·2로 3회 콤보 관리. 원문은 Character에 두지만 AttackController가 관리한다는 내용도 있음 |
| `groggyStagger` | Player가 모으는 수치. 최대100 고정, 100 도달 즉시 현재 Boss 그로기·게이지0 소모(Q12·Q25) |
| `IsDead` | 사망 상태 |
| `IsDodge` | Hitbox 유래 피해 무시. 다른 효과로 감소하는 체력까지 무조건 무시하지 않음 |
| `IsInvulnerable` | 모든 피해 무시 |
| `IsDash`, `IsAttack`, `IsAttackCombo` | 대시 입력 제한 / 공격 행동 / 콤보 유지 여부 |
| `IsCutScene`, `IsFever` | 컷씬 입력 제한·연출 상태 / 각성 상태 |

Q2에서 drawCancelCoolTime으로 확정했다. HP/AD/AS/SP는 유지한다. 그로기 상한은 100이며 도달 즉시 현재 Boss에 그로기를 적용하고 0으로 소모한다(Q25). Dodge는 Hitbox 피해, Invulnerable은 효과 포함 모든 피해를 차단한다.

#### P02. PlayerInputController — 원문, MonoBehaviour

- **역할**: 물리 입력을 읽고 사용자 의도를 Player에 전달한다. 이동·일반 공격은 누르는 동안 반복된다. 커서를 `Camera.ScreenToWorldPoint`로 월드 좌표화하고 Draw 전용 입력 및 상호작용 우선순위를 처리한다. Player의 실제 행동 가능 여부와 행동 계산은 Player 측 책임이라는 원문을 보존한다.
- **생명 주기**: 세션 진입 시 하나 준비한다. StageManager 생성 후 PlayerSpawner가 SetPlayer로 연결하며 Awake에서 아직 없는 Player를 가정하지 않는다. Player 사망·부활에도 연결 유지, Title 복귀/앱 종료 때 ClearPlayer로 해제한다.
- **보유 공개 API**: `SetPlayer`, `ClearPlayer` — 세션 대상 연결/해제; `SetInteractionTarget` — 수정 후보 지정/제거; `SetDrawInput` — Draw 입력 모드 설정; `SetBlockInput` — 전역 입력 차단 반영. 입력 콜백 `OnMove`, `OnAttack`, `OnDash`, `OnDraw`, `OnSlash`, `OnConfirmLine`, `OnResetLine`, `OnPlacePoint`, `OnInteract`, `OnSettings`, `OnDebug`는 각 의도를 전달한다.
- **보유 공개 변수**: `BlockInput` — 컷씬·로딩 등 전역 입력 차단; `aimInput` — 화면 커서 입력; `MoveInput`, `AimWorldPosition` — 전달할 이동·조준 의도; `InteractionTarget` — 현재 수정 후보. 캐릭터 내부의 행동 가능 여부는 Player가 결정한다.
- **연관 class**: Input이 PlayerCharacter를 참조하고 Player는 Input을 모른다. GameManager/CutsceneController 전역 이벤트를 구독한다. CrystalCheckPoint가 후보를 전달하고 UIController가 안내한다. DebugController 호출은 Editor에 한정한다(Q6·Q23).

#### P03. PlayerCharacter — 원문, Character 상속

- **역할**: 단 하나의 Player를 대표하고 입력받은 의도로 이번 프레임 행동을 계산·확정한다. 구성 기능을 소유하고 호출한다. 무기·스킬 선택 시스템은 만들지 않는다.
- **생명 주기**: 세션 시작에 StageManager가 1회 복제하고 PlayerSpawner가 연결·초기화한다. 첫 로드는 저장 HP와 마지막 수정 위치, 사망 부활은 같은 인스턴스에 maxHP·기본 수치·체크포인트 위치를 적용한다. Title/앱 종료 전까지 유지한다(Q5·Q14).
- **보유 공개 API**: `SetMoveInput`, `SetAimPosition` — 입력 의도 갱신; `ApplyAttack`, `ApplyDash`, `ApplyDraw`, `ApplySlash` — 내부 상태·쿨타임 확인 후 행동; `Interact` — 현재 수정 활성화; `SetCombat` — 전투 여부; `ResetState` — 부활 상태 초기화. Apply 명칭이 조건 없는 강제 실행을 뜻하지는 않는다.
- **보유 공개 변수**: P01 수치·상태와 `MoveDirection`, `AimDirection`, `IsInCombat`, `IsDrawing`, `IsSlashing`. 준비된 이벤트로 상태 변화를 알리고 명시적 FSM 프레임워크는 도입하지 않는다(Q6).
- **연관 class**: SkillController·AttackController·PlayerBodyController·CharacterRendering·PlayerSpawner·CharacterBuff를 보유한다. InputController 역참조는 없다.

#### P04. PlayerSpawner — 원문, MonoBehaviour

- **역할**: 이미 생성된 Player의 최초 연결, 수치·위치 초기화, 사망 후 재배치·부활을 담당한다. 프리팹 최초 복제는 StageManager 책임이다.
- **생명 주기**: Player 프리팹 Component로 함께 생성·제거된다. 최초 Bind는 1회이며 반복 부활에서 Input/UI 연결을 다시 끊거나 중복 구독하지 않는다. 사망 때 효과 해제, 부활 단계에서 기본 수치 복귀를 구분한다.
- **보유 공개 API**: 원문 `playerSpawn()` — 체크포인트 위치 배치; `playerInit()` — 첫 로드/부활에 맞는 수치 초기화; `Bind` — 최초 Component·Input·UI 연결; `Unbind` — 세션 종료 해제.
- **보유 공개 변수**: `Player` — 초기화 대상; `SpawnPoint` — 이번 배치 위치. 저장된 CheckpointId로 MapManager에서 위치를 구한다.
- **연관 class**: PlayerCharacter·그 Component들을 참조; SaveManager에서 저장 상태 조회; MapManager에서 체크포인트 위치 조회; GameManager가 부활 흐름 조정; BossSpawner 초기화와 연결.

#### P05. AttackController — 명칭 확정(원문 WeaponController)

- **역할**: 고정 무기 Sprite 회전, 공격 시점 커서 방향 고정, 3회 콤보와 유지시간, 공격 animation·FX 요청, 정해진 프레임의 판정 및 DamageInstance 전달. 같은 공격에서 같은 대상의 중복 적중을 막는다.
- **생명 주기**: Player Component로 유지한다. 공격 시작마다 적중 기록을 비우고 종료/사망 때 판정을 닫는다. 느린 이동 중에도 콤보는 유지시간까지 보존하며 시간 만료·강제 종료 때 초기화한다.
- **보유 공개 API**: `ApplyAttack` — 3회 콤보 공격 시작; `UpdateAim` — 무기 방향; `OpenHitWindow`, `CloseHitWindow` — 사각 판정 시작/종료; `ResetCombo`, `CancelAttack` — 초기화·사망 강제 종료. 공격 중 느린 이동만 허용하고 대시 취소는 허용하지 않는다. 피격은 별도 경직을 만들지 않는다(Q10).
- **보유 공개 변수**: `ComboIndex`, `ComboRemaining`, `AttackCooldownRemaining`, `IsAttacking` — 진행 조회. 공격력·공격속도 원본은 P01이다. 실제 타격 박스 크기·오프셋은 표시와 판정이 같도록 설정한다.
- **연관 class**: PlayerCharacter 보유; CharacterRendering 모션 요청; CollisionBox가 NonAlloc 사각 Cast로 Hitbox를 조회해 DamageInstance 전달. 동일 수신 대상은 1타 1회. 콤보 1·2 FX 반전 재사용, 콤보3 X형 FX 원문 보존.

#### P06. SkillController — 명칭 확정, MonoBehaviour

- **역할**: Draw·Slash 가능 여부와 쿨타임, 확정 `Line? CurrentLine`을 보유한다. LineDrawer가 확정할 때만 값을 교체하고 LineSlasher는 이 값을 조회한다.
- **생명 주기**: Player Component로 생성, Awake에서 C# helper 둘을 준비한다. 재Draw 취소는 기존 확정 Line을 바꾸지 않는다. LineDrawer가 만료 시 CurrentLine을 null로 바꾼다. 사망 시 Line·임시 점·쿨타임을 정리하고 부활 시 기본 상태로 시작한다.
- **보유 공개 API**: `ApplyDraw`, `CancelDraw`, `PlacePoint`, `ResetPoints`, `ConfirmLine`, `ApplySlash` — 입력별 행동; `SetLine(Line?)` — 확정/만료 값 전달; `ResetSkills` — 사망·부활 초기화; `ApplyDrawCooldown` — 정상/각성용 대기시간 적용. 편집 취소에서 SetLine(null)을 호출하지 않는다.
- **보유 공개 변수**: `CurrentLine`(Line?), `HasLine`, `CanDraw`, `CanSlash`, `DrawCooldownRemaining`, `SlashCooldownRemaining` — 데이터와 HUD 상태. Fever는 별도의 Draw 쿨타임 값(기본 정상값의 절반)을 적용하며 재획득마다 반복 반감하지 않는다.
- **연관 class**: PlayerCharacter 소유; LineDrawer·LineSlasher·Line 보유; UIController·CutsceneController·CharacterBuff와 연결. SkillManager는 이 클래스의 과거 별칭이다.

#### P07. LineDrawer — 일반 C# helper

- **역할**: Draw 임시 점·미리보기·확정, 확정 Line 수명·만료 예고를 관리한다. Shift 시작/취소, M1 두 점, R 초기화, F 확정. 동일 점 두 개는 무효, 두 번째 점은 방향이고 실제 길이는 고정한다(Q8).
- **생명 주기**: SkillController가 생성하는 일반 C# 객체이다. Draw 진입 때 임시 점만 초기화, 확정 때 저장 Line 교체. Draw 종료 후 수명 계산을 계속하되 Draw/Slash 중 게임 시간 정지에는 멈춘다. 수명 종료 시 저장 Line=null, 소유자 제거 때 종료한다.
- **보유 공개 API**: `Begin`, `PlacePoint`, `UpdatePreview`, `ResetPoints`, `Confirm`, `Cancel`, `TickLineLifetime` — 점 편집·확정·수명 관리. 취소/확정/자연 만료 뒤 짧은 Draw 대기, Slash 후 긴 대기, 확정 직후 짧은 Slash 대기를 적용한다.
- **보유 공개 변수**: `StartPoint`, `EndPoint`, `PointCount`, `CanConfirm`, `LineRemaining`. 화면을 크게 벗어나지 않는 입력을 전제로 별도 맵 경계 보정은 만들지 않는다.
- **연관 class**: SkillController 소유, Line 반환, UIController·CameraController·CutsceneController 연출 요청. Line 시각 객체 자체는 연출 규격 확정 후 정의한다.

#### P08. LineSlasher — 일반 C# helper

- **역할**: 확정 Line 사각 영역을 1프레임 조회해 타격·탄 제거/반사·각성 보상을 요청한다. 벽 너머도 같은 Line 영역으로 판정한다. 여러 부위의 동시 적중 성공 여부는 Boss가 판단한다.
- **생명 주기**: SkillController가 생성하는 일반 C# 객체. Slash 시작에 게임 진행 정지·전면 무적·입력 제한을 요청하고 종료/중단 때 복원한다. 유효 Slash가 해당 Line을 사용하면 CurrentLine을 null로 바꾸고 Slash를 비활성화한다(N1 391~392행, N2 52·56행). Q7은 재Draw 진입/취소 중 보존 규칙이며 사용 후 소모를 없애지 않는다. 해당 공격의 적중 묶음은 공격 처리 동안만 유지한다.
- **보유 공개 API**: `Execute` — 확정 Line으로 Slash 시작; `ApplyHits` — 사각 NonAlloc Cast 적중 처리; `Complete`, `Cancel` — 완료/강제 종료 및 상태 복원.
- **보유 공개 변수**: `IsRunning`, `AttackId`, `ActiveLine` — 실행 상태·동시 타격 식별·이번 사용 선. 금색 반사체는 Boss 쪽으로 빠르게 이동하는 연출과 Fever 보상을 사용한다.
- **연관 class**: SkillController·Line을 참조하고 CollisionBox/Hitbox/DamageInstance로 대상에 전달한다. Projectile은 자기 반응을 처리하고 BossStructure/BossCharacter는 같은 AttackId의 부위 결과를 모은다. CharacterBuff·CameraController·CutsceneController에 효과를 요청한다.

#### P09. PlayerBodyController — 명칭 확정(원문 CharacterBody)

- **역할**: 이동·대시·몸체 방향과 충돌을 처리한다. 커서 기준 4방향 몸체/이동 모션, WASD 이동은 대각 정규화. 이동/대시는 벽 및 Boss 몸체에 막혀 내부로 진입할 수 없다. 몸 접촉 피해는 없다(Q28).
- **생명 주기**: Player 프리팹 Component로 유지하고 부활 때 위치·진행 중 대시·타이머를 초기화한다. Draw/Slash 게임 시간 정지 동안 이동 및 행동 타이머를 멈춘다.
- **보유 공개 API**: `Move`, `SetFacing`, `ApplyDash`, `Stop`, `ResetMotion` — 이동·바라보기·대시·중단·초기화. 대시는 이동 의도 방향 우선, 없으면 커서 방향. 이동/조준 길이가 0이면 마지막 유효 방향을 사용한다.
- **보유 공개 변수**: `MoveDirection`, `FacingDirection`, `DashDirection`, `DashRemaining`, `DodgeRemaining`, `InputBlockRemaining`, `DashCooldownRemaining` — 방향·독립 시간. 대시의 이동/보호/입력차단 시간을 하나로 합치지 않는다.
- **연관 class**: PlayerCharacter 보유; Hitbox 판정 정보를 Character 피해 API로 전달; CharacterRendering에 이동·대시·피격 표시 요청.

#### P10. Line — 원문의 struct, 저장 필드는 Line?

- **역할**: 확정 경로의 기하 정보. 편집 중 임시 점과 확정된 경로를 구분한다.
- **생명 주기**: Draw 확정 때 값 생성, SkillController.CurrentLine에 nullable 값으로 보유한다. 새 확정 때 교체, Slash 사용·만료·사망 초기화 때 null. 편집 취소 때 기존 값 유지. MonoBehaviour/Destroy 대상이 아니다.
- **보유 공개 API**: 생성자 — 확정 시작점·방향·길이로 값 구성; `GetEndPoint`, `GetBounds` — 계산된 끝점·판정 영역 조회.
- **보유 공개 변수**: `StartPoint`, `Direction`, `Length`, `Width` — 확정 기하 데이터. `EndPoint`는 계산 결과이며 두 번째 입력 점까지의 거리로 Length를 바꾸지 않는다.
- **연관 class**: LineDrawer 생성, SkillController 보유, LineSlasher 소비. 만료 타이머는 원문상 LineDrawer 책임이며 Line에 중복 배치하지 않는다.

### 1.2 Boss

#### B01. BossCharacter — 원문, Character 상속

- **역할**: Boss의 공통 전투·행동 실행 창구. Idle에서는 패턴 목록을 직접 알지 않고 매 프레임 BossPatternSelector에 요청한다. 후보가 없으면 Idle 유지. 100 게이지 도달 즉시 그로기, 현재 패턴 중단. 기존 필드 투사체·장판은 유지한다(Q25·Q27·Q28).
- **생명 주기**: StageManager가 프리팹을 복제, BossSpawner가 기본 수치·위치·Trigger를 준비한다. Player 사망 시 같은 Boss의 위치·수치를 초기화한다. 양쪽 OnDie 상호 구독은 어느 한쪽 사망 때 해제하고 재전투 시 연결한다. 처치 Boss는 우선 전체 프리팹의 사망 표시 B안, 사체 전용 A안과 비교는 보류.
- **보유 공개 API**: `BeginEncounter`, `EndEncounter` — 전투와 상호 구독 시작/종료; `EnterGroggy` — 현재 행동 중단·그로기; `ApplyPattern` — 선택된 패턴 시작; `CancelPattern` — 현재 패턴 중단; `HandleSlashHit` — 같은 AttackId의 부위 결과 수집·성공 판단. 피해 API는 Character 상속.
- **보유 공개 변수**: `BossId`, `BossName`, `IsInCombat`, `IsGroggy`, `CurrentPattern`, `BasePosition`, `PatternSelector`. 패턴 목록은 Selector가 보유한다. 몸체 Collider는 Player 진입을 막으며 접촉 자체의 피해는 없다.
- **연관 class**: Boss_01~06이 상속; BossSpawner·BossPatternSelector·CharacterBuff 보유. PlayerCharacter와 OnDie 직접 상호 구독. EncounterTrigger·MapManager·SaveManager·UIController와 연결한다.

#### B02. BossSpawner — Boss 프리팹 Component

- **역할**: Boss 최초 초기화, Player 사망 후 수치·위치 재설정, 전투 진입 Trigger 복구. PlayerSpawner와 유사하다는 원문을 유지한다.
- **생명 주기**: Boss 프리팹 Component로 함께 유지한다. 최초 연결과 재도전 ResetEncounter를 구분한다. Player 사망 시 Boss 기본값·위치·Trigger를 복원하며 프리팹을 새로 복제하지 않는다. 세션 종료 때 연결 해제. 그로기만으로 기존 투사체·장판을 제거하지 않는다.
- **보유 공개 API**: `Initialize` — 최초 연결·수치 준비; `ResetEncounter` — 재도전 수치·위치·Trigger 복원; `PlaceAt` — 위치 적용; `Release` — 세션 종료 구독·전투 생성물 정리.
- **보유 공개 변수**: `Boss`, `EncounterTrigger`, `BasePosition` — 대상과 배치 정보. MapManager가 위치 원본을 가진다.
- **연관 class**: BossCharacter 보유, StageManager 생성 후 호출, MapManager 위치 조회, EncounterTrigger 생성/재활성화. Player 사망과 SaveManager 진행도에 의존.

#### B03. Boss_01…Boss_06 — Boss_01 우선, 나머지 후속 범위

- **역할**: Boss별 수치·패턴·기믹을 정의한다. N3 순서대로 Boss_01~06 이름을 사용한다(Q4). 현재 제출 대상은 경비 로봇 Boss_01과 아래 5패턴 전체(Q24), 나머지는 후속 확장 원문 목록이다.
- **생명 주기**: BossCharacter/BossSpawner의 수명을 따른다. 패턴 전용 투사체·구조물은 패턴 실행에 맞춰 준비한다. 그로기 때 현재 패턴만 중단하고 이미 나온 위험물은 자체 수명까지 유지한다. 사망·재도전 정리는 그로기와 분리한다.
- **보유 공개 API**: 공통 API 상속; `InitializePatterns` — Selector에 해당 Boss 패턴 등록; `HandleSlashHit` — 동시 부위·기믹 성공 여부 확인. Player가 성공 조건을 대신 판단하지 않는다.
- **보유 공개 변수**: 상속 수치 외 `Phase`, `PatternCooldown` 등 필요한 값만. 군용 로봇·뱀의 50% 페이즈 기준은 원문. 정확한 HP·AD·패턴 시간·가중치는 아직 값이 없으며 설정 데이터로 둘 수 있지만 승인된 밸런스 값은 아니다.
- **연관 class**: BossCharacter 상속, BossPattern·Projectile·BossStructure·CollisionBox·CharacterRendering 연결.

| 타입·콘텐츠 범위 | 원문 역할·특징 | 보존해야 할 패턴(원문 번호) |
| --- | --- | --- |
| `Boss_01` 경비 로봇 | 상체·분리 손, 문을 막는 튜토리얼 보스 | 1 복부 좌우 직선 탄환; 2 추적 손 낙하; 3 중앙 내려침·양쪽 탄환; 4 추적 눈 레이저; 5 좌우 교대 부채꼴 탄환 |
| `Boss_02` 거대 군용 로봇 | 공간에서 미사일을 꺼냄, 반사 소개. HP 50% 이하 광폭화·3/4/5 가속·6 추가 | 1 점프 착지 범위 피해; 2 접근 후 소형 미사일 폭격; 3 유도 미사일 5개·Slash 제거/시간 만료; 4 남는 역장 미사일·이동 제약; 5 직선 고속 미사일·경고; 6 상단 차원문 추적 고속 미사일·비프음 |
| `Boss_03` 뱀 | 평소 거리를 두고 배회, 머리+꼬리 동시 Slash 추가 피해. HP 50% 이하 이동 증가·5 추가 | 1 머리 추적 돌진·공격 성공/시간 경과 종료; 2 잠복 후 이전 위치에서 현재 방향으로 도약; 3 낙하 마디 정전기·먹어서 회복·먹는 중 Slash 마디 제거+그로기; 4 회전 방사 탄환; 5 마디 연속 낙하·마지막 탄환 |
| `Boss_04` 꽃 봉오리 | 중앙 몸체+4촉수, 접촉 피해·Dodge 통과 | 1 전조 후 90도 회전; 2 직선 5발; 3 직선 레이저; 4 세 방향 방패+지면 전류; 5 에너지 구 반사 적중 그로기; 6 하늘 구 낙하 폭발; 7 촉수 4개 순차 융기 2세트·각각 게이지·마지막 금색·4개 동시 Slash 즉시 그로기 |
| `Boss_05` 슈트 맨 | 기본 공격 기회가 적어 Slash로 멈추고 공격 | 1 추적 직선 레이저; 2 부채꼴 회전 레이저; 3 Slash 제거 가능한 연속 에너지 구; 4 6지점 별형 공격·마지막 착지 Slash 그로기·후속 회전; 5 화면 밖 가로/세로 돌진 중 Slash 저지+그로기; 6 대각 충전장치 2개·일반/Slash 저지, Slash 그로기; 7 관성 추적 이동·Slash 저지+그로기 |
| `Boss_06` 최종 보스 | 공중 부양·일반 공격 불가, 그로기 때 낙하·딜 타임 | 1 3지점 이동 방사 탄환; 2 육망성 입출구 12개·순회 탄환 반사 그로기; 3 추적 전기 기둥 4개·시간 후 폭발·전부 제거 그로기; 4 상하좌우 차원문 각 5개·추적 일제 탄환; 5 수정 4개 순차 융기·직접 폭파·타이밍 Slash로 폭발 없이 전부 제거+그로기; 6 약 6개 문 추적 반사 탄환; 7 벽으로 가둔 5×5 구역 중 1곳만 안전한 폭격 4회·마지막 낙하 Slash 그로기·융기 지형 복구; 8 차원문 낙하 육탄전 4회·착지 후 다시 사라지기 전 Slash 그로기 |

군용 로봇의 '반사 소개'는 어느 패턴에 적용되는지 원문에 명시되지 않았다. 해당 보스는 후속 범위다. 현재 경비 로봇 5패턴에도 금색탄 배치 지점은 명시되지 않아 원문에 없는 패턴으로 채우지 않는다. 공용 반사·각성 로직과 실제 패턴 배치는 구분한다. 꽃의 접촉 피해·Dodge 통과는 후속 보스 원문 이력이며 현재 Q28의 몸체 차단·접촉 피해 없음과 같다고 간주하지 않는다.

#### B04. BossPattern — 패턴별 C# 실행 객체

- **역할**: 패턴별 실행 조건(쿨타임·HP·위치·다른 패턴 실행 횟수), 추첨 가중치, 상황별 우선 실행 조건, 실행·완료·중단을 정의한다. 일반은 가중 추첨, 특정 상황에서는 특정 패턴이 먼저 실행될 수 있다(Q27).
- **생명 주기**: Boss 초기화 때 C# 패턴 객체를 등록하고 선택 시 시작한다. 완료 후 Idle, 사망·그로기·재도전 때 Cancel. Cancel은 아직 실행 중인 동작·추가 발사를 중단하며 이미 발사된 투사체와 장판을 일괄 제거하지 않는다.
- **보유 공개 API**: `CanRun` — 실행 조건; `GetWeight` — 현재 추첨 가중치; `GetPriority` — 상황별 우선 실행 값; `Begin`, `Tick`, `Cancel`; `OnCompleted` — 종료 알림.
- **보유 공개 변수**: `PatternId`, `Weight`, `Priority`, `Cooldown`, `LastUsedTime`, `IsRunning`. 특정 상황의 강제 조건은 아직 지정되지 않았으므로 기본값은 우선 개입 없음. 직전 패턴 제외 규칙은 유지한다.
- **연관 class**: BossPatternSelector가 목록·선택을 보유하고 BossCharacter가 선택 결과를 실행한다. Projectile/BossStructure/CollisionBox와 CharacterRendering·위험 예고를 사용한다.

#### B05. Projectile — 구체 투사체의 기반 클래스

- **역할**: 모든 구체 탄환의 기반 클래스. 이동·수명·피격·Slash 반응과 보상을 제공한다. 현재 특수탄은 금색=반사+Fever로 통합한다. 반사는 물리 반사각 대신 Boss 위치로 빠르게 이동해 적중하는 연출이다(Q3·Q12). 레이저·장판을 무조건 이 타입에 넣지 않는다.
- **생명 주기**: StageManager의 생성 메서드로 필요한 프리팹을 생성하고 패턴이 발사한다. 적중·Slash 제거·수명 만료·전투 초기화 때 종료한다. 그로기 시 이미 필드에 있던 탄은 유지한다. 풀링 도입은 아직 확정하지 않았다.
- **보유 공개 API**: `Initialize` — 소유자·이동·수명 적용; `Reflect` — Boss 목표로 반사 연출 전환; `HandleSlash` — 제거/금색 반사와 보상; `Despawn` — 판정·표시 종료.
- **보유 공개 변수**: `Owner`, `Damage`, `Direction`, `Speed`, `Lifetime`, `SlashResponse`, `GroggyGain`, `GrantsFever`, `ReflectionTarget`. 금색 반사 후에는 Player 피해 대상에서 제외한다.
- **연관 class**: 구체 탄환은 Projectile 상속. BossPattern이 발사를 요청하고 StageManager가 프리팹 생성. CollisionBox/Hitbox와 LineSlasher 연결, BossCharacter가 반사 목표, CharacterBuff가 Fever 적용.

#### B06. BossStructure — 기믹 대상 보완 정의

- **역할**: 일반 공격을 받는 구조물, Slash만 받는 특수 구조물, 부위·충전장치·기둥·수정 등의 기믹 판정. 원문의 동시 타격·부위 판정 조건을 보스에 전달한다.
- **생명 주기**: 보스/패턴 생성 시 준비, 파괴·기믹 완료·전투 초기화 때 해제. 시체 장식은 살아 있는 기믹 인스턴스와 구분한다.
- **보유 공개 API**: `ApplyDamage` — 허용된 공격 피해; `HandleSlash` — Slash 기믹 알림; `Reset` — 새 패턴/재도전 초기화; `Remove` — 대상 해제.
- **보유 공개 변수**: `Owner`, `TargetId`, `AcceptsNormalAttack`, `IsActive`, `HP`(체력이 필요한 구조물만).
- **연관 class**: BossCharacter/BossPattern 보유, Hitbox·DamageInstance 사용. LineSlasher는 적중만 알리고 Boss가 AttackId별 부위 결과를 모아 동시 타격 성공을 판단한다. 부위마다 Character 상속을 강제하지 않는다.

#### B07. EncounterTrigger — 조우 Trigger 보완 정의

- **역할**: 미처치 Boss 구역 진입을 감지하고 첫 조우/재전투를 구분한다. Trigger 발동, 해당 컷씬 완료, 문 닫기와 Boss UI 표시 순서로 전투를 준비한다(Q17).
- **생명 주기**: StageManager 생성·MapManager 배치 후 BossSpawner가 활성화한다. 발동 즉시 중복 진입 차단, Player 사망 때 복원, Boss 처치 후 비활성화. 첫 조우 저장 요청은 결과를 기다리지 않는다.
- **보유 공개 API**: `Initialize`, `Activate`, `Deactivate`, `HandleEnter`.
- **보유 공개 변수**: `Boss`, `IsActive`, `TriggerBounds` — 연결 보스·활성·구역 설정.
- **연관 class**: BossSpawner 소유, MapManager 위치 조회, PlayerCharacter 진입, SaveManager 첫 조우 기록, GameManager·CutsceneController·문 제어 요청.

#### B08. BossPatternSelector — Q17·Q27에서 필요한 책임 보완

- **역할**: Idle 요청에 대해 조건을 만족하고 직전 실행 패턴을 제외한 후보를 선택한다. 기본은 가중 추첨, 특정 상황의 우선 실행 조건이 있으면 해당 패턴을 먼저 선택할 수 있다. 후보가 없으면 null을 반환한다.
- **생명 주기**: Boss 초기화 때 1회 생성·패턴 등록, 전투 동안 재사용한다. 재도전 때 선택 이력 초기화, Boss 세션 종료 때 제거한다.
- **보유 공개 API**: `SetPatterns` — 목록 등록; `GetNextPattern` — 후보 또는 null 반환; `HandlePatternStarted` — 실제 실행한 직전 패턴 기록; `ResetHistory` — 재도전 이력 초기화.
- **보유 공개 변수**: `Patterns`, `LastPatternId` — 목록과 직전 실제 실행 패턴. Weight는 확률, Priority는 특정 상황의 우선 실행에 사용한다. 기본 우선 개입은 없고 지정된 규칙만 적용한다.
- **연관 class**: BossCharacter가 보유·요청하고 BossPattern의 조건·Weight·Priority를 조회한다. Controller가 목록을 알아야 할 필요는 없다.

매 프레임 목록 할당·정렬 없이 재사용 순회와 누적 가중 구간 선택으로 구현할 계획이다. 가중치에 의도한 차이와 구현 때문에 생긴 편향을 구별한다. 성능·분포는 아직 미검증이며, 직전 중복 금지를 우선 규칙으로 유지한다. 강제 조건·동점의 상세 규칙은 실제 해당 조건을 추가할 때 정의한다.

### 1.3 UI / anim

아래는 **연출 구현에 앞서 정한 코드의 책임과 데이터 계약**이다. Sprite·FX의 프레임·피벗·실제 모션과 미정 컷씬을 완성했다고 보지 않는다. 원문 디자인 요구는 3.3에 보존한다.

#### U01. CharacterRendering — 원문에 보유만 명시, 정의 보완

- **역할**: 커서 기준 4방향(왼앞·오앞·왼뒤·오뒤) 몸체·이동 모션, 무기 회전·공격·피격·대시·Draw/Slash·사망·부활 표현을 연결한다. 공격속도 반영과 논리 타격 시점을 제공하며 최종 Sprite·FX 프레임은 연출 확정 후 연결한다.
- **생명 주기**: 각 Character 프리팹과 함께 생성하고 초기화 때 대상 참조. 사망 모션이 끝날 때까지 유지, 재배치 때 기존 표시 초기화, Character 종료 때 해제.
- **보유 공개 API**: `SetFacing`, `SetMovement`, `PlayAction`, `SetAttackSpeed`, `ShowHitFeedback`, `ResetVisuals`; `OnHitFrame`, `OnActionFinished` — 타격·종료 시점 알림.
- **보유 공개 변수**: `FacingDirection`, `CurrentAction`, `IsPlaying`, `Animator`, `Sprites` — 방향·재생 상태·에셋 연결. 실제 프레임·피벗은 아직 미제작.
- **연관 class**: PlayerCharacter/BossCharacter 보유, AttackController·PlayerBodyController·LineSlasher 호출. Camera/UI를 직접 모두 소유하지 않는다.

#### U02. UIController — 보완 정의(Q3·Q4)

- **역할**: Player HP, Draw/Slash 사용 가능·쿨타임, 그로기, 전투 중 Boss HP, 상호작용 F 안내, 컷씬별 UI 숨김/복구. Player HUD와 Boss HUD는 별개로 제어해야 한다.
- **생명 주기**: 게임 UI 준비 후 Player에 1회 연결하고 사망·부활 동안 유지한다. Boss 조우 때 Boss만 연결하며 처치 때 Boss HUD만 숨기고 대상 연결 해제. Player HUD는 유지한다(Q19). Title 복귀 때 전체 연결 해제.
- **보유 공개 API**: `SetPlayer`, `SetBoss`, `ClearBoss`, `Unbind`, `SetPlayerHudVisible`, `SetBossHudVisible`, `ShowInteraction`, `RefreshStatus` — 대상·표시·상호작용 갱신.
- **보유 공개 변수**: `Player`, `Boss`, `IsPlayerHudVisible`, `IsBossHudVisible` — 연결·표시 조회. HP/쿨타임 수치를 UI에 두 번째 원본으로 저장하지 않는다.
- **연관 class**: Character 이벤트와 SkillController 상태 조회, CutsceneController 표시 요청, CrystalCheckPoint 상호작용. 개별 HPBar 클래스를 새로 만들 필요는 아직 없다.

#### U03. TitleUI — 보완 정의(Q3·Q4)

- **역할**: Title animation·슬롯 초기화 완료 후 시작/설정/종료와 3슬롯을 제공한다. 진행률은 처치 Boss 수로 계산하며 SaveData에 별도 저장하지 않는다. .sav→.bak 모두 실패하면 빈 슬롯, 완료 슬롯 선택은 새게임 확인 후 초기화한다.
- **생명 주기**: Title 씬에서 생성하고 UniTask로 초기화 완료를 기다린다. 슬롯 클릭으로 세션 플레이 시간 계측 시작, Title 복귀 때 종료. MainWorld 진입 때 Title UI 해제, 복귀 시 재구성한다(Q16·Q21).
- **보유 공개 API**: `ShowSlots`, `RefreshSlots`, `SelectSlot`, `ConfirmRestart`, `OpenSettings`, `QuitGame`.
- **보유 공개 변수**: `SelectedSlot`, `IsReady` — 선택·준비 상태. 슬롯 내용은 SaveData 조회 결과.
- **연관 class**: GameManager 시작·종료 요청, SaveManager 로드·선택, SettingsUI. 슬롯마다 별도 MonoBehaviour는 현재 필요하다고 확정하지 않는다.

#### U04. LoadingUI — 보완 정의(Q3·Q4)

- **역할**: 초기 배치 중 검은 화면·우하단 링, 준비 완료 후 화면 복귀. 후속 비동기 준비가 늦어질 때 같은 표시 재사용. 실패를 무한 로딩으로 숨기지 않는 보완 계약 필요.
- **생명 주기**: 로딩 동안 표시하고 완료/실패/취소 때 해제한다. 코루틴과 unscaled time으로 동작하여 게임 시간 정지에도 표시가 멈추지 않게 한다. 저장 성공을 기다리는 진행 차단 화면은 만들지 않는다.
- **보유 공개 API**: `Show`, `Hide`, `ShowFailure`.
- **보유 공개 변수**: `IsVisible`, `FailureMessage` — 외부 확인/사용자 안내.
- **연관 class**: GameManager·StageManager·SaveManager, UIController. 최종 로딩 FX와 대기 표시 임계시간은 후속 아트/수치 조정.

#### U05. SettingsUI — 원문 기능만 존재, 정의 보완

- **역할**: Title 및 비전투 ESC에서 음악·효과음 음량, 키 재지정, 해상도·창 모드를 조절한다(Q20 A). 컷씬에서는 ESC 예외 없이 입력 차단한다.
- **생명 주기**: 설정 표시/닫기 동안 사용하고 설정값은 슬롯 진행 데이터와 별도로 전역 보존한다. 새 게임 슬롯 초기화로 사용자 설정을 지우지 않는다.
- **보유 공개 API**: `Open`, `Close`, `Apply` — 표시·닫기·옵션 적용; `SetBinding` — 입력별 키 변경.
- **보유 공개 변수**: `IsOpen`, `MusicVolume`, `SfxVolume`, `Bindings`, `Resolution`, `WindowMode` — 선택값. 옵션 저장은 전역 설정 영역이며 Slot SaveData와 구분한다.
- **연관 class**: PlayerInputController 키 매핑, AudioController 음량, GameManager 설정 허용 상태. 키 재지정 패키지·별도 설정 프레임워크는 미도입.

#### U06. CameraController — 보완 정의(Q3·Q4)

- **역할**: 자체 최소 카메라. 기본 Player 지연 추적, 지정 target/시간 이동, Draw 때 Player와 커서 사이 가중 중심·줌 아웃, Slash 줌 인/복귀, 사망·보스 컷씬 포커스를 처리한다(Q19). 원문 기본 1초는 조정값이며 참고 코드가 정확히 1초 도착을 보장한다는 뜻은 아니다.
- **생명 주기**: 게임 카메라 준비 후 Player에 연결, unscaled time으로 Draw/Slash 정지 중에도 움직인다. 컷씬 종료·중단 시 기존 target·줌 복원, Title 복귀 때 참조 해제.
- **보유 공개 API**: `Follow`, `Focus`, `SetZoom`, `Restore`, `SetDrawAim`.
- **보유 공개 변수**: `Target`, `FollowDuration`, `DefaultZoom`, `Mode`, `DrawAimWeight` — 추적·전환 시간·기본 줌·Player/커서 가중치.
- **연관 class**: PlayerCharacter 위치·PlayerInputController 커서 좌표·CutsceneController 요청 사용. Sephiria TargetTracker/GameCamera, Dungreed PlayerTracker에서 필요한 부분만 참고한다. Cinemachine은 현재 도입하지 않는다.

#### U07. CutsceneController — 보완 정의(Q3·Q4)

- **역할**: 전역 컷씬 시작·완료·중단 이벤트, 입력 차단·게임 시간·UI·카메라·animation 완료를 코루틴으로 조정한다. 명시적 FSM은 도입하지 않는다. 현재 컷씬 입력 예외·ESC 건너뛰기는 없다(Q6).
- **생명 주기**: Managers와 함께 준비하고 연출 요청별 상태를 관리한다. 종료/중단/사망 시 복원 이벤트를 빠짐없이 보낸다. Draw/Slash 중 게임 동작·쿨타임·Line·Buff 시간은 정지, UI·카메라·컷씬은 unscaled로 계속한다(Q9).
- **보유 공개 API**: `Play`, `Cancel`, `Complete`; `OnStarted`, `OnFinished` — 전역 사건 알림. 시간 상태 적용은 GameManager의 단일 창구를 사용한다. Draw 전용 입력은 일반 컷씬의 모든 입력 차단과 구분한다.
- **보유 공개 변수**: `IsPlaying`, `CurrentKind` — 재생 여부·원인. CutsceneKind는 Spawn, PlayerDeath, BossEncounter, BossDeath, Slash, Ending으로 데이터만 식별한다.
- **연관 class**: GameManager, PlayerInputController, UIController, CameraController, CharacterRendering, AudioController. `+` 동시 연출도 스레드 병렬 실행이라는 뜻은 아니다.

#### U08. AudioController — FMOD 음향 연결 정의

- **역할**: FMOD를 사용해 구역별 발소리, 공격·대시·각성·Boss 효과음과 음악·음량을 연결한다. 비상업 전시와 FMOD 사용 경험이 도입 이유다(Q22).
- **생명 주기**: 게임 시스템 준비 시 생성, 씬/전투 전환 시 재생 대상 갱신, 앱 종료 때 종료. Title과 게임 음악 연결 방식은 연출 정의 때 확정.
- **보유 공개 API**: `PlayMusic`, `StopMusic`, `PlaySfx`, `PlayFootstep`, `SetVolume` — 음악·이벤트·바닥별 효과·설정 적용.
- **보유 공개 변수**: `MusicVolume`, `SfxVolume`, `CurrentMusic`, `FootstepEvents` — 음량·재생 중 이벤트·표면별 FMOD EventReference 매핑. 실제 음원과 Bank 제작은 아트/오디오 단계에서 연결한다.
- **연관 class**: SettingsUI, GameManager, CutsceneController, 공격/피격/패턴 피드백. FMOD 자체의 라이브러리 타입을 프로젝트 전용 Class로 다시 감싸는 범용 계층은 만들지 않는다.

### 1.4 시스템 / 기타

#### S01. GameManager — 케이스 책임 보완(Q3·Q4)

- **역할**: 슬롯 세션 시작·종료, 생성 준비, 전역 게임 시간, 재도전, 완료·엔딩을 조정한다. 마지막 제출 Boss가 쓰러지는 순간 완료 판정 후 컷씬·스탭롤·Title로 진행한다. Boss와 Player의 OnDie를 중간에서 중계하는 공통 객체 역할은 맡지 않는다.
- **생명 주기**: 앱 시스템 초기화부터 종료까지 하나 유지. 슬롯 클릭에서 플레이 시간 계측 시작, Title 복귀 때 종료하며 세션 객체를 제거한다. 새 세션에서 이전 Player/Boss 참조를 재사용하지 않는다.
- **보유 공개 API**: `Initialize`, `StartGame`, `RestartFromCheckpoint`, `ReturnToTitle`, `CompleteGame`, `SetGameplayPaused`, `QuitGame`. QuitGame은 SaveManager의 진행 중 저장·복구 완료를 기다린 뒤 종료한다(Q29).
- **보유 공개 변수**: `Instance`, `CurrentPlayer`, `CurrentBoss`, `IsLoading`, `IsInCombat`, `IsGameplayPaused`, `IsSessionCompleted`, `SessionPlayTime`. 시간 누적은 슬롯 진입~Title이며 Draw/컷씬/로딩도 포함한다.
- **연관 class**: SaveManager, StageManager, PlayerInputController, CutsceneController, TitleUI·LoadingUI·UIController·TutorialController.

#### S02. StageManager — 명칭 확정(원문 StageGeneratorManager)

- **역할**: 필요 Map·Boss·Player·패턴 프리팹 생성의 단일 창구. MapGenerator·BossGenerator는 별도 클래스가 아닌 메서드다. 공간 배치·맵 구성·상호작용은 MapManager, 개체 초기화는 Spawner에게 맡긴다(Q3).
- **생명 주기**: Title에서 단일 MainWorld로 진입할 때 Map과 현재 제출 Boss·Player를 준비한다. 현재는 경비 로봇1명이며 후속5보스를 미리 만들지 않는다. Player/Boss는 세션 동안 유지, Title 복귀 때 Clear. 씬·프리팹 생성은 메인 스레드 코루틴으로 조정한다.
- **보유 공개 API**: `Generate` — 세션 준비 코루틴; `CreateMap`, `CreateBosses`, `CreatePlayer`, `CreatePrefab` — 필요한 프리팹 복제; `Clear` — 세션 제거. UniTask는 이 생성 흐름에 사용하지 않는다(Q21).
- **보유 공개 변수**: `Map`, `Player`, `Bosses`, `IsReady` — 생성 결과·준비 완료 조회.
- **연관 class**: GameManager가 호출, MapManager·PlayerCharacter·BossCharacter 생성. MapManager가 위치/구성, 각 Spawner가 초기화. SaveData 처치 기록에 따라 우선 전체 Boss 프리팹 사망표시 B안을 적용한다(Q5).

#### S03. MapManager — 원문, Map 프리팹 Component

- **역할**: Map 프리팹의 배치·상호작용·구성, 체크포인트·Boss base·Trigger 위치 조회, 문 차단·열림·animation을 담당한다. 책임이 실제 비대해졌을 때 세부 클래스를 분리하고 지금 미리 나누지 않는다(Q3).
- **생명 주기**: Map과 함께 생성, SaveData 진행 상태로 초기화, Map 해제 시 종료. 활성화된 수정의 알림 연결은 끊는다는 원문을 보존한다.
- **보유 공개 API**: `Initialize`, `GetCheckpoint`, `GetBossPosition`, `GetEncounterArea`, `OpenDoor`, `CloseDoor`.
- **보유 공개 변수**: `Checkpoints`, `BossPositions`, `EncounterAreas`, `Doors` — 맵 배치 원본과 조회. public으로 연결하며 별도 DoorController는 현재 만들지 않는다.
- **연관 class**: StageManager가 생성, CrystalCheckPoint 이벤트, BossSpawner·PlayerSpawner 위치 조회, EncounterTrigger·CutsceneController 문 열림/닫힘 요청.

#### S04. CrystalCheckPoint — 원문, MonoBehaviour

- **역할**: Trigger 안의 상호작용 후보를 전달하고 F 입력 때만 한 번 활성화하여 회복·저장·문 열림을 요청한다. 범위 이탈은 후보만 제거하며 활성화하지 않는다(Q15).
- **생명 주기**: 프리팹 생성 후 저장된 활성 상태로 초기화한다. 활성화 완료 시 상호작용 Trigger와 Map 알림 연결을 제거하여 재사용하지 않는다. 저장 요청은 기다리지 않고 다음 흐름을 진행한다(Q16).
- **보유 공개 API**: 원문 `activateCheckPoint` — F 상호작용 활성화; `Initialize` — 저장 상태 적용; `SetInteraction` — 후보 설정/해제. 기존 원문 이름을 보존하며 기능 구현 후 사용자 요청에 따라 변경 가능.
- **보유 공개 변수**: `CheckpointId`, `IsActivated`, `SpawnPosition`, `IsActivating` — 식별·상태·재생성 위치·중복 입력 방지.
- **연관 class**: MapManager·SaveManager·PlayerInputController·PlayerSpawner·CutsceneController. 회복 완료 때 HP·CheckpointId 갱신 및 저장 요청, 저장 실패는 SaveManager가 처리한다.

#### S05. SaveManager — 원문 static class

- **역할**: 3슬롯 로드·선택, SaveData 접근, 저장 시점의 데이터 복사본으로 비동기 요청 직렬 처리. 체크포인트/첫조우/처치에서는 완료를 기다리지 않는다. 파일 기록·검증·복구 순서는 아래 Q29 규칙을 따른다.
- **생명 주기**: Title 초기화 때 슬롯 로드, 세션 선택 데이터 유지. static class이므로 Initialize/종료 API를 명시 호출한다. 한 요청 실패가 후속 요청을 영구 차단하지 않는다. 앱 종료 시 진행 중인 저장·백업·복구를 마친 뒤 종료한다.
- **보유 공개 API**: `LoadSlotsAsync`, `SelectSlot`, 원문 `NewSaveFile`, `Save(int backupCount)` — 복사본을 저장 큐에 등록, 인자는 현재 미사용; `FlushAsync` — 종료 시 미완 저장·복구 완료 대기. 구현 내부의 파일 비동기는 UniTask 사용, 요청자는 게임 흐름을 계속한다.
- **보유 공개 변수**: `CurrentSave`, `Slots`, `CurrentSlot`, `IsSaving`, `LastError`, `PendingSaveCount`. 저장 작업이 가변 게임 데이터나 Unity 객체를 백그라운드에서 직접 읽지 않도록 요청 시 복사한다.
- **연관 class**: SaveData 보유; TitleUI·GameManager·Spawner·MapManager·Boss가 조회/갱신한다. 진행률은 소비자가 처치 수로 계산. LogError에 최종 실패 이유를 남긴다. 최신 Q29가 Q16의 N개 백업 큐 가정을 대체한다.

#### S06. SaveData — 명칭 확정(원문 SavaData)

- **역할**: 원문 Dictionary<string, object> 값, key·타입 처리, 지정 경로의 .sav 기록·무결성 검사·.bak 복구/갱신. 암복호화 책임은 원문대로 보유하되 파일 형식·라이브러리의 구체 선택은 실제 코드 작성 때 결정한다.
- **생명 주기**: SaveManager.NewSaveFile 또는 파일 로드로 생성, 메모리에 유지하며 Set으로 갱신, 슬롯 전환/초기화 때 교체. 디스크 데이터는 별도 수명.
- **보유 공개 API**: `Get<T>`, `Set<T>` — 데이터 조회/변경; `Copy` — 요청 시점 복사본; `LoadAsync`, `SaveAsync`, `ValidateFile`, `RestoreBackup` — 로드·기록·검사·복구. 가변 배열/목록을 얕은 복사만 하고 독립 복사본이라고 기록하지 않는다.
- **보유 공개 변수**: `Values`, `FileName`, `FilePath`, `BackupPath` — 데이터와 경로. `.sav`와 `.bak`은 슬롯별 한 쌍이며 백업 개수 인자로 파일 수가 늘지 않는다. 진행률 필드는 없다.
- **연관 class**: SaveManager가 보유, TitleUI·Spawner·MapManager·Boss가 소비. 암호화 키나 타입 변환을 이유로 불필요한 프레임워크를 추가하지 않는다. 복구 정책은 아래 표 뒤에 명시한다.

| 데이터 | key | 갱신 시점 |
| --- | --- | --- |
| 현재 HP | `PlayerHP` | Boss clear, 수정 회복 완료 |
| 쓰러뜨린 Boss 수/기록 | `DefeatedBosses` | Boss 처치 때 BossId 기록 추가; 처치 수·진행률은 기록에서 계산 |
| 활성 체크포인트 | `CheckpointId` | 수정 회복 완료; Boss 처치 후 로드도 마지막 수정 위치 |
| 사망 횟수 | `DeathCount` | 매 사망 |
| 플레이 시간 | `PlayTime` | 슬롯 클릭~Title까지 누적, 저장 요청 시 현재값 반영 |
| 마지막 저장 시각 | `LastSavedAt` | 요청 스냅샷의 저장 시각; 저장 성공 보장과 구별 |
| 첫 조우 기록(보완) | `EncounteredBosses` | 첫 조우 컷씬 완료 |
| 튜토리얼 완료 단계(채택 시) | `TutorialStep` | 후속 튜토리얼 추가 시에만 사용, 현재 제출 데이터에는 불필요 |

**파일 처리 확정(Q16·Q26·Q29)**: 슬롯 로드는 .sav를 검사하고 실패하면 .bak를 검사한다. 둘 다 실패하면 빈 슬롯이다. Save(backupCount)의 인자는 남기되 현재 사용하지 않고 슬롯당 백업은 1개만 갱신한다. .sav 기록·무결성 검사, 실패 시 1회 재시도, 재실패 시 기존 .bak를 .sav로 복원하고 이유를 LogError에 남겨 해당 요청을 종료한다. 다음 저장 이벤트는 정상 시도한다. .sav 검사가 통과하면 그 파일을 .bak로 복사·저장한다. 앱 종료는 진행 중인 저장·복구 처리 완료 후 허용한다. 체크포인트·첫조우·처치의 게임 진행은 저장 완료를 기다리지 않는다. 세피리아의 백업 개수·큐를 그대로 복제한 규칙이 아니다(3.5).

**위 규칙을 연결할 구현안(아직 미구현)**: 저장 시점의 독립 복사본을 직렬 처리해 오래된 요청이 최신 데이터를 덮지 않게 한다. 파일 쓰기·복사·복구 결과를 확인하고, 임시 파일 검증 후 교체하여 유효 백업이 쓰기 중 손상되지 않도록 한다. 유효 백업이 없거나 복구도 실패하면 원인을 기록하고 무한 재시도하지 않는다. Title 전환 자체와 저장 성공을 결합하지 않되, 진행 중인 동일 슬롯 쓰기와 Title 재로드·슬롯 초기화가 경합하지 않도록 준비 완료 시점에 순서를 연결한다. 이는 사용자 확정 게임 규칙에 필요한 오류 처리·연결 제안이며 검증된 동작이나 별도 사용자 선택 사실이 아니다.

#### S07. CharacterBuff — 원문, MonoBehaviour

- **역할**: 각 Character 자기 효과를 관리한다. Apply 요청은 다음 프레임 적용/기간 갱신, Remove는 현재 처리에서 우선하며 대기 Apply도 제거한다. 동일 효과는 수치 중첩 없이 기간 갱신. Fever는 AD/AS/SP 증가·획득마다 1회 소량 회복·별도 Draw 쿨타임(기본 정상값 절반)이다.
- **생명 주기**: 각 Character 프리팹에 부착한다. 사망 단계에서 모든 효과 제거, 부활 단계에서 기본 수치 복원. Draw/Slash 정지 중 효과 타이머도 정지한다. Boss 처치 시 Player 디버프·지속피해 효과를 제거할 창구를 제공한다.
- **보유 공개 API**: `Apply` — 적용/갱신 예약; `Remove` — 효과 및 예약 제거; `Clear` — 모든 효과 해제; `ClearDebuffs` — Boss 처치 후 해로운 효과 정리; `Has` — 상태 조회. 재Fever는 기간 갱신과 회복1회·쿨타임 조정을 다시 수행한다.
- **보유 공개 변수**: `Target`, `ActiveBuffs`, `RemainingTime`, `FeverDrawCooldown` — 대상·효과·남은 시간·각성용 대기값. 실제 증가량·회복량·지속시간은 밸런스 설정값이다.
- **연관 class**: Character의 ApplyBuff/RemoveBuff, LineSlasher·Projectile의 Fever 보상, BossCharacter 그로기와 연결. 현재 장기 디버프/DOT를 부여하는 Boss가 없으므로 제거 구조만 정의하고 존재하지 않는 DOT 콘텐츠를 추가하거나 별도 검증했다고 기록하지 않는다(Q11·Q13).

#### S08. Hitbox — 판정 책임 보완(Q3·Q4)

- **역할**: Collider2D에 연결된 피격 대상·부위를 Character/Projectile/BossStructure 수신자와 연결한다. 공간 질의는 CollisionBox, 피해 허용은 각 수신자 책임이다.
- **생명 주기**: 대상 프리팹과 함께 준비, 판정 활성 구간에 조회 가능, 비활성/사망/제거에 맞춰 판정 해제. 몸체 이동 차단 Collider와 피격 역할은 구분한다.
- **보유 공개 API**: `ReceiveHit`, `SetEnabled`.
- **보유 공개 변수**: `Owner`, `TargetKind`, `Bounds`, `IsEnabled`, `PartId` — 대상·분류·범위·부위 식별.
- **연관 class**: Character/Projectile/BossStructure가 보유; CollisionBox가 조회; DamageInstance 전달.

#### S09. CollisionBox — 판정 책임 보완(Q3·Q4)

- **역할**: 일반 공격·Slash·적 공격의 사각 영역을 NonAlloc Cast로 조회하고 같은 공격의 같은 피해 수신자 중복을 막는다. 실제 Collider를 대상에 사용하며 박스 위치·회전·크기와 동일한 Editor Gizmo를 그린다(Q11).
- **생명 주기**: 공격 시작에 조건·적중 기록 초기화, 일반 공격은 지정 판정 구간, Slash는1프레임 조회. 완료/중단 때 종료한다. 재사용 배열이 가득 차 누락되는 경우를 구현 검증 대상으로 둔다.
- **보유 공개 API**: `Begin`, `Query`, `End`, `ClearHits`, `DrawGizmos` — 공격 준비·사각 Cast·종료·중복 기록·Editor 가시화. Unity6.3에서는 배열 결과를 받는 Physics2D.BoxCast 오버로드를 사용해 NonAlloc 요구를 충족하는 방향(3.4).
- **보유 공개 변수**: `Damage`, `Area`, `Angle`, `HitTargets`, `Results`, `IsActive` — 피해·박스 기하·대상 기록·재사용 결과 버퍼·실행 상태.
- **연관 class**: AttackController·LineSlasher·BossPattern이 일반 C# 질의 helper로 사용한다. Hitbox의 수신자를 기준으로 중복을 막고 Boss 부위 기믹은 AttackId/PartId별 별도 보고한다. 별도 공간 해시 프레임워크는 도입하지 않는다.

#### S10. DamageInstance — 피해 전달 struct

- **역할**: 누가 어떤 종류의 공격으로 어떤 피해를 주었는지 전달. 일반/특수 구분, 동일 공격과 부위 기믹 판정에 필요한 식별을 보유한다.
- **생명 주기**: 공격 시작 때 struct 값을 구성해 Hitbox/피해 API에 전달한다. AttackId는 한 공격의 여러 부위 적중을 묶으며 처리 후 보관할 필요가 없는 값은 소멸한다.
- **보유 공개 API**: 생성자 — 공격자·피해·종류·공격ID 구성. 피해 가능 판정은 수신자 API가 수행한다.
- **보유 공개 변수**: `Source`, `Amount`, `Kind`, `AttackId`, `IsHitboxDamage` — 공격자·피해량·종류·중복/동시타격 묶음·회피와 효과 피해 구분.
- **연관 class**: AttackController·LineSlasher·BossPattern 생성, Hitbox 전달, Character 피해 처리. DamageType으로 종류를 구분하고 Source/TargetKind로 대상 유효성을 판정한다.

#### S11. TutorialController — 후속 기능 정의

- **역할**: 후속 여유 범위의 안내 담당. 전투구역 이동 중 이동·공격·회피, 경비 로봇 전투 시작 Draw & Slash, 승리 후 체크포인트 안내. 현재 경비 로봇1명 완성에 앞서 dialogue/튜토리얼을 구현하지 않는다(Q1).
- **생명 주기**: 후속 튜토리얼을 추가할 때 새 게임에 준비하고 완료 단계로 반복 안내를 방지한다. 지금은 정의만 보존하며 생성·구현 대상이 아니다.
- **보유 공개 API**: `Begin`, `NotifyAction`, `Advance`, `SkipCompleted`, `End`.
- **보유 공개 변수**: `CurrentStep`, `IsCompleted`, `AllowedAction`.
- **연관 class**: PlayerCharacter 이벤트·성공 결과, PlayerInputController 입력 허용, SaveData 완료 기록, Boss_01/MapManager·UIController·CutsceneController 연결.

#### S12. DebugController — 원문, Managers Component

- **역할**: Editor에서만 ~ 입력·GUI로 Character 수치를 제어한다. 제출 소스에는 포함하며 디버그 코드 제작 효율 향상에 AI를 사용했음을 보고한다(Q23).
- **생명 주기**: Editor 실행에서 Managers와 준비·대상 연결, 종료 때 해제한다. 플레이어 빌드에는 Editor 제어가 개입하지 않도록 UNITY_EDITOR 등 컴파일 경계를 적용한다. 실제 구현·빌드 확인은 아직 미실행.
- **보유 공개 API**: `Toggle`, `SetTarget`, `SetValue` — 디버그가 허용한 수치만 변경.
- **보유 공개 변수**: `IsVisible`, `Target`.
- **연관 class**: PlayerInputController 키 입력, Character 수치. 프리팹/패키지를 새로 추가하는 도구가 아니라 원문 내부 기능이다.

#### S13. 데이터 enum과 보류한 상태 표현

Q6에 따라 현재 상태 변경은 bool·이벤트 구독으로 표현하고 별도 FSM 프레임워크는 만들지 않는다. N2의 Mode/State/Condition은 원문의 개념 분류로 보존하며 현재 필수 타입으로 중복 구현하지 않는다. 아래 enum은 행동 제어기가 아니라 데이터 식별값이다.

| enum | 역할(목적) | 생명 주기 | 보유 공개 API | 보유 공개 값·용도 | 연관 class(상속, 보유) |
| --- | --- | --- | --- | --- | --- |
| CameraMode | 추적 동작 선택 | 카메라 초기화·연출 진입/복귀 때 값 설정 | 없음, CameraController가 변경 | Observer 일반 추적, Target 지정 대상 | CameraController 보유, 상속 없음 |
| CutsceneKind | 연출 원인 식별 | 재생 시작 때 설정, 종료·취소 때 비움 | 없음 | Spawn·PlayerDeath·BossEncounter·BossDeath·Slash·Ending | CutsceneController 보유, GameManager/UI/Camera 전달 |
| BuffType | 효과 종류 | Apply 요청~효과 제거 동안 값 전달·보유 | 없음 | Fever 각성, Groggy 그로기, Slow 감속 | CharacterBuff 보유, Character API 전달 |
| DamageType | 피해 종류 | DamageInstance 생성 때 지정, 그 값의 수명을 따름 | 없음 | Normal 일반, Slash 특수, Effect Hitbox 외 효과 | DamageInstance 보유, Character/기믹 검사 |
| SlashResponse | Slash 대상 반응 | Projectile/기믹 초기화 때 설정 | 없음 | Ignore 반응 없음, Destroy 제거, Reflect 금색 반사 | Projectile/BossStructure 보유, LineSlasher 전달 |
| TargetKind | 피격 대상 분류 | Hitbox 초기화 때 설정, 대상 수명을 따름 | 없음 | Player·Boss·NormalStructure·SpecialStructure·Projectile | Hitbox 보유, 공격 유효 대상 판정 |

원문 후보 이력: GameMode(Tutorial/Cutscene/Initializing/Normal/Combat/Dead), PlayerState(Idle/Attack/Dash/Draw/Slash), CharacterCondition(Fever/Dodge/Invulnerable/Damaged). 필요해질 때만 enum/FSM 여부를 다시 비교한다. 이번에 피격 경직 상태를 새로 추가하지 않는다.

### 1.5 누락 역할 보완 결과와 정의 상태

| 원문에서 부족했던 정의 | 현행 책임 | 확정 근거 |
| --- | --- | --- |
| GameManager·전역 컷씬·시간 | S01 시간 창구 + U07 이벤트·코루틴, Player 내부 상태는 P03 | Q3·Q6·Q9 |
| Generator·Map·문 | S02 생성 메서드, S03 배치/구성/상호작용·문 | Q3·Q18 |
| Hitbox·CollisionBox·DamageInstance | S08~S10, Collider 수신자와 NonAlloc 사각 Cast 분리 | Q3·Q11 |
| Line·편집·수명 | P10 struct, P06 nullable 보유, P07 확정·만료 | Q7·Q8 |
| 패턴·추첨·투사체·다중 부위 | B04/B08 실행·선택 분리, B05 상속, B06/B01 Boss 측 집계 | Q3·Q12·Q17·Q27 |
| 그로기·몸체 접촉 | 현재 패턴 중단, 기존 위험물 유지, 접촉 피해 없음·몸체 진입 차단 | Q25·Q28 |
| EncounterTrigger | B07, 컷씬 뒤 문 닫기·Boss UI | Q17 |
| 렌더링·UI·카메라·설정 | U01~U06, 4방향·자체 카메라·Player HUD 유지 | Q9·Q19·Q20 |
| 튜토리얼·엔딩 | S11 후속, S01/U07/U03 현재 엔딩 | Q1·Q24 |
| 음향·비동기·Unity | U08 FMOD, S05/U03만 UniTask, Unity6.3 LTS | Q21~Q23 및 3.4 공식 조사 |
| 저장·복원·종료 | S05/S06, 백업1개·검사/재시도/복원·종료대기 | Q16·Q26·Q29 |

Q1~Q29의 역할·공개 계약·수명·참조·현재 콘텐츠 방향은 반영했다. Q4에 따라 신규 API 이름은 사용자가 기능 결과를 보고 바꿀 수 있다. 실제 수치 튜닝·각 패턴 타이밍·에셋 규격은 구현/연출 단계에서 다루며, 정의 완료를 구현 완료로 기록하지 않는다. 현재 개발 착수를 막는 답변 대기는 없다.

## 2. Case별 흐름과 구현 대응 — 정의 확정 후 작성

이 장에는 아직 확정 API 호출 순서를 쓰지 않는다. 사용자 요구대로 **1장 정의 완료 후 실제 코드 작성과 함께**, 각 Case에 `class.API / class.변수`를 사용해 흐름을 작성한다. 동시 작업은 `+`, 완료 후 시작은 `→`, 분기는 하위 리스트 `if : 조건`으로 쓴다. `+`는 논리적 동시 진행이며 멀티스레드를 뜻하지 않는다.

다음 목록은 원문의 Case를 잊지 않기 위한 **구현 대기 목록과 완료 판단 기준**이다. 현재 제출 범위는 `사양 상태: 사용자 확정`, 후속 보스·튜토리얼은 `사양 상태: 후속 원문 보존`이다. 모든 행의 `코드·연결 위치: 미생성`, `구현 상태: 미착수`, `확인 결과: 미실행`이다. 문서가 끝난 후 프로젝트를 만들기로 한 사용자 결정이 선행 조건이며, 이를 환경 오류나 아트 의존으로 기록하지 않는다.

### 2.1 Player

| 기능 ID | 원문 Case와 보존 규칙 | 담당 정의 | 아트 없이 확인할 완료 조건 | 남은 아트 의존 |
| --- | --- | --- | --- | --- |
| F-P01 | 이동·바라보기·무기 방향: WASD 합산, 반대 입력 상쇄, 커서 별도 추적, 뒷걸음 | P02·P03·P05·P09·U01 | 0/대각/반대 입력·차단/해제 시 방향과 위치가 규칙대로 변화 | 4방향 프레임·역재생 모션·피벗 |
| F-P02 | 대시: 이동 중 이동 벡터, 정지 시 커서; 회피·입력차단·쿨타임 별도 | P03·P09 | 벽/Boss 몸체 차단·0벡터·독립 타이머·재사용 제한 | 잔상 간격·Sprite·모션 |
| F-P03 | 일반 공격: 홀드 반복, 3콤보, AS 반영, 느린 이동만 허용·대시 취소 없음 | P03·P05·S08~10 | 콤보 만료·중복 피해·강제 중단·유효 대상 | 공격 FX·판정 프레임 일치 |
| F-P04 | Draw 진입/종료: UI·카메라·시간·입력 전환; 재Draw 취소 때 기존 Line 유지 | P06·P07·U02·U06·U07 | 재진입/취소·복구 및 일반 입력 차단 | 줌·암전·UI 모션 |
| F-P05 | 점 0/1/2개, 미리보기, 고정 방향, reset/confirm/cancel | P07·P10 | 같은 점 무효·고정 길이·2개 이후 입력·취소/확정; 맵 경계 보정 없음 | 점·선 표시·커서 |
| F-P06 | 확정 Line 수명·만료 예고·자연 만료·쿨타임 | P06·P07 | 시간 정책·만료/Slash 같은 프레임·오래된 선 제거 | 깜빡임·소멸 FX |
| F-P07 | Slash: 선·쿨타임 확인, 정지 마지막 시점 판정, 피해·제거·반사·각성 | P08·B05·B06·S08~10 | 대상 중복/AttackId 부위 전달·벽 무관·Line1회소모·중단 복구 | 발도·검격 FX·발동 프레임 |
| F-P08 | 피격: Dodge와 완전 무적, 피해 후 보호·깜빡임 | P01·P09·S07~10 | 유효 피해만 HP/OnDamaged, 사망 1회, 효과 피해 구분 | 깜빡임·음·화면 피드백 |
| F-P09 | 최초 생성·부활·사망: 연결, 저장 HP/maxHP, Boss 초기화, 사망 수 저장 | P04·S01·S05 | 같은 인스턴스·Input/UI 연결 유지·전투 OnDie만 해제·죽음 효과제거/부활 기본값 | 수정 앞 기상·사망 연출 |
| F-P10 | 수정 상호작용: 후보 전달, F 우선, 회복·저장·문 열림 | S04·P02·S03 | 범위 이탈은 후보만 제거·F1회 활성화·비동기 저장 실패 | 수정·상호작용 모션·F 아이콘 |
| F-P11 | 각성·그로기·슬로우 적용/갱신/제거 | S07·P01·B01 | Remove 우선·기간갱신+매획득회복·Draw반감값·100도달즉시그로기/0소모 | 오라·그로기·슬로우 표현 |

### 2.2 Boss

| 기능 ID | 원문 Case | 담당 정의 | 아트 없이 확인할 완료 조건 | 남은 아트 의존 |
| --- | --- | --- | --- | --- |
| F-B01 | 생존/처치 Boss 생성·사체 표시·Player 사망 시 재배치 | S02·B02 | 저장 상태별 생성·Trigger·기본 수치 복원 | 사체 Sprite·배치 크기 |
| F-B02 | 첫 조우/재조우: 문·컷씬·OnDie 구독·전투·첫 조우 저장 | B07·B01·S03·S05 | 컷씬 뒤 문+Boss HUD·첫 조우 저장·중복 진입 차단·비전투 유효피해 | 입장 컷씬·문 모션 |
| F-B03 | Idle/조건부 패턴·확률·가중치·페이즈 | B01·B04·B08 | 조건/직전 제외·가중 추첨·상황별 우선·후보 없으면 Idle | 전조·패턴 모션 |
| F-B04 | 그로기 진입·패턴 취소·회복 | B01·S07 | 100즉시발동/0소모·진행패턴중단·기존투사체/장판유지 | 그로기 표시·낙하 모션 |
| F-B05 | Boss 사망·진행 저장·문 열림·구독 해제 | B01·S01·S03·S05 | 단 한 번 처리, 저장/사체/재진입 반영 | 사망 컷씬·카메라/UI |
| F-B06 | 탄환 제거·금색 반사+각성·레이저/장판·몸 진입 차단/접촉피해 없음 | B05·B06·S08~10 | 공격당 중복·반사 소유자·유효 대상·전투 종료 정리 | 위험색·예고·범위 FX |
| F-B07-01~05 | 경비 로봇 패턴 1~5 (B03 표) | B03·B04 | 각 패턴 시작/피해/종료/강제중단 | 보스별 Sprite·손·레이저·탄환 |
| F-B08-01~06 | 군용 로봇 패턴 1~6 | B03·B04·B05·B06 | 50% 변화·유도/역장 수명·반사 대상 확정 | 미사일·문·폭격 |
| F-B09-01~05 | 뱀 패턴 1~5 및 평상 이동 | B03·B04·B06 | 머리+꼬리 동시 Slash, 먹는 시점, 50% 변화 | 마디·잠복·전기 |
| F-B10-01~07 | 꽃 봉오리 패턴 1~7 | B03·B04·B06 | 촉수 접촉·반사·4개 동시 타격 기믹 | 촉수·방패·전류 |
| F-B11-01~07 | 슈트 맨 패턴 1~7 | B03·B04·B06 | 저지 창·6번째 착지·충전 2개·관성 추적 | 레이저·착지·충전 |
| F-B12-01~08 | 최종 보스 패턴 1~8 및 부양 | B03·B04·B06 | 부양 피해 제한·기둥/수정 일괄 기믹·벽 복구 | 차원문·기둥·수정·낙하 |

경비 로봇 F-B07의 5패턴은 현재 제출 대상이다(Q24). F-B08~12의 나머지33패턴은 후속 콘텐츠이며 현재 구현 완료 조건에 포함하지 않는다. 총38개 원문 목록은 보존한다.

### 2.3 UI / anim

| 기능 ID | 원문 Case | 담당 정의 | 아트 없이 확인할 완료 조건 | 남은 아트 의존 |
| --- | --- | --- | --- | --- |
| F-U01 | Title·3슬롯·진행률·설정·종료 | U03·S01·S05 | sav/bak 모두 실패=빈 슬롯·완료=새게임확인·진행률 계산 | 로고·배경 동화·슬롯 디자인 |
| F-U02 | Player 4정보·Boss HP·쿨타임·상호작용 F | U02 | 값/사용 불가/대상 변경 반영·구독 해제 | 아이콘·바·게이지·배치 |
| F-U03 | 컷씬 UI 숨김/복귀·Boss HP 독립 제어 | U02·U07 | 원인별 정책·취소 복구·Boss 사망 시 Player HUD 유지/Boss HUD 숨김 | 10/60초 모션·색 |
| F-U04 | 추적·target·Draw·Slash·사망 카메라 | U06 | target 변경·정지 중 이동·복귀 | 줌량·이동 곡선·구도 |
| F-U05 | 로딩·기다림·실패 표시 | U04 | 준비 완료 집계·실패 분기·중복 해제 | 링·검은 화면 전환 |
| F-U06 | 공격 판정/FX 시점·재생속도·중단 | U01·P05·P08 | 임시 신호로 타격/종료 확인, 최종 프레임은 나중 | Sprite 프레임·FX·동기 조정 |
| F-U07 | 음악·효과음·음량 | U08 | FMOD 발소리/공격/대시/각성/Boss 이벤트·재생·정지·음량 | 실제 음악/효과음·전환 연출 |
| F-U08 | 설정·비전투 ESC | U05·P02 | 전투 중 차단, 선택 옵션 저장/복구 | 최종 패널 디자인 |

### 2.4 시스템 / 기타

| 기능 ID | 원문 Case | 담당 정의 | 아트 없이 확인할 완료 조건 | 남은 아트 의존 |
| --- | --- | --- | --- | --- |
| F-S01 | Title 준비·슬롯 로드·MainWorld 초기화 | S01·S02·S05 | Map/Boss/Player/UI 모두 준비 뒤 시작·실패 복구 | 최초 진입 연출 |
| F-S02 | 저장 Get/Set·명시적 비동기 Save·backup | S05·S06 | key/타입 오류·연속 저장 순서·손상/백업·종료 중 저장 | 없음 |
| F-S03 | 체크포인트·Boss 처치·첫 조우·진행률 | S03~S06 | 다음 실행/부활에서 같은 진행 상태 복원 | 문·사체 표시 연결 |
| F-S04 | 전체 정지/입력 차단·상태 우선순위 | S01·U07·P02·S07 | 죽음/로딩/Draw/Slash 전환 시 상태 잔류 없음 | 정의된 컷씬 타이밍 |
| F-S05 (후속) | 튜토리얼·재시도 안내 생략 | S11 | 완료 조건·허용 입력·진행 기록 | 안내 문구·조력자/벽 그림 |
| F-S06 | 마지막 Boss·엔딩·Title·새 게임 | S01·U03·U07·S05 | 마지막Boss사망순간 완료→컷씬→스탭롤→Title·완료슬롯 새게임확인 | 사망 컷씬·스탭롤·Title 전환 |
| F-S07 | Debug GUI·수치 변경 | S12 | Editor에서만 제어·소스제출·AI사용기록 | 최종 디자인 의존 없음 |

케이스 구현 시 각 행을 `기능 ID | 사양 상태 | 코드·씬·프리팹 연결 | 구현 상태 | 실제 확인 결과 | 남은 아트 | 비아트 차단 사항`으로 갱신한다. 임시 시각물로 확인한 결과를 최종 아트 검증으로 기록하지 않는다.

## 3. 고민·결정·아트·도구

### 3.1 고민

이 절은 **아직 비교·측정하지 않은 원문의 고민**이다. 선택한 현재안이 있어도 성능 우위가 입증된 것은 아니다. 제출 사양 충돌(Q)은 먼저 결정하고, 성능 비교는 기능을 구현한 뒤 같은 조건으로 한다.

| 고민 ID | 어느 부분에서 어느 로직을 어떻게 비교하려 했는가 | 출처 | 이후 비교 기준 / 상태 |
| --- | --- | --- | --- |
| C01 | Player 부활: 기존 인스턴스 이동·초기화 A / Destroy 후 재생성 B | N1 PlayerSpawner, 110행 | Q5에서 기존 인스턴스 유지 확정. 재생성과의 성능 비교는 미측정 |
| C02 | 이동속도: 상태 변경 때 SP 대입 A / 매 이동 시 bool 분기 B | N1 이동, 289행 | 동일 프레임 입력에서 CPU·정확성 비교. 미측정 |
| C03 | Scene: 한 Scene 전체 선배치 A / 한 Scene 내용을 로딩 중 교체 B / 스테이지별 Scene C | N1 비고 1, 543~549행 | 첫 로드·재도전·구간 이동·메모리. 원문은 A로 시작 후 비교 |
| C04 | 단일 맵 A / Additive Level Streaming B / 시야 타일·체크포인트 선행 배치 C | N1 StageGeneratorManager, 229~234행 | 작은 규모에 필요한지, 학습·연결 비용과 준비 지연. 미측정 |
| C05 | 처치 Boss: 사망 Sprite A / 배치 없음 B / 전체 프리팹 C; 판단 주체도 생성 측 / Boss 내부 | N1 234·414·487행 | Q5 질문의 B=전체 프리팹을 먼저 적용하고 A=사체 Sprite와 비교 예정. 이 표의 원문 A/B/C 표기와 질문 A/B를 혼동하지 않음. 성능 미측정 |
| C06 | 생성·배치·부활·컷씬: 코루틴 A / 다른 비동기 방법 B, 다음 구역 사전 준비 여부 | N1 비고 2, 551~564행 | 프레임 정지·완료 시간·취소/실패 처리. 코루틴의 진행 겹침을 멀티스레드 병렬화로 단정하지 않음 |
| C07 | 조우 Collider A와 코드 Hitbox B의 연결, 전투 중 Trigger 종료 여부 | N1 조우, 479행 | 중복 조우·전투 판정·초기화 명확성. Q11·Q17 |
| C08 | 별도 Mode FSM A / Player 상태로 일반·전투 처리 B | N2 시스템 Case 1, 77~79행 | Q6 현재 bool·이벤트 구독 채택, 명시적 FSM은 필요 시 후속 비교 |
| C09 | 전투 가능 상태를 bool로 매번 검사 A / 다른 허용 판정 B | N2 Boss 전투, 86행 | 전투 전 피해·대상 인식 정확성 및 실제 비용. B 로직은 원문 미정 |
| C10 | 모든 Boss 비활성 선배치 A / 필요할 때 생성·활성화 B | N2 Boss 배치, 112행 | 초기 로드·메모리·구간 진입 끊김. 최적화 후보 |
| C11 | 태그 판정: CompareTag A / tag == B | N2 외부 상호작용, 148행 | 실제 사용 경로에서 측정. Reddit 링크는 원문 참고이며 성능 결론으로 채택 안함 |
| C12 | 상태 enum A / '이진수' 표현 B | N2 Mode-State-Condition, 171행 | 표현 의미·조합·메모리·CPU. enum 자체와 Flags 표현을 먼저 정확히 구분 |
| C13 | Condition이 수명 관리 A / bool만 제공하고 코루틴·함수에서 통합 관리 B | N2 219행 | 종료/재적용/해제 순서·정지 중 타이머. Q5·Q9·Q13 |
| C14 | 각성 수치: 매 조회 bool 분기 A / 상태 변경 때 대입 B / 배열 C / 삼항 연산자 D | N2 224행 | 원문의 '분기 예측' 고민 보존, 측정 없는 속도 우열 결론 금지 |
| C15 | 발생 원인이 다른 무적 시간: animation event A / Update 타이머 B | N2 227행 | 프레임 속도·중단·정지·연출 교체 시 정확성. 미검증 |
| C16 | 대시 animation 종료와 입력 재개를 같은 시점 A / 다른 시점 B | N2 207행, N1 512행 | Q9 서로 다른 타이머 확정. 보호·입력·이동 종료는 독립 조정, 실제 조작감 미검증 |
| C17 | 콤보 카운터·bool 상태까지 Status에 포함 A / 각 담당 기능에 분리 B | N2 246~252행 | Dungreed·Sephiria 참고 요청. 3.5에 읽은 구조만 기록, 성능 비교 아님 |
| C18 | dot animation과 VFX를 원하는 시점에 연결하는 방법 | N2 마지막 311행 | 원문에 A/B 후보가 없으므로 없음으로 보존. Animation Event / 명시적 로직 시점 신호는 AI 제안, 연출 확정 후 비교 |
| C19 | 보스 사망 컷씬: 완전 무적 A / 피격 무시 B | N2 Boss 사망, 122행 | Q11 A: 컷씬/Slash는 전면 무적, Dodge는 Hitbox 피해 보호 |
| C20 | Slash 가능 판단을 endPoint 존재만으로 할지 / 확정 Line 유효성까지 검사할지 | N2 236행 | 임시 편집점·같은 좌표·만료 상태를 구별. 유효 Line 검사 제안 |
| C21 | 움직일 때 몸 접촉 피해, 정지할 때 접촉 허용의 자연스러움 | N2 적 상호작용 270~273행 | Q28 현재 접촉 피해 없음, Player의 Boss 몸체 진입은 충돌 차단. 조작감 미검증 |
| C22 | OnDie 직접 상호 구독 A / 공통 중계 객체 B | Q5 | A 우선. 오버헤드가 적을 것이라는 예상 + 별도 객체 설계 시간 부족. 실제 호출/해제 비용·복잡성 비교 보류 |
| C23 | 금색탄을 Boss로 빠르게 보내는 반사 연출 A / 반사각을 활용하는 실제 반사 B | Q12 | 빠른 개발을 위해 A, B는 후속 검사 영역. 물리 반사 성능을 입증한 선택 아님 |
| C24 | 몸체·이동 모션4방향 A / 8방향 B | Q9 | 개발 기간상 A, B는 2차 목표 |
| C25 | MapManager 현재 책임 유지 A / 세부 책임 클래스 분리 B | Q3 | 현재 A, 책임이 실제 커졌을 때 B 검토 |
| C26 | 특정 컷씬 ESC 건너뛰기 추가 | Q6 | 현재 입력 예외 없음, 시간이 남으면 후속 검토 |
| C27 | 패턴 추첨: 매 요청 후보 목록 구성 / 기존 목록·버퍼 재사용 순회 | Q17·Q27의 최적화·편향 우려, B08에서 구체화 | 아래 PF05로 CPU·할당 비교. 가중 분포 정확성은 별도 확인 |

L1의 입력 방법·카메라 보스 보정·회피 보정·커서·캐릭터 식별 비교는 원문 8.1~8.3에 남아 있다. 그 문서의 1일/3일/10일 실험 계획을 현재 남은 일정에 그대로 재배정하지 않는다. 이번 통합에서는 Q8·Q9·Q10·Q19와 후속 아트 검증으로 연결한다.

#### 3.1.1 최적화 비교 계획 — Unity Profiler

**목적(2026-11-15 사용자 요청)**: 같은 기능을 제공하는 구현들을 Unity Profiler로 비교하여 더 나은 성능이 확인된 안을 채택하고, 프로젝트 결과의 선택 이유를 실측 자료로 뒷받침한다. 아래 A/B 구성·우선순위·측정 조건은 이를 위한 **실험안**이다. 현재 구현안을 바꾸거나 비교를 완료했다는 뜻은 아니다. 모든 항목은 **미실행**이며, 기능이 동작한 뒤 측정한다.

| 비교 ID·출처 | 비교할 구현 | 동일하게 유지할 조건 | 주요 측정값·비교 시점 |
| --- | --- | --- | --- |
| PF01 · C05 | 현재 전체 Boss 프리팹의 사망 표시 / 사체 전용 표시 객체 | 같은 처치 기록·Sprite·위치·표시 시간, 양쪽 모두 전투 기능 중단 | 초기화/처치 구간 CPU·GC Alloc·사용 메모리·객체 수. 현재 1보스와 복수 보스 누적 결과를 분리. 완료 슬롯은 새 게임이므로 처치 맵 재로드의 실사용 이득은 해당 진행 경로가 있을 때 평가 |
| PF02 · C02·C14 | 상태 변경 시 SP·Fever 수치 계산/대입 / 매 이동·공격 조회 시 같은 수치 계산/분기 | 동일 입력·효과 획득/갱신/제거·최종 수치. 조회 비용과 상태 변경 비용 모두 포함 | 관련 함수 CPU Time/Self·Calls·GC Alloc. 실제 전투 입력에서 비교하며 배열/삼항 문법 자체의 우위를 가정하지 않음 |
| PF03 · C13 | CharacterBuff의 Update 순회로 효과 시간 관리 / 효과별 코루틴으로 같은 수명 처리 | 효과 수·기간·빈도, Draw/Slash 중 정지, Remove 우선, 사망 정리 동일 | 평상시·효과 적용/종료 프레임 CPU·GC Alloc·종료 후 잔류 작업. 현재 효과 수로 먼저 측정하고 인위적 대량 효과 결과를 실게임 개선으로 쓰지 않음 |
| PF04 · C22 | 현재 OnDie 직접 상호 구독 / 단일 중계 객체 경유 | 같은 수신자·통지 내용·전투/사망/재도전 횟수, 중복 호출·구독 누적 없음 | 구독/해제/통지 CPU·GC Alloc·반복 재도전 후 객체/참조 잔류. 저빈도 사건이므로 측정 오차 이하이면 우위 미확인 |
| PF05 · C27, B08 | 요청마다 후보 목록 구성 / 기존 패턴 목록·버퍼를 재사용하며 누적 가중치로 순회 | 같은 조건·직전 패턴 제외·우선 실행 조건·가중치·입력/난수 조건 | Idle의 GetNextPattern CPU·Calls·GC Alloc·최악 프레임. 추첨 분포·중복 금지·후보 없음은 별도 정확성 검사 |
| PF06 · C10, 조건부 | 제출 Boss 전체 선배치·비활성 / 필요한 Boss를 진행 중 준비 | Boss 2명 이상이 된 뒤 같은 자산·경로·준비 완료 조건, 단일 MainWorld 유지 | 최초 준비 시간·구간 진입 지연·전투 프레임·최대/유지 메모리. 1보스 단계에서 비교용 콘텐츠나 Streaming을 새로 만들지 않음 |
| PF07 · C11, 낮은 순위 | CompareTag / tag 문자열 비교 | 같은 호출 지점·대상·태그·실제 호출 횟수 | 실제 경로가 CPU/할당의 유의미한 비용으로 관찰될 때만 비교. 억지로 수백만 회 반복한 차이를 실게임 효과로 주장하지 않음 |

**진행 순서**: 현재안이 정상 동작하는 기준선을 확보한 뒤 PF01~PF05 중 실제 비용이 관찰되는 항목과 기존 성능 주장을 확인할 항목부터 비교한다. PF06·PF07은 조건 충족 시 진행한다. Profiler 비교가 현재 확정 기능의 완성을 대신하지 않으며, 모든 대안을 먼저 구현하는 과제로 확대하지 않는다.

**비교에서 분리할 고민**:

- C01의 Player 재생성은 세션 인스턴스·참조 유지 계약을 깨므로 단순히 더 빠르다는 이유로 채택하지 않는다. C03·C04의 씬 분리/Streaming과 C06의 비동기 도구 재선택은 현재 규모·도구 범위를 바꾸므로 이번 우선 비교에 포함하지 않는다.
- C08·C12·C17·C25의 FSM·상태 표현·책임 분리는 정확성과 추가 개발 편의가 우선이다. C07·C09는 구체 대안이 부족하다. 병목 근거 없이 새 구조를 만들지 않는다.
- C15·C18의 타이밍/연출 연결은 같은 결과·중단 규칙을 보장한 뒤 검토한다. C16·C19~C21·C23·C24·C26의 대시·무적·유효성·접촉·반사각·방향 수·skip은 행동/표현 자체가 달라 순수 최적화 비교와 별도로 유지한다.

**측정과 채택 방법**:

1. 한 번에 한 구현만 바꾼다. A/B의 기능·수치·이벤트 순서·자산 결과가 같은지 먼저 확인한다. 패턴 추첨의 의도한 확률과 편향 여부는 성능 수치와 별도로 확인한다.
2. 제출 대상 Windows 환경의 Development Build에 Profiler를 연결한다. Editor 측정은 빠른 조사 자료로 구분한다. Unity/패키지 버전·기기·스크립팅 백엔드·해상도·품질·VSync/프레임 제한·자산·입력/난수·측정 구간을 동일하게 기록한다.
3. 초기 로딩, 워밍업 후 전투, 재도전을 구분해 같은 구간을 반복 측정한다. 반복 횟수·대표 CPU 시간·느린 프레임/최대 지연·GC Alloc·사용/예약 메모리·객체 수를 남긴다. FPS 하나나 한 번의 최솟값으로 결론 내리지 않는다.
4. 정량 비교는 Deep Profile을 끈 동일 조건에서 하고 필요한 함수 구간에 ProfilerMarker를 둔다. Deep Profile은 원인 조사에만 별도 사용한다. GC Alloc은 할당량이며 그 자체로 메모리 누수의 증거가 아니다.
5. 각 비교의 주지표를 측정 전에 정한다. 반복 측정에서 개선이 확인되고 기능·다른 핵심 지표가 악화되지 않은 안을 선택한다. 지표가 서로 상충하면 비용과 이득을 함께 기록한다. 차이가 불명확하면 **성능 우위 미확인·현재안 유지**로 결론 내린다.
6. 선택 후 관련 흐름을 재검증하고 GAME_SPEC 현재안·선택 이유·결과 보고 자료를 갱신한다. 결과는 해당 기기·규모·조건에 한정하며 다른 프로젝트까지 일반화하지 않는다.

| 결과 기록 양식 | 환경·버전·반복 횟수 | A/B CPU·GC·메모리·지연 | 기능 동등성 | 채택/유지와 이유·한계 | Profiler 캡처 경로 |
| --- | --- | --- | --- | --- | --- |
| PF ID — 측정 시 작성 | 미실행 | 미측정 | 미검증 | 결정 전 | 아직 없음 |

공식 근거 확인일: **2026-11-15**. [대상 플랫폼에서 측정](https://docs.unity.com/en-us/engine/6000.3/manual/analysis/profiler/profiling-applications/profiling-target-device), [Deep Profile 비용](https://docs.unity.com/en-us/engine/6000.3/manual/analysis/profiler/deep-profiling), [ProfilerMarker](https://docs.unity.com/en-us/engine/6000.3/script-reference/unity/profiling/profilermarker), [CPU 지표](https://docs.unity.com/en-us/engine/6000.3/manual/analysis/profiler/visualizing-data/cpu/cpu), [Memory 지표](https://docs.unity.com/en-us/engine/6000.3/manual/analysis/profiler/visualizing-data/memory/memory). 이번 정리에서 Profiler 실행·추가 패키지 설치·성능 측정은 하지 않았다.

### 3.2 결정

원문에 이유가 붙은 선택은 아래에 보존한다. 이번 사용자의 확정과 원문 작성 당시의 선택을 구별하며, 성능 주장 자체를 검증 결과로 승격하지 않는다.

| 결정 ID | 선택 내용 | 원문 이유 | 상태·출처 |
| --- | --- | --- | --- |
| D01 | 수치·상태·기본 API를 Character에 우선 집중 | 쉽게 조회하고 한 곳에서 검토 후 필요한 담당으로 이동 | 원문 임시 정리, N1 9~18행 |
| D02 | Input은 의도 전달, Player는 행동 확정; 2개 Update는 구현 방법 중 하나 | 역할 분리 | 원문 선택, N1 61~65행. 호출 순서 확보 방식은 미구현 |
| D03 | 외부 상황이 BlockInput을 변경 | Player가 InputController를 모르게 함 | Q6 확정. 전역 사건→Input 차단, Player 내부 가능 여부는 Player 책임 |
| D04 | 단일 MainWorld 전체 배치부터 제작 | 이어진 공간, 작은 게임 규모, Additive 미숙 | 원문 우선안이며 비교 후 변경 가능, N1 230~233·549행, Q18 |
| D05 | 죽은 Boss는 Sprite만 배치 | 원문은 RAM 누수 방지라고 기록 | 과거 원문 선택. Q5에서 전체 프리팹 사망표시 우선으로 변경, Sprite만 두는 안은 비교 대상 |
| D06 | Boss 사망 후 이벤트·연결 해제 | 원문은 RAM 누수 방지라고 기록 | Q5에서 전투 OnDie 상호 구독만 해제함을 명확화. Player/Input/UI 생존 연결은 유지, 잔류 참조 검증 미실행 |
| D07 | 팔레트 고정·디자인 재사용 | 자연스러운 픽셀 및 작업량 감소 | 원문 선택, N4 3~6행 |
| D08 | 컷씬 종료 UI는 진입 동작 역방향 활용 | 동일하게 원위치 복구·코드 재사용 | 원문 선택, N4 125~129행 |
| D09 | 8방향 전체 공격 animation 대신 회전 무기와 검기 FX | 모든 방향 animation 확보가 어려움 | 원문 선택, N4 175~177행 |
| D10 | 콤보 1·2는 같은 검기 뒤집기, 3은 X형 별도 | 재사용 방향이며 별도 성능 이유 미기록 | 원문 선택, N4 179~181행 |
| D11 | 일반 공격은 검집채, Slash만 발도 | 검집 착용 Sprite 제작 수고 감소, 대칭 무기 | 원문 선택, N4 183행 |
| D12 | 실험실 PNG 무대 재사용·위에 Sprite 중첩 | 디자인 시간이 적어 최소 소스를 최대한 재활용 | 원문 선택, N4 191~197행 |
| D13 | 인게임 비동기 준비 대기에도 로딩 도트 재사용 | 게임이 계속 작동 중임을 알림 | 비동기 채택 시 조건부, N4 61행 |
| D14 | 클래스 정의 완료 후 케이스별 코드·흐름을 함께 작성 | 사용자가 흐름의 실제 구현을 확인하기 위함 | 2026-11-14 사용자 확정 |
| D15 | 문서와 비연출 로직 방향 확정 후 Unity 개발 시작 | 후속 사용자 지시. 별도 이유 미기록 | 2026-11-14 사용자 확정 |
| D16 | 경비 로봇1명·원문5패턴과 입장~Ending을 먼저 완성 | 극단적으로 짧은 흐름을 우선 완성하고 남는 시간에 적·dialogue 순으로 추가 | Q1·Q24 사용자 확정 |
| D17 | 신규 API 명명 위임, public/static 중심 | 기능 결과를 보고 사용자가 직관적으로 바꾸며 과한 안전장치의 코드 증가를 피함 | Q4 확정. 오류 처리 자체를 없애는 뜻은 아님 |
| D18 | Input→Player 단방향, 프리팹 세션 유지, Character별 Buff | 세피리아 구조 참고; 다른 수명 선택의 상세 이유 미기록 | Q5·Q6 확정, 직접 OnDie의 예상 이점은 C22 |
| D19 | 그로기100 즉시 발동·0소모, 패턴 중단·기존 위험물 유지 | 이유 미기록 | Q25·Q28 확정, 접촉 피해 없음/몸 진입 차단 포함 |
| D20 | Save/Title 초기화만 UniTask, 나머지 코루틴 | 세피리아의 사용처 구분 참고 | Q21 확정, 실제 추출 코드 확인 한계는 3.5 |
| D21 | FMOD 효과음·바닥 소리·음악 | 비상업 전시, FMOD를 다뤄보는 학습 목적 | Q22 확정 |
| D22 | 자체 최소 CameraController | 세피리아·던그리드 추적 조절 참고 | Q19 확정 |
| D23 | Unity6.3 LTS 계열 | 선택 plugin이2022.3 전용일 때만 이전 버전으로 하겠다는 조건; 그런 제약이 발견되지 않음 | Q23 선택 규칙 적용, 설치·검증 미실행 |
| D24 | 백업 인자는 현재 미사용·bak1개, 검증/1회재시도/복원·앱종료대기 | 이유 미기록 | Q29 최신 답변, Q16의 N개 백업 가정을 대체 |
| D25 | 질문 문단을 Q별 현재 답변·연결 ID로 압축 | 검토할 내용을 간결히 정리하기 위한 사용자 요청 | 2026-11-15 후속 요청, 상세 사양은 1장에 유지 |
| D26 | 최적화 비교 후보를 선정하고 Unity Profiler 실측 후 성능이 나은 안 채택 | 프로젝트 결과의 선택 논리를 실측으로 강화 | 2026-11-15 후속 요청. 실험안은 3.1.1, 측정·대안 채택은 미실행 |
| D27 | 세부 시스템 조정 외에는 Boss 콘텐츠·FX/VFX 추가에 집중할 수 있는 기반 완성 | 이후 제작 작업을 콘텐츠와 연출에 집중하기 위함 | 2026-11-15 후속 요청. 상단 완성 목표, 실제 기반 구현은 미착수 |

정리 방식은 [기존 기록](archive/01_Choice/Notion_Organization.md), 이번 답변·이유·대안은 [Q1~Q29 선택 기록](archive/01_Choice/Notion_Answers_20261115.md), 사용자가 나중에 비교하기로 한 항목은 [비교·보류 기록](archive/02_Hold/Notion_Comparisons_20261115.md)에 연결한다. 원문 비교 전체를 임의로 보류 결정으로 바꾸지 않고, 현재 선택·비교 미실행·명시적 후속 검토를 구별한다.

### 3.3 연출·아트 요구와 정의 순서

- 사용자 확정: 연출 방식을 완전히 정의한 순간 구체 구현 부분을 정한다. 그 전에도 입력 차단·상태·시간·완료/중단 신호·피해 프레임 연결처럼 코드로 해결할 계약을 먼저 준비한다.
- 원문 규격: 1920×1080, 60FPS, `6×6 source px`, 팔레트 고정, 픽셀 재사용. 이 표기는 원문 그대로이며 월드 단위·PPU·피벗·실제 sprite 해상도는 아직 정하지 않았다.
- Map 크기 결정 순서: Player 캐릭터 이미지, 예시 Boss 크기, 무대, 복도/문, 수정. 아직 크기를 임의로 확정하지 않는다.
- 2026-11-15 별도 사용자 확정 — Player 정면 Idle 시안: 여성 인간형 동양풍 검사, 흑백 조합, 20×20px **이내**의 자연스러운 폭, 머리 약 전체 높이의 2/3, 무기·팔·손을 몸체에 표시하지 않음. Aseprite로 5종 이상 제작. 독립 회전 무기의 별도 손 점/손 생략 중 최종 방식, 최종 디자인, 전체 애니메이션 규격·PPU·피벗은 아직 선택하지 않음. [요청·이유 기록](archive/01_Choice/Player_Idle_Concept_Brief_20261115.md).
- 시안 결과: [6종 비교표](../art/player/idle_concepts_20x20_20261115/player-idle-comparison.png), [Aseprite 통합 원본](../art/player/idle_concepts_20x20_20261115/player-idle-concepts.aseprite). 각 20×20px 투명 캔버스, 실제 폭12~14px·높이18px, 머리12px(2/3), 팔·손·무기 없음. Aseprite 1.3.18.6에서 작성·저장 후 개별/통합 원본 재열기, PNG 픽셀 일치·이진 알파 검사, 1배·4배·12배 시각 검수를 완료함([검사 결과](../art/player/idle_concepts_20x20_20261115/validation.json)). 통합 파일의 6프레임은 디자인 대안이며 Idle 애니메이션이 아님. 최종 선택·나머지 방향·애니메이션·Unity 적용은 미완료. 이번 추가는 로컬 기록이며 Notion에 게시하지 않음.

- 후속 수정 방향 사용자 확정: **04번 머리 + 06번 의상**, 실제 높이 **20px**, 좌우 대칭 느낌의 의상, 망토처럼 흩날릴 수 있게 겉옷과 안옷을 구분함. [R01 미리보기](../art/player/idle_revision_01/player-front-idle-r01-preview-single.png) 및 [Aseprite 원본](../art/player/idle_revision_01/player-front-idle-r01.aseprite) 제작 완료. 캔버스20×20px·실제14×20px·머리13px(65%), 사선 백발 유지, 의상 좌우 픽셀 대칭, 팔·손·무기 없음. 머리/안옷·발/좌우 망토의 4레이어로 분리했고 망토 밑단은 발보다 1px 위에서 끝남. 원본 재열기·PNG 일치·실높이·의상 대칭·이진 알파 검사를 통과함([검사 결과](../art/player/idle_revision_01/validation.json)). 수정 방향의 선택과 완성 이미지의 최종 승인은 구분하며, 애니메이션·Unity 적용은 미실행. 이전 6종 원본은 보존함.

- 최신 방향 수정 — 사용자 확정: 정면 R01 대신 ETG 참고 이미지처럼 중심에서 왼쪽을 바라보는 시안 약10종을 요청했으며, 게임의 4방향을 **왼앞·오앞·왼뒤·오뒤**로 명시함. 이번 제작은 **왼앞 3/4 Idle 후보10종**이며 다른 세 방향이나 애니메이션10프레임을 제작한 것이 아님. 의상 구조의 대칭 느낌은 유지하되 3/4 시점에서 얼굴·앞섶·발·망토의 보이는 면적은 비대칭으로 표현함. 이전 정면 R01은 비교 이력으로 보존하며 현재 방향 기준으로 사용하지 않음.
- 최신 제작 결과: [왼앞10종 비교표](../art/player/idle_leftfront_concepts_10/player-leftfront-comparison.png), [Aseprite 통합 원본](../art/player/idle_leftfront_concepts_10/player-leftfront-10-concepts.aseprite), [전체 ZIP](../art/player/idle_leftfront_concepts_10/player-leftfront-10-concepts.zip). 각 투명20×20px·실제폭13~14px/높이20px·머리13px(65%), 백발·흑백 의상·분리 망토, 팔·손·무기 없음. 뒤 망토/안옷·발/앞 망토 앞섶/머리의4레이어. Aseprite 작성·개별 및10프레임 원본 재열기·PNG 일치·중복 없음·규격·이진 알파 검사를 통과함([검사 결과](../art/player/idle_leftfront_concepts_10/validation.json)). 1×/4×/10×에서 왼앞 방향 및 앞섶·망토를 시각 검수함. 최신10종 중 최종 선택, 오앞·왼뒤·오뒤·애니메이션·Unity 적용은 미실행. 로컬 기록만 갱신함.

- **위 최초 왼앞10종은 사용자 반려**: 사용자가 "실제 각도가 아니야. 완전히 다 깨져버렸잖아."라고 지적함. 앞선 시각 검수의 통과 판단을 철회하며, 파일 크기·알파·픽셀 일치 검사만 통과한 반려 시안으로 구분함. 얼굴 위치 이동과 길어진 얼굴/옆머리, 잘게 분리된 의상으로 각도와 형태를 충분히 표현하지 못함.
- 재작성 결과: [REDRAW02 비교표](../art/player/idle_leftfront_redraw_02/player-leftfront-comparison.png), [Aseprite 원본](../art/player/idle_leftfront_redraw_02/player-leftfront-redraw.aseprite), [전체 ZIP](../art/player/idle_leftfront_redraw_02/player-leftfront-redraw-10.zip). 기존10종을 변형·리샘플링하지 않고 연결된 전체 실루엣으로10종을 다시 그림. 얼굴 비례·옆면·볼/턱 윤곽, 이어진 망토 면, 같은 바닥선의 발을 조정함. 각20×20px·실높이20px·머리영역13px, 원본/PNG 기술 검사와 픽셀 연결 검사를 완료함([검사 결과](../art/player/idle_leftfront_redraw_02/validation.json)). 안옷·발/이어진 겉옷/머리3레이어. **새 수정안이며 사용자 각도·미술 품질 승인과 최종 채택은 아직 없음.** 기술 검사를 시각적 요구 충족으로 기록하지 않음.

- 별도 새 요청 결과 — 정면 얼굴·옆 귀/뒤통수 시안 V2: [6종 비교표](../art/player/idle_concepts_v2_20261115/player-idle-comparison.png), [Aseprite 원본](../art/player/idle_concepts_v2_20261115/player-idle-concepts.aseprite), [전체 ZIP](../art/player/idle_concepts_v2_20261115/player-idle-v2-6-concepts.zip). 현재 직접 요청에 따라 여성 인간형·흑백 동양풍·팔/손/무기 없음 조건으로 새6종을 제작함. 사용자 제공 Hunter ZIP의 같은 높이의 두 눈·얼굴 옆 귀·뒤통수 배치와 의상 이미지2장을 참고함. 과거 왼앞 후보를 덮어쓰거나 게임의4방향 정의를 변경한 작업은 아님.
- V2 규격·검사: 각20×20px 투명 캔버스, 실제폭12~15px·높이20px. 두상 기본영역13px(65%)이며 묶음머리/긴 머리끝은 아래로 연장됨. 의상/머리카락·윤곽/얼굴·귀3레이어. Aseprite 1.3.18.6에서 직접 픽셀을 작성하고 개별6원본·6프레임 통합 원본 재열기, PNG 일치·이진 알파·규격·중복 없음·픽셀 연결을 검사함([결과](../art/player/idle_concepts_v2_20261115/validation.json)). 1×·4×·12× 시각 검토 후03의 눈 옆 피부여백2px을 수정하고 재검사함. **6프레임은 서로 다른 시안이며 애니메이션이 아님. 최종 채택·사용자 미술 승인·Unity 적용은 미완료.** 로컬 기록만 갱신함.

| 자산 | 원문 요구·재사용 | 사용 위치 / 코드 연결 | 아직 필요한 정의·확인 |
| --- | --- | --- | --- |
| Title 로고·배경 | Draw 위, Slash 아래; l에 살짝 뽑힌 검; 연구소 흰 타일·깜빡이는 조명 | U03 Title | 원본 로고 참고, 실제 크기·움직임·palette |
| Player 몸체 | 왼앞·오앞·왼뒤·오뒤4방향 우선, 뒷걸음 역재생·후속8방향. 최초 왼앞10종 반려 후 REDRAW02 후보10종 재작성(위 결과 참조) | U01·P09 | 각도·미술 품질 확인 및 시안 선택·나머지3방향 및 애니메이션·피벗·게임 내 표시 크기·Unity 연결 |
| 고정 무기·일반 공격 | 검집채 공격, 축 회전과 FX로 방향 표현; 1/2콤보 검기 좌우 반전, 3콤보 X | P05·U01 | 사각 판정과 공격 프레임·FX 일치 |
| 대시 | 시작 시 Sprite 자세를 끝 위치에 표시, 끝에 가까울수록 촘촘한 잔상 | P09·U01 | 충돌 시 끝 위치·잔상 간격·투명도 |
| Draw 점·선·커서 | 어두운 화면에서도 보이는 pixel FX, 고정 길이 미리보기, 수명 후반 깜빡임 가속·만료 FX | P07·P10 | 동일 점 무효 처리의 표시·선 너비·수명 표시 |
| Slash | 발도 후 검을 넣는 순간 줌/UI/색 복원, 경로 검격 FX, 이후 행동이 모션 덮어쓰기 가능 | P08·U01·U07 | 게임 정지 중 실제 타격 프레임·선 FX |
| HUD | 좌하 HP, 우하 Draw/Slash, 좌중 그로기, 상단 Boss HP; HP 숫자 없음; 쿨타임 그림자/아래에서 위 선 | U02 | 화면별 위치·크기·아이콘·값 가독성 |
| HUD 이동 | 바깥으로 나갈 때 어두워지며 60FPS 기준 10프레임; 복귀는 역방향·원색 | U02·U07 | Boss 사망 Player HUD 유지·정지 중에도 완료 |
| 로딩 | 검은 화면·우하단 회전 링, 준비 지연 시 재사용 | U04 | 임계시간·오류 안내와 겹침 |
| Player 사망·부활 | 상하 검은 프레임, 사망 후 완전 검은 화면, 수정 앞에서 일어나며 화면 복원 | U01·U07·P04 | 컷씬 종료/초기화 신호·실제 Sprite |
| Boss·공격 예고 | N3 6컨셉과 38패턴, 탄환/반사 가능 구분, 장판·레이저·비프음 | B03~B06·U01·U08 | 경비 로봇5패턴 우선·금색=반사+각성·전조 길이 |
| Map·문·수정 | 연구소 실험방 PNG를 재사용하고 균열·사출구 Sprite를 겹침. 복도·전투장·문·수정 | S03·S04 | 월드 크기·이동 차단·Trigger 위치 |
| Ending | 마지막 Boss 사망 컷씬·스탭롤 후 Title | S01·U07 | 확정 순서에 맞춘 컷씬·크레딧·음악 |

원본 이미지 14개는 N4 상대 경로에 그대로 있다. 이번 작업은 텍스트 요구와 링크를 정리했으며 이미지 픽셀 규격·미술 품질은 검수하지 않았다. 제작 원본은 `/art`, 실제 적용 자산은 향후 `/project`에 둔다.

### 3.4 Plugin·asset·Unity 버전

공식 자료 확인일 **2026-11-15**. 사용 목적에서 도구를 선정하고 지원 교집합을 확인했다. 사용자 Q23의 선택 규칙에 따라 **Unity6.3 LTS 계열**을 사용한다. 현재 선택 도구에2022.3 전용 제약은 없으며 기존 프로젝트가 없어 이전 비용은 없다. 설치·신규 연결 비용은 발생한다. 프로젝트 생성·설치·구매와 실제 조합의 컴파일/빌드는 아직 하지 않았다.

| 도구·대안 | 용도와 현재 결정 | 도입·학습·연결 예상 / 비용·조건 | 버전 근거와 확인 한계 |
| --- | --- | --- | --- |
| UniTask | **채택**. 세이브 파일 저장과 Title UI 초기화에만 사용. 게임 생성·연출·기타 대기는 Unity 코루틴 | AI 추정1~3시간, MIT 고지 유지 | 2.5.11 릴리스·README 최소2018.4.13f1 확인. 6.3 전체 테스트 표는 없어 실제 compile 확인 필요 |
| 표준 Task·비동기 파일I/O | 비교 대안으로 보존, 별도 비동기 프레임워크 추가 안함 | 추정1~2시간, 별도 패키지 비용 없음 | UniTask 자체가 직렬화/암호화/백업을 해결하거나 동기I/O를 자동 백그라운드화하지 않음 |
| FMOD Studio + Unity Integration | **채택**. 표면별 발소리·공격/대시/Fever/Boss 효과음·음악, 학습 목적 | 단순 BGM·음량2~4시간, 처음 이벤트/Bank 학습까지4~8시간 이상 가능. 비상업 전시 EULA 조건·크레딧 준수 | Integration2.04.00은 Unity6.3 지원 명시, 일반 최소6.0. Studio/Integration은 같은2.04계열. 전체 음원 제작 시간은 별도 |
| AudioSource·AudioMixer | 기본 오디오 비교 대안, 현재 FMOD 대신 채택하지 않음 | 추정0.5~2시간, 음원 권리는 별도 | 음량/음악 소유자를 이중으로 만들지 않음 |
| 자체 CameraController | **채택**. 기본 추적·Draw 가중 중심·컷씬 이동/줌 | 필요한 동작만 코드 작성·확인, 패키지 비용 없음 | Sephiria/Dungreed 읽은 부분을3.5에 기록. 정확한 추적 시간·unscaled 동작은 구현 검증 필요 |
| Cinemachine | 조사한 대안, 현재 미도입 | 추정1~3시간 학습/연결; 패키지 LICENSE 확인 | 자체 Camera Transform과 중복 제어할 수 있어 동시에 연결하지 않음 |
| Unity 기본 SpriteRenderer·Animator·UI·선 표시 | 4방향·HUD·점/선·경비 로봇5패턴의 우선 도구 후보 | 추가 유료 팩보다 원문 디자인 재사용. 실제 제작/연결 시간은 에셋 규격에 의존 | LineRenderer는 선 표시 후보이고 픽셀 표현을 보장하지 않음. 유료 Sprite/FX asset은 아직 선정·구매하지 않음 |

근거: [UniTask2.5.11](https://github.com/Cysharp/UniTask/releases/tag/2.5.11), [패키지 메타데이터](https://github.com/Cysharp/UniTask/blob/2.5.11/src/UniTask/Assets/Plugins/UniTask/package.json), [README·사용 방식](https://github.com/Cysharp/UniTask), [MIT License](https://github.com/Cysharp/UniTask/blob/master/LICENSE). 최소 지원 버전과 최신 Editor에서 실제 빌드 검증은 구별한다.

[FMOD2.04 지원표](https://www.fmod.com/docs/2.04/unity/welcome.html), [2.04 변경 사항](https://www.fmod.com/docs/2.04/unity/welcome-whats-new-204.html), [연결 안내](https://www.fmod.com/docs/2.04/unity/integration-tutorial.html). 2.04 표의2022.3은 Tuanjie 대상이며 일반 Unity2022.3 지원으로 읽지 않는다. [FMOD2.03 표](https://www.fmod.com/docs/2.03/unity/welcome.html)는2022.3과6.3을 지원하지만 현재 내려갈 필요는 없다. [FMOD EULA](https://www.fmod.com/legal)의 비상업 사용 조건과 FMOD / Firelight Technologies Pty Ltd. 크레딧을 제출에 반영한다. 음원 자체의 사용 조건은 별도다.

Unity6.3 LTS의 공식 공개 패치 예로 [6000.3.28f1](https://unity.com/releases/editor/whats-new/6000.3.28f1)(2026-11-03)을 확인했다. 이를 설치된 버전이나 절대 최신 패치라고 기록하지 않는다. 개발 착수 시 실제 설치 가능한6.3 LTS 패치를 기록하면 된다.

**사각 판정 API**: 사용자 CastNonAlloc 요구는 재사용 결과 배열에 채우는 사각 Cast이다. [Unity6.3 Physics2D.BoxCast](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Physics2D.BoxCast.html)의 ContactFilter2D·RaycastHit2D[] 오버로드가 이를 지원한다. [2022.3 BoxCastNonAlloc](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Physics2D.BoxCastNonAlloc.html)은 향후 폐기 안내와 BoxCast 권고가 있으나6.3에서 무조건 제거됐다고 단정하지 않는다. 0거리 Cast와 시작 중첩의 결과가 OverlapBox와 동일하다고 가정하지 않고 실제 시작 중첩·회전·버퍼 포화·Gizmo 일치를 확인한다. 사용자 요청을 임의로 Overlap 판정으로 교체하지 않는다.

### 3.5 레퍼런스 활용과 확인 한계

현재 `docs/reference`에는 Dungreed·Sephiria 추출 코드가 존재한다. 과거 STATUS의 부재 기록을 현재 사실로 사용하지 않는다. 원본 게임 버전·추출 완전성·실행 결과는 각 README에서 미검증으로 명시되어 있다.

| 실제 읽은 파일·위치 | 확인한 책임 | 현재 게임에 필요한 최소 채택 제안 / 생략 이유 |
| --- | --- | --- |
| [Dungreed Status](<reference/dungreed/code/ingame code/Assembly-CSharp/Status.cs>) 6~70행 | HP 등 수치·계산 | 수치와 행동 상태를 구분. 범용 수치 라이브러리 복제는 불필요 |
| [Dungreed Creature](<reference/dungreed/code/ingame code/Assembly-CSharp/Creature.cs>) 396~430·1004·1065~1079행 | Status 보유, 죽음·무적 확인 후 피해 적용 | Character 피해 허용 판정 참고. 현재 Character 집중 보유 선택을 자동 변경하지 않음 |
| [Dungreed CharacterController2D](<reference/dungreed/code/ingame code/Assembly-CSharp/CharacterController2D.cs>) 696·851·918행 | 행동 enum·대시 상태 | 상태 책임 참고. 횡스크롤 점프·중력 생략 |
| [Sephiria UnitAvatar](<reference/sephiria/code/ingame code/Assembly-CSharp/UnitAvatar.cs>) 151~224행 | 수치 조회·변경 API | 조회 책임만 참고. 서버·SyncDictionary·대규모 스탯 DB 생략 |
| [Sephiria WeaponSimple](<reference/sephiria/code/ingame code/Assembly-CSharp/WeaponSimple.cs>) 451~485·525·820·829행 | 콤보 idx별 공격·캐릭터 수치 조회 | 콤보를 공격 담당에 두는 후보. 무기 교체·획득 생략 |
| [Sephiria WeaponControllerSimple](<reference/sephiria/code/ingame code/Assembly-CSharp/WeaponControllerSimple.cs>) 479~508행 | 공격 단계·animation·이동 차단 연결 | 실행과 표현 연결만 참고. 서버/클라이언트 분기 생략 |
| [Sephiria CharacterBuff](<reference/sephiria/code/ingame code/Assembly-CSharp/CharacterBuff.cs>) 193~227·252~272행 | 효과 초기화·종료·중복 적용 방지 | 원문 수명·대상·효과 책임과 대응. 네트워크·복잡한 중첩 체계 생략 |

추가로 Q5·Q6·Q11·Q16·Q19·Q21에서 요청한 실제 코드를 확인했다. 아래는 실행 결과가 아닌 추출 소스 관찰이다.

| 실제 읽은 파일·위치 | 확인한 내용 | 채택할 부분 / 복제하지 않을 부분 |
| --- | --- | --- |
| [Sephiria PlayerInputController](<reference/sephiria/code/ingame code/Assembly-CSharp/PlayerInputController.cs>) 317행, [PlayerSpawner](<reference/sephiria/code/ingame code/Assembly-CSharp/PlayerSpawner.cs>) 291행 | Input이 Player 기능을 조회·호출, 생성 후 입력·카메라 연결 | 단방향 의도 전달·생성 뒤 연결. 네트워크 권한·아이템·무기 선택은 생략 |
| [CutScenePlayer](<reference/sephiria/code/ingame code/Assembly-CSharp/CutScenePlayer.cs>) 48행 | 컷씬 입력 차단, 기존 이동·공격·캐스팅 종료 | 전역 입력 사건과 내부 행동 정리 구분 |
| [Hitbox](<reference/sephiria/code/ingame code/Assembly-CSharp/Hitbox.cs>) 8행, [MeleeCollision](<reference/sephiria/code/ingame code/Assembly-CSharp/MeleeCollision.cs>) 168·483행 | Collider 대상에서 피해 수신자로 연결, attackedInSwing으로 중복 방지, 결과 버퍼10 | 수신자 매핑·공격별 중복 방지. 고정10개를 검증 없이 그대로 채택하지 않음 |
| [MeleeCollision_Rectangle](<reference/sephiria/code/ingame code/Assembly-CSharp/MeleeCollision_Rectangle.cs>) 8·33행 | BoxCastNonAlloc에 박스 회전·크기·offset, 0방향/0거리 사용 | 재사용 Cast 참고. Gizmo는 offset만 회전하고 박스 회전은 맞지 않을 수 있어 그대로 복제하지 않음 |
| [Bullet](<reference/sephiria/code/ingame code/Assembly-CSharp/Bullet.cs>) 197행, [TopdownSpatialHash](<reference/sephiria/code/ingame code/Assembly-CSharp/TopdownSpatialHash.cs>) 60행 | 다른 경로는 공간 해시/OverlapCollider, 공간 해시 검사는 대상 중심점 OverlapPoint이며 버퍼 만충 시 반환 | 모든 피격이 같은 Cast라는 설명은 하지 않음. 현재 규모에 공간 해시·중심점 판정을 추가하지 않음 |
| [Sephiria SaveManager](<reference/sephiria/code/ingame code/Assembly-CSharp/SaveManager.cs>) 103~188행, [HandleSaveProgress](<reference/sephiria/code/ingame code/Assembly-CSharp/HandleSaveProgress.cs>) 8행 | 최신 대기 복사본 하나씩 덮어두는 방식, UniTask.RunOnThreadPool 호출, LateUpdate 완료 처리 | 비동기 작업/완료 관찰만 참고. FIFO 큐라는 설명은 틀리며 async 상태기계 본문이 없어 세부 저장 순서는 확인 불가 |
| [Sephiria SaveData](<reference/sephiria/code/ingame code/Assembly-CSharp/SaveData.cs>) 47~63·263~386행 | 얕은 Dictionary 복사, timestamp .sav 백업 최대20, .saving 기록 뒤 원본 삭제·이동. 복구 루프에서 후보 대신 BindedPath를 읽는 코드 | 우리 .sav/.bak1개·검사·재시도 규칙과 다름. 가변값 얕은 복사·삭제후이동·잘못된 복구 경로는 그대로 적용하지 않음. 원본 실행 버그로 재현한 것은 아님 |
| [TitleSceneUIControlnitializer](<reference/sephiria/code/ingame code/Assembly-CSharp/TitleSceneUIControlnitializer.cs>) 28·155·191행, [UI_SaveProfilePanel](<reference/sephiria/code/ingame code/Assembly-CSharp/UI_SaveProfilePanel.cs>) 8행, [UI_TitleLobby](<reference/sephiria/code/ingame code/Assembly-CSharp/UI_TitleLobby.cs>) 76행 | UniTaskVoid 시작과 대기, 파일 존재·정상/오류/빈 슬롯, 새 게임 확인 | Title 준비·상태 표시 참고, 클라우드 생략. 일부 파일 읽기는 동기이므로 UniTask 사용=모든 파일 비동기라고 하지 않음 |
| [Sephiria GameCamera](<reference/sephiria/code/ingame code/Assembly-CSharp/GameCamera.cs>) 111행, [TargetTracker](<reference/sephiria/code/ingame code/Assembly-CSharp/TargetTracker.cs>) 51·137행 | 관찰 대상·마우스 offset·방 경계·지수형 추적, 멀면 순간 배치 | 최소 target/offset/경계 참고. 정확한1초 도착 보장 아님, scaled/unscaled는 우리 Q9로 정리 |
| [Dungreed PlayerTracker](<reference/dungreed/code/ingame code/Assembly-CSharp/PlayerTracker.cs>) 34·146·154행 | target+offset+경계. trackTime=0.5 미사용, speed 인자 대신 상수 사용 | 최소 추적과 연출 전환만 참고. 0.5초 추적이라고 해석하지 않음, 2인·층·로딩 연결 생략 |

원본의 서버·멀티플레이·무기 교체·대규모 상태 체계는 현재 게임으로 옮기지 않는다. 현재 선택의 성능 우위나 실행 정상 여부는 미검증이다.

### 3.6 다음 단계

1. Q1~Q29 답변과 정의·케이스 범위·선택 이유를 통합했고 현재 답변 대기는 없다. 신규 API 이름은 Q4에 따라 기능별 구현 결과를 보고 사용자가 수정할 수 있다.
2. 문서 단계의 결과를 기준으로 **개발 단계 착수 요청을 받으면** Unity6.3 LTS 실제 패치와 패키지 버전을 기록하고 프로젝트를 구성한다. 이번 문서 작업에서는 생성·설치·코드 작성을 하지 않았다.
3. 2장 각 케이스마다 실제 코드와 함께 class.API/변수 기반 상세 흐름을 작성한다. 동시는 +, 순서는 →, 분기는 if : 하위 목록. 호출·데이터 연결과 아트 없이 가능한 동작 확인을 함께 남긴다.
4. 연출이 정의된 항목에 Sprite·FX·animation·음원을 적용하고 실제 자산에 의존하는 크기·피벗·타이밍 조정을 마친다. 검증은 구현 중부터 반복한다.
5. 공통 기능이 동작해 비교 가능한 기준선이 생기면 3.1.1의 Profiler 비교를 수행한다. 기능 동등성·실측 결과·채택 이유를 남겨 콘텐츠·연출 추가에 집중할 기반과 결과 보고의 근거를 함께 보강한다.
