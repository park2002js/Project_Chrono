# 구현 책임별 레퍼런스 코드 지도

확인일: 2026-11-16. 기준 사양은 [GAME_SPEC.md](GAME_SPEC.md)의 P01~P10, B01~B08, U01~U09, S01~S13 **40개 정의**이며 B03의 여섯 보스와 S13의 데이터 enum도 포함한다. 이 문서는 실제 본문을 읽은 **73개 원작 코드 경로**를 찾아가는 지도다. 기능 사양·구현 완료 여부의 원본은 GAME_SPEC에 둔다. F-S01·F-P01·F-P02·F-P03 일반 공격·공통 피해 API의 확인 이력과 F-P04 원본 Unity 컴파일·41묶음 Play Mode/임시 화면 확인(2026-11-15 19:45:51 KST), 이후 점 편집 확장 확인·Player 원본/Skill 수명·만료 표현 변경과 미구현 전투 기능을 구분한다. 이전29묶음은 GAME_SPEC 2.1.2, F-P03 초기35묶음과 책임 정리 뒤35묶음(2026-11-15 18:30:34 KST)은2.1.3, F-P04 당시 기준선은2.1.4, F-P05 전체 편집은2.1.5, F-P06 수명·소비 경계는2.1.6, F-P07 구현·검증은2.1.7, F-P08은2.1.8, 신규 F-P09 생성/사망/부활·F-P10 수정·F-P11 고정 효과는2.1.9~2.1.11의 구현/최신 확인 상태를 따른다. U06은 사용자 요청의 이름인 **GameCamera**로 표기한다.

## 사용 순서와 확인 범위

**주요 갱신 실행 순서 분석(2026-11-16):** Player/Boss/UI/월드·시스템 변경 전 [전체 실행 순서 비교](archive/03_Analysis/Update_Order_Comparison_20261116.md)를 함께 확인한다. 우리44파일과 원작74코드/meta의 실제 콜백·순서, 수동/사건/코루틴·상속과 예정 기능을 구분했다. 변경 전 원작 Body/몸체 Animator는 Update, 우리 Body/관찰은 FixedUpdate였던 차이 외에 Buff30 소비·쿨 경계·공격 Late/클립 사건·HUD Late 등의 차이를 D01~D14로 기록했다. 원작과 다르다는 사실만으로 변경을 승인하거나 정적 후보를 관측 오류로 간주하지 않는다. 이후 사용자 요청으로 Body.Move 즉시 적용·Rendering.Update(3)로 변경했으며 현재 계약은 아래 P03·P09·U01과 GAME_SPEC2.1.1을 따른다.

**공격 애니메이션 에셋 분석(2026-11-15):** F-P03·AttackController·CharacterRendering의 후속 공격 모션 제작은 [세피리아 대검 1·2타 분석](archive/03_Analysis/Sephiria_GreatSword_Attacks_20261115.md)을 함께 읽는다. 현재 경로는 `WeaponSimple_GreatSword → WeaponControllerSimple → AnimationWrapper.controller` 레이어2의 `GreatSword_Attack1/2.anim`이다. 보유 `GreatSword_New.controller`의 구형 공격과 혼합하지 않는다. `WeaponShoulder`의 피벗, `NewWeaponFireData → SpriteFx → Animator2D_Basic/Timer`의 검기 시간을 포함한 실제 파일 경로·행·GUID·해시는 분석 문서9장에 있다. 원작 수치의 분석 완료와 우리 공격 모션 적용은 별개다.

1. 작업할 **클래스 ID와 기능 ID, 변경할 메서드의 책임**을 GAME_SPEC에서 확인한다. 같은 클래스의 일부가 구현되어 있어도 다른 기능까지 완료된 것으로 보지 않는다.
2. 아래 Sephiria 파일의 해당 메서드 본문을 먼저 읽는다. 호출자·피호출자, 상태 필드, 종료·취소 처리까지 따라가고 필요한 경우 Dungreed 보조 파일을 비교한다. 메서드 이름이나 선언만으로 구조를 결정하지 않는다.
3. `static Instance` 또는 MonoBehaviour이면 `Awake → OnEnable → Start`와 생성 호출자, `.meta`의 executionOrder, 실제 씬·프리팹 배치를 함께 확인한다. 소비자보다 오래 유지되는 참조는 준비 순서를 정한 뒤 한 번 확보하고 재사용한다. 초기 활성 씬 객체의 Awake→Start 보장을 동적 생성·늦은 활성화에 그대로 적용하지 않는다. 준비 전 접근, 불필요한 프레임 반복 조회·생성, 중복 생성·파괴·재진입 시 기존 인스턴스나 캐시 손상을 검사한다.
4. **소규모 싱글 플레이 보스 러시·고정 무기·확정 능력**에 필요한 책임만 적용한다. 서버 권한/RPC, 인벤토리·무기 교체, 능력 획득, 대규모 데이터베이스·범용 FSM을 함께 들여오지 않는다. 원작과 우리 사양이 다르면 우리 사양을 따른다.
5. 실제 변경에서는 참고한 메서드와 채택·생략 이유를 GAME_SPEC의 해당 구현 기록에 남기고, 호출 순서·누락 참조·해제 경로를 검증한다. Sequence Diagram과 Notion 기록은 [AGENTS](../AGENTS.md)·[DESIGN](agents/DESIGN.md)의 기존 절차를 따른다.

**읽은 자료:** 아래 링크는 모두 저장소에 있는 `code/ingame code/Assembly-CSharp`의 실제 C# 본문이다. 표에 적은 메서드의 관련 본문·필드·호출 관계를 확인했다. 대형 파일 전체의 모든 분기나 원작 게임 실행을 검증했다는 뜻은 아니다. Unity 자동 생성 RPC/직렬화 코드는 원작 전송 구조의 흔적으로 취급한다. `assets`의 직렬화 데이터는 초기화 순서·배치 확인 보조이며 빈 스크립트 덤프를 구현 근거로 사용하지 않는다.

**미확인·동일 구현 없음의 의미:** Draw/Slash의 전체 규칙, Boss_01~06의 고유 패턴과 그로기 규칙, 우리 체크포인트의 회복·저장·문 연결을 그대로 수행하는 단일 원작 클래스는 조사 범위에서 확인하지 못했다. 아래에는 확인한 부분 책임만 연결한다. Dungreed의 `—`는 보조 본문을 추가로 확인하지 않았거나 이번 책임에서 필요한 근거가 없다는 뜻이며, 그 게임에 해당 기능이 없다는 단정이 아니다.

## Player — P01~P10

| 정의·관련 기능 | 우리 책임·범위 | Sephiria 파일과 확인한 근거 | Dungreed 보조 | 적용·생략 |
| --- | --- | --- | --- | --- |
| P01 Character · F-P03/08/09/11 | 최종 피해 허용·HP/IsDead·호출별 치명 snapshot과 OnDamaged/OnDie 통지와 Revive 상태 확정/OnRevive | [UnitAvatar.cs][s-unit] `Awake`, `ApplyDamage`1336, `ForceDie`2053, `Revive`2301, `CancelCurrentAction`2799: 수신 상태 검사·사망 전 진행 행동 정리  [UnitAvatar.cs][s-unit]의 Update1027·피격 보호 검사1445/부여1982, OnDamagedServerside1878·client5976; | — | Character는 상태 확정/사건만, Player는 보호 기한, Rendering은 사건 구독/색 표시. Hitbox 피해는 Dodge 또는 IsHitProtected, 전면 무적은 효과 포함 거부. 원작 Effect 보호 부여 제외와 달리 우리 실제 HP 감소는 보호 갱신. Revive도 상태만 확정하고 구독자가 자기 행동/효과/배치 복원. 렌더·Save 직접 호출·네트워크/방어·가드/저항·원작 부활 보호 미도입 |
| P02 PlayerInputController · F-P01~07/10, F-S04, F-U08 | 독립 입력 캐시·전달 판단과 Player/Slash F·Draw/F 구분·전용 잠금 | [PlayerInputController.cs][s-input] `Awake`, `HandleOnMove/Aim`, `HandleOnDash`351, `HandleOnFire`387, `Update`, `BlockAvatarInput`, `OnDisable`, `SetPlayerObject`: 입력 허용 판단·차단/교체·해제 시 StopAttack | — | HandleOnSlash→Character 공통 허용. Draw는 시간 게이트 전 별도 라우팅, Slash는 일반 요청 정리/잠금. 맵 전환 때 F 확정이 일반 Slash로 누출되지 않도록 캐시 정리. F-P10은 InteractionTarget 있으면 ApplyInteraction, 없으면 Slash. 거부 요청의 F를 Slash로 재전달하지 않음. 후보 사건은 UI가 자기 안내로 소비. 네트워크·조이스틱/슬롯 제외 |
| P03 PlayerCharacter · F-P01~11 | Update(0) 이동 계산·Body.Move 즉시 호출·일반 변위/조준 전달·공통 시작·중단 사건·Line 원본/Slash 보호·Groggy와 독립 피격 보호 기한 | [UnitAvatar.cs][s-unit] `Move`, `Stop`, `Dash`3233, `Update`1177~1183, `ForceDie`2053·`CancelCurrentAction`2799; [CharacterDash_FastMove.cs][s-dash-fast] `OnDashBegin`, `DashMoveUpdate`; [PlayerAvatar.cs][s-player] `StartAttackClientside`835: CanMove 후 무기로 전달 | — | OnDamaged가 보호 기한/기간을 확정하고 bool 진입/종료 사건만 통지. Remaining/Elapsed는 scaled 절대 기한에서 계산해 Update 전 요청도 최신 만료를 보며 Rendering에 직접 색을 지정하지 않음. 대시/전면 무적과 별도 상태·명시 사망/비활성/ResetMotion 정리. F-P08 검증은2.1.8. 자기 OnRevive에서 행동/Line/게이지 복구, F10은 activeInteraction·완료 구독/전면 보호·이동 잠금만 소유하고 수정이 완료/취소를 알림 |
| P04 PlayerSpawner · F-S01, F-P02/09 | Awake 저장 상태·위치 캐시, Start 준비/사건 구독, 생성/사망/기상 후속 | [PlayerSpawner.cs][s-spawner] `Awake`, `OnStartAuthority`, `RemoveAuthority`, OnDie/OnRevive 구독89/133·HandleDieServerside736·HandleDieClientside759·HandleReviveClientside780: 자기 구성/연결·사건별 후속; [CharacterDash.cs][s-dash] `StopDash`의 취소 경계 참고 | [GameManager.cs][d-gm] `CreatePlayer`: 생성 뒤 연결·저장값 적용 | Map 검증 뒤 복제, Awake가 Current·Map Instance로 초기화/배치. playerSpawn의 ResetMotion은 쿨타임까지 초기화하며 별도 CancelDash는 쿨타임 유지. Start 준비→최초 기상→입력. 저장 HP0은 생성 게이트 안에서 DeathCount 없이 Revive, OnRevive 중 취소는 콜백 뒤 소유 재확인으로 UI 재열림 방지. 사망은 횟수 Save→자기 정지/표시→전투 복구→Revive→캐시 배치. 같은 Player/연결·기존 pause 보존. 원작 협동 GameOver/장비/2P 제외 |
| P05 AttackController · F-P01/03, F-U06 | 고정 무기 방향·3콤보·공격 시간·판정·Animator 요청·중단 | [WeaponControllerSimple.cs][s-weapon-simple-controller] `CancelAction`137, `AttackButtonDown`337, `BeginAttackAnimation`479, `UpdateAnimatorAttackSpeed`1020; [WeaponSimple.cs][s-weapon-simple] `StartBasicAttackAnimation`285·판정449; [AnimationWrapper.controller][s-animation-wrapper]·[GreatSword_Attack1.anim][s-greatsword-attack1]·[GreatSword_Attack2.anim][s-greatsword-attack2]·[GreatSword_Attack3.anim][s-greatsword-attack3]의 실제 궤적 | — | 몸체/무기 분리와 위치·Euler 곡선을 채택. WeaponPivot 조준 아래 WeaponMotion만 Animator가 변경하며4클립을 기존 AttackDuration/AS에 맞춤. 판정·홀드·콤보·취소는 코드가 유지하며 클립 피해 이벤트 없음. Awake에 Animator/클립 길이 확보, 취소 후 Idle 즉시 복구. 서버·무기 교체·DB·대시 공격 생략. 이관 확인 상태는 GAME_SPEC3.3.11 |
| P06 SkillController · F-P04/06/07 | Draw/Slash 자체 모드·쿨·전환·Player 원본 수명/한 번 소비와 Slash unscaled 시점.2.1.7 확인 | [SkillController.cs][s-skill] `LocalCastStart`261·`OnBeginCastAnimation`379·`OnEndCastAnimation`457·`CancelAction`356; [WeaponSkillController.cs][s-wskill] `OnWeaponSkillButtonDown`218; [MeleeCollision.cs][s-melee] `OnUpdateServer`49의 시간 종료; [BulletBlinkBeforeDestroy.cs][s-line-blink] `OnSpawn`41·`Update`48의 잔여 비율 기반 가속 예고; [FlameGroundFadeFx.cs][s-line-fade] `Update`8·`OnSpawn`29의 재생 초기화·종료 정리 | — | 공통 시작은 Character, 모드/시간은 Skill, 원본은 Player, 기하는 Drawer, snapshot/1회 질의는 Slasher, 표현은 DrawVfx. 시작 소비→마지막 정지 판정→자기 복구→성공 flash. 같은 원작 Line 시스템은 미확인이며 시간/예고/대칭 정리만 참고. 스킬 슬롯·네트워크·풀·원작 수치 제외 |
| P07 LineDrawer · F-P04/05 | 임시 점·고정 길이 미리보기/확정 기하. F-P05 전체 편집은 최신48묶음 확인·2.1.5 참조 | **동일 구현 미확인.** [SkillController.cs][s-skill]의 시작/취소 경계만 부분 참고 | — | 우리 Q7~9로 직접 구현. 임시 기하만 계산하고 확정 원본은 Player, 저장·수명·만료 요청은 Skill, 표시만 DrawVfx에 맡긴다. 재Draw 취소 시 기존 Line 보존. 원작 캐스팅을 선 그리기 구현으로 간주하지 않음 |
| P08 LineSlasher · F-P07, F-U06 | 소비 Line snapshot·공용 AttackId·QuerySlash1회와 중단 기록. F-P07 확인 | [MeleeCollision_Rectangle.cs][s-melee-rect] `CheckCollision`, [MeleeCollision.cs][s-melee] `Attack`, [BulletBreaker.cs][s-breaker] `Update`: 사각 질의·중복 대상 관리·탄환 제거 | — | 사각 질의/수신자 중복·탄 분기만 부분 참고. Skill이 시간/전환을 관리하고 Slasher는 마지막 정지시점1회 판정만 담당. 원작 매 Update 서버 질의와 달리 Boss목표 반사·Fever·부위 집계는 우리 Q12/13 |
| P09 PlayerBodyController · F-P01/02 | 유한 변위의 즉시 충돌 검사·위치 적용과 마지막 실제 결과 진단 | [TopdownRigidbody.cs][s-body] RequestMove322·Update183/275~277·Move328/392: 원작의 누적·렌더 Update 소비·충돌 보정·Translate | — | Character.Update(0)→Body.Move 즉시 적용. 요청 큐·FixedUpdate·Body 실행 순서 Attribute 제거. Rendering은 Body 결과를 읽지 않으며 ActualMovement/MoveDirection은 마지막 호출의 진단값만 유지. 피해/표시·대시 시간·외력·구덩이/RPC 미도입 |
| P10 Line(struct) · F-P05/06/07 | 시작점·단위 방향·고정 길이/폭의 값 데이터, Player.CurrentLine에 nullable 원본으로 보관 | **동일 데이터 타입 미확인.** [MeleeCollision_Rectangle.cs][s-melee-rect] `CheckCollision`의 크기·각도·오프셋 계산만 소비 지점의 부분 참고 | — | 두 번째 클릭 거리는 방향에만 사용. struct이며 수명은 Skill, 표현은 DrawVfx에 분리. 표시 프리팹·타이머·전역 전환을 값에 넣지 않음 |

**기본 대검3타의 몸체 연결:** [GreatSword_Attack3.anim][s-greatsword-attack3]305·340행의0초 `GreatSwordSwing_Start`/0.76666665초 `GreatSwordSwing_Stop` → WeaponControlAnimator.CustomAnimationRequest20 → WeaponControllerSimple.CustomAnimationRequest956 → [PlayerAvatar.cs][s-player]1459·1479의 Swing 플래그로 이어진다. SetAnimationTransition381·394는 조준에 따라 LOWER/UPPER10장·20fps를 선택하고815~816의 공격속도 delegate를 적용한다. SWEEP422~426은 별도 플래그·7장30fps다. 우리3타는 무기 궤적만 이관했고 몸체 아트·요청 이벤트는 생략했다. [전체 근거4.2](archive/03_Analysis/Sephiria_Player_Animation_Architecture_20261116.md#42-대검-공격과-몸체-공격)를 확인한다.

## Boss — B01~B08

**F-B01~06 갱신(2026-11-16)**: 공통 전투 수명·선택/실행 객체와 DummyBoss4패턴, Projectile4종의 상속 연결을 구현하고 Unity6000.3.21f1의 컴파일·96묶음 실행 검사를 통과했다. 세부 결과·실제 상속표·추가 절차는 GAME_SPEC2.2.1~2.2.6을 따른다. 아래에 신규로 읽은 레이저3파일을 포함하며 경비 로봇5패턴의 콘텐츠 완료로 표시하지 않는다.

| 정의·관련 기능 | 우리 책임·범위 | Sephiria 파일과 확인한 근거 | Dungreed 보조 | 적용·생략 |
| --- | --- | --- | --- | --- |
| B01 BossCharacter · F-B02~05 | 조건 선택 요청·현재 실행·즉시 취소·부위 집계·단일 그로기·재도전 | [UnitAvatar.cs][s-unit], [UnitAI_BossBasic.cs][s-boss-ai] Awake58·ActivateDramaticDie83·ActivatePhaseChange92·CheckDeadlyDamage180: 캐릭터와 AI/연출 분리 | — | Awake는 자기 Selector/구체 패턴만 준비하고 PatternsInitialized 확인 뒤 Spawner 초기화. 전투 시작에서 Target 캐시. HandlePatternStarted/Completed는 외부 콜백 전에 현재 참조·이력만 갱신. 그로기 때 패턴 Cancel만 요청하고 네트워크·다중 목숨 제외 |
| B02 BossSpawner · F-S01, F-B01 | 생존/처치 상태 초기화·배치·재도전 복구 | [BossSpawner.cs][s-boss-spawner] `OnStartServer`: prefab 생성·수치·방향·AI 연결; `Update`: 전투 구역/도전자 관리 | — | 생성 책임은 StageManager에 두고 초기화·복구를 분리. 원작 네트워크 인원 HP 배율·동료·드롭·진행 난이도 제외. 사체/재도전은 우리 저장 정책 |
| B03 Boss_01…Boss_06 · F-B07~12 | 보스별 실제 패턴. **01 현재 제출, 02~06 후속** | [UnitAI_ArmadilloGolem.cs][s-armadillo] `OnAIUpdate_FoundEnemy`, `RestockPatterns`; [UnitAI_Askard.cs][s-askard-ai] `OnAIUpdate_FoundEnemy`, `BeginPhaseChange`; [Unit_Askard.cs][s-askard] `StartTentacle_Point`, `CancelCurrentAction` | — | AI가 행동 실행체에 요청하고 실행체가 타이밍/중단을 관리하는 구성을 참고. 우리 여섯 보스와 원작 개체가 동일하다는 뜻은 아님. 아래 세부 대응 확인 |
| B04 BossPattern · F-B03/04/07~12 | 조건·가중치·우선값·Begin/Tick/Cancel·사용 이력 | [UnitAI_Askard.cs][s-askard-ai] OnAIUpdate_FoundEnemy47; [Unit_Askard.cs][s-askard] CancelCurrentAction210·StartHomingBullet1499·HomingBulletCoroutine1519·StartLaser1588 | — | 예고→연속 발사→회복→Idle을 작은 C# 패턴으로 분리. 원작 동일 독립 BossPattern은 미확인. **원작 취소의 DespawnAllBullet238은 생략**하고 그로기는 미래 발사만 중단. 서버·코루틴/셔플 목록은 그대로 복사하지 않음 |
| B05 Projectile · F-P07, F-B06~12 | Normal/Gold의 비행·반사, Area/Laser의 전조·범위 일회 피해·공통 수명 | [Bullet.cs][s-bullet] AttackOnServer345·ApplyDamage528·DestroySelf549·TurnOnHoming581; [BulletDestroyModule.cs][s-bullet-destroy] OnDestroyRequestReceived62; [BulletMoveModule_Laser.cs][s-laser] Initialize35·OnMove49·SetLaserLength118; [AskardDarknessLaser.cs][s-darkness-laser] Update37·Initialize87; [AskardLaserWarning.cs][s-laser-warning] Fire18·TurnOff24·Update37; [Unit_Askard.cs][s-askard] 생성565/1674·활성 지연570 | — | 공통 Projectile→Normal→Gold, Projectile→Area→Laser. 종료 상태 선설정·전조/피해 분리·재사용 질의 배열 참고. 반사는 Owner 추적·Fever/게이지1회, Area/Laser는 Slash Ignore·한 발사당 Player 성공피해1회. 네트워크·풀·공간해시·원작 반복피해·벽 자동 길이 조정 생략 |
| B06 BossStructure · F-P07, F-B06/08~12 | 독립 HP·일반 허용·Slash 부위 보고의 최소 수신자. F-P07 지원 확인 | [BreakableProp.cs][s-prop] Awake108·ApplyDamage184·OnBreak의 수신/종료; [Hitbox.cs][s-hitbox] GetCombatBehaviour8·Awake18의 수신자 분리 | — | 구조물 독립 반응과 Boss AttackId/PartId 집계 분리. 동시 성공은 Boss 책임. 원작 드롭·랜덤 ID·네트워크·파괴 저장 제외 |
| B07 EncounterTrigger · F-B02 | 실제 진입 전달·Stage의 첫/재조우·컷씬 완료 뒤 저장/문/HUD | [BossSpawner.cs][s-boss-spawner] Update·EnterEventCoroutineServerside: 진입 연출 종료 후 AI/전투 활성화 | — | Trigger는 Initialize와 OnTriggerEnter2D만 보유. Stage가 중복·양쪽 사망 구독·첫 완료 기록·연출 중단을 담당. 원작 좌표 구역 반복 검사/서버 권한 생략 |
| B08 BossPatternSelector · F-B03 | 조건·직전 제외·최고 양수 Priority끼리 가중 추첨·null Idle | [UnitAI_Askard.cs][s-askard-ai] OnAIUpdate_FoundEnemy47·GetPatternIndex117; [UnitAI_ArmadilloGolem.cs][s-armadillo] RestockPatterns209: 중복 항목·shuffle | — | 원작 셔플 목록을 동일 구현으로 설명하지 않음. GetPatternIndex는 주요 선택 경로에서 미호출. 우리 등록 시 버퍼 준비·순회 중 후보/가중치 재사용, 실제 시작만 이력 기록·재도전 초기화. Dummy HP/거리/다른 패턴 횟수는 시험 조건 |

### B03 보스별 부분 대응과 후속 경계

| 우리 보스 | 확인한 원작의 유사 책임 | 동일하지 않은 부분·적용 범위 |
| --- | --- | --- |
| Boss_01 경비 로봇 · 현재 5패턴 | [UnitAI_ArmadilloGolem][s-armadillo]의 목표 위치 기반 경고/미사일 시간 처리, [UnitAI_Askard][s-askard-ai]의 상태 확인 후 패턴 요청 | 좌우 탄·손 추적 낙하·내려침·눈 레이저·교대 부채꼴의 동일 세트는 확인하지 못함. 원작 이름이 로봇과 비슷하다는 이유로 패턴을 교체하지 않음 |
| Boss_02 군용 로봇 · 후속 6패턴 | [UnitAI_ArmadilloGolem][s-armadillo] `OnAIUpdate_FoundEnemy`의 warning/fire interval, [Bullet][s-bullet]의 homing 목표 갱신 | 50% 광폭화·역장·금색 배치는 별도. 원작 발사 구성과 우리 6패턴은 일대일 대응이 아님 |
| Boss_03 뱀 · 후속 5패턴 | [Unit_Askard][s-askard]의 행동 시작/취소, [Hitbox][s-hitbox]·[BreakableProp][s-prop]의 독립 수신자 책임만 부분 유사 | 뱀 마디·먹기·머리+꼬리 동시 Slash에 해당하는 구현은 미확인. 후속 작업 시 실제 기믹별 추가 탐색 필요 |
| Boss_04 꽃 봉오리 · 후속 7패턴 | [UnitAI_Askard][s-askard-ai]의 여러 촉수 패턴 요청과 [Unit_Askard][s-askard]의 촉수 실행/정리 | 촉수 4개 동시 Slash·금색 구 반사·그로기와 같지 않음. 원작 취소 시 촉수/탄 일괄 제거는 우리 그로기 정책에 적용하지 않음 |
| Boss_05 슈트 맨 · 후속 7패턴 | [Unit_Askard][s-askard]의 `CancelCurrentAction` 실행 정리, [UnitAI_Askard][s-askard-ai]의 레이저/이동 행동 요청만 부분 유사 | 6번째 착지·충전 장치 2개·Slash 저지 창·관성 추적은 동일 구현 미확인 |
| Boss_06 최종 보스 · 후속 8패턴 | [UnitAI_BossBasic][s-boss-ai]의 연출/페이즈 콜백, [UnitAI_Askard][s-askard-ai]의 페이즈별 후보 추가 | 부양 중 피해 제한·차원문·기둥/수정 동시 제거·5×5 폭격·벽 복구의 동일 구현은 미확인. 현 제출 범위에 추가 구현하지 않음 |

## UI·표시·연출 — U01~U09

| 정의·관련 기능 | 우리 책임·범위 | Sephiria 파일과 확인한 근거 | Dungreed 보조 | 적용·생략 |
| --- | --- | --- | --- | --- |
| U01 CharacterRendering · F-P01/02/08/09, F-U06 | Character가 전달한 일반 변위·조준의 Update(3) 모션·대시/생명주기·피격 표시 | [PlayerAvatar.cs][s-player] SetAnimationTransition366·UpdateCostumeOutfit757; [Animator2D_Basic.cs][s-animator2d] Awake31·Update55·ChangeState402·SetFrame597; [Animator2D_MultipleSpriteRenderer.cs][s-animator2d-multiple] SetSprite10; [SilhouetteFx.cs][s-silhouette] SetSprite·Update; [UnitOverlaySprite.cs][s-hit-overlay] 원본 표시 복구 | — | Character.Update(0)→SetMovement(일반 변위, 조준)→Rendering.Update(3). Body 참조·FixedUpdate·물리 변위/시간 누적·SpeedMultiplier 제거. 상태 캐시·고정 Idle5fps/Move10fps·뒷걸음 역재생·대시 자세·잔상8개·Death/Spawn unscaled 유지. 원작 MOVE_BACK은 위 조준의 뒷모습이며 우리 역재생과 다름. 현재 변경 검증은 GAME_SPEC2.1.1, 이전 이관 결과는3.3.11 |
| U01 지원 PlayerSpriteAnimationSet · F-P01/02/09 | 상태별 Sprite·FPS·개별 프레임 시간·반전·반복의 단일 데이터 | [AnimationSet.cs][s-animation-set] StateInfo·timeline·fps·repeat; [KeyFrameData.cs][s-keyframe-data] 상태 프레임 조회·DEAD/SNARE 보충; [PinkRabbit.asset][s-pinkrabbit-animation] 실제14상태·프레임/FPS | — | 우리14상태를 ScriptableObject에 직접 정의. Death/Spawn 개별시간과 기존 확정 아트 사용. 원작 코스튬·전역 캐시·문자열 FSM·DEAD 자동 복제는 필요 없어 도입하지 않음. 조사 근거는 [Player 애니메이션 분석](archive/03_Analysis/Sephiria_Player_Animation_Architecture_20261116.md) |
| U02 UIController · F-P04/07/09/10, F-U02/03 | 독립 Instance·Draw/Slash/Death HUD·후보/상호작용 안내·진입 전 표시 복구 | [UIManager.cs][s-ui] `Awake`, `ConnectPlayerObject`, `DisconnectPlayerObject`; [UI_PlayerHP.cs][s-hp] `Connect/Disconnect`; [UI_BossHPBar.cs][s-bosshp] `SetBossAI`, `UpdateHP`; [UI_SkillQuickSlotElement.cs][s-cooldown] `SetCooldown` | [GameManager.cs][d-gm] `CreatePlayer`의 전역 UI 연결 | 최종 HUD 층을 Canvas에 연결하고 현재 HP·Groggy·쿨타임을 즉시 표시. Slash/Death는 자기 HUD 소유만 복구. 후보 사건과 현재 상태로 수정의 월드 F 안내를 표시. Cursor는 활성화 경계에 적용/복구. 원작 root/풀/광범위 IUIPlayerSettable/네트워크 ping과 후속 UI animation 제외 |
| U03 UI_Title · F-S01, F-U01, F-S06 | 씬 Start 준비·최종 로고/PF3메뉴·3슬롯 표시/선택 | [UI_TitleLobby.cs][s-title] `Start/Update`, `QuitGame`; [UI_SaveProfilePanel.cs][s-slots] `OnOpened`, `SelectProfile`: 슬롯 상태 표시/선택 | [TitleUIManager.cs][d-title-ui] Awake·Start·LimitHozitontalRectSize의 자기 참조/화면비; [UI_TitleBasicControl.cs][d-title-basic] 패널 상속. `dungreed/project/ExportedProject/Assets/Scenes/Title.unity` 활성 로고 Rect943·Buttons Rect925와 자식907/926/868 배치 | 우리 GameManager가 저장 로드를 시작하고 UI_Title은 준비 후 공개 진입 API 호출. 320×180 씬 좌표를1920×1080으로 환산한 로고 중심/3버튼 배치만 채택하고 사용자 로고·PF스타더스트 사용. 설정 패널은 기존 미구현으로 버튼 비활성. 원작 Steam/로비/네트워크 호스트 제외 |
| U04 UI_Loading · F-U05, F-S01 | 로딩 진행·완료/실패 표시. 기본 구현 | [ScreenFader.cs][s-fader] `SetLoadingScreen`, `ClearLoadingScreen`, `FadeIn/FadeOut`: 전환 표시와 화면 객체 수명 | — | 화면 표시 책임만 참고. 준비 완료 판단은 게임 흐름 책임. 원작 로딩 화면 타입 DB·씬 부팅 전체를 UI_Loading에 합치지 않음 |
| U05 UI_Settings · F-U08 | 음량·키 바인딩·화면 설정·비전투 ESC. 미구현 | [UI_OptionsPanel.cs][s-options] `OnOpened`, `OnClosed`, `ResetKeybindings`: InputAction override JSON 저장/초기화; [SoundManager.cs][s-sound] 음량 setter | — | 바인딩 저장과 옵션 화면 생명 주기만 채택. 원작 옵션 전체/Steam 설정 제외. 전투/컷씬 중 열기 제한은 우리 입력 게이트 |
| U06 GameCamera · F-P04/07/09, F-U04 | 독립 Camera·기본 추적, Draw 가중 줌 아웃/Slash·Death Player 줌 인·복구 | [GameCamera.cs][s-camera] `Awake`, `OnDestroy`, `SetObserver`; [TargetTracker.cs][s-tracker] `LateUpdate`51·`SetTarget`235·`SetTempTarget`254/`ResetTempTarget`274·`SetZoom`286: 커서 offset·이전 target·unscaled 줌 | [GameCamera.cs][d-camera] `Awake/Start`, [PlayerTracker.cs][d-player-tracker] `Awake`21·`LateUpdate`38: Camera 캐시·target+offset·방 경계 | 서비스 Start 캐시·전환 경계의 target/줌 보관. 자기 모드로 일반0.5초/Draw0.3초 선택, SmoothDamp 기준이며 도착시간 보장 아님. 완성 맵 VerticalLimits와 현재 줌으로 Follow/목표점·보간/줌 뒤 실제 위치y를 제한. 원작 전체 방 관리·2P·날씨·렌더 텍스처·후처리 제외 |
| U07 CutsceneController · F-B02/05, F-U03 | Boss 첫/재조우·사망의 시작/정상완료/중단·상태 보관/복구 | [CutScenePlayer.cs][s-cutscene] PlayCutScene·CutSceneCoroutine: 중복 재생·입력 차단·clip 순차 실행·종료 콜백 | — | Prepare에서 새 씬 참조1회, Play는 unscaled 대기·자기 IsBossCutscenePaused·HUD/초점 보관. Complete/Cancel·WasCompleted로 후속 전투/저장 구분. Draw/Slash/Player사망은 기존 소유자 유지. 노드/범용 FSM·ESC skip·최종 연출 생략 |
| U08 AudioController · F-U07 | FMOD 효과음/BGM·음량. 미구현 | [SoundManager.cs][s-sound] `PauseAll`, `SetMasterVolume/SetFXVolume/SetBGMVolume`, `PlayBGM`, `StopBGM`: 이벤트 인스턴스 생성·정지/release | — | 우리 확정 이벤트만 연결. 원작 parameter 경로·bank·이벤트 GUID를 그대로 사용하지 않음. 라이브러리·음원 준비 상태와 코드 작성은 별도 |
| U09 UI_Death · F-P09 | 생성/사망 문구 시점·텍스처 흑백/암전·Player 강조·원본 재질 복구 | [PlayerSpawner.cs][s-spawner] HandleDieClientside759·HandleReviveClientside780의 사건별 표현; [UIManager.cs][s-ui] 대상 연결/해제; [ScreenFader.cs][s-fader] 표시/복구 | — | Spawner가 시점을 요청하고 UI는 자기 원본 sharedMaterial/정렬/HUD를 보관·복구. 사건 경계에서 SpriteRenderer·TilemapRenderer를 수집하며 컬러 텍스처도 URP2D 전용shader 최종 texture×tint 휘도로 흑백화하며 Player 하위는 제외. HP·시간/Save·부활 정책은 UI에서 판단하지 않음. 원작 FX/음향/네트워크와 동일 아트 구현 아님 |

## 시스템 — S01~S13

| 정의·관련 기능 | 우리 책임·범위 | Sephiria 파일과 확인한 근거 | Dungreed 보조 | 적용·생략 |
| --- | --- | --- | --- | --- |
| S01 GameManager · F-S01/04/06, F-P04/09, F-B02/05 | 부팅·Save·씬세션과 일반/Draw/Slash/Death/BossCutscene 정지 OR | [SaveManager.cs][s-save-manager] Load/Current; [GameTimeManager.cs][s-time] Awake·Pause·ResetTimeScaleTo1 | [GameManager.cs][d-gm] Awake/Start·PauseGame/ResumeGame·CreatePlayer | 각 정지의 소유만 해제하고 최종 OR로 시간 적용. 원작 ResetTimeScaleTo1 무조건 호출·DB·2P·로그라이크 초기화 제외. Title 참조 없음 |
| S02 StageManager · F-S01, F-P09, F-B01~06 | Map/Boss/Player 생성 순서·조우/처치 연출·기록·전투 정리·재도전 | [DungeonManager.cs][s-dungeon] LocalLoadStage; [FloorGenerator.cs][s-floor] Generate; [BossSpawner.cs][s-boss-spawner] EnterEventCoroutineServerside | [GameManager.cs][d-gm] CreatePlayer의 생성 후 연결 | 씬 서비스 Start→Cutscene.Prepare→Map검증→Boss패턴등록/Spawner→Player/Bind. 첫 조우는 정상 연출 뒤 기록·문/HUD·Target, 처치는 즉시기록/위험물정리→연출→사체/문. 양쪽 사망 구독을 전투 경계에서 해제. 네트워크/절차 생성 제외 |
| S03 MapManager · F-S03, F-P10, F-B01/02/05 | Map Instance·고정 배치·수정/처치 이력과 입구·출구 복구 | [FloorGenerator.cs][s-floor] FindSpawnPoint; [DungeonDoor.cs][s-door] Open·Cloase·TurnOffCollider/TurnOnCollider | — | Map Initialize에서EntryMotion/ExitMotion을캐시. 저장/재도전은SetOpen(open,true)완결상태,수정/처치사건은SetOpen(open)모션. 최종open뒤통과·닫기즉시차단은MapDoor책임. 아트없는검증객체만기존GameObject활성대체유지. 무작위던전·미니맵·Astar제외. 이번연결검증은GAME_SPEC3.3.10 |
| MapDoor · F-S03, F-P10, F-B02/05 | 최종 문 Animator와 실제 통과 Collider의 개방 완료 경계 | [DungeonDoor.cs][s-door] Awake의Collider캐시·Open/Cloase의상태·TurnOffCollider/TurnOnCollider의독립판정; [DungeonDoorCustomPoint.cs][s-door-point] doorDir·Gizmo·XML배치메타데이터 | — | Awake자기캐시·SetOpen요청·LateUpdate의open/전이완료확인뒤Blocker해제. 닫기는즉시차단. 원작CustomPoint는생성함수가없으며우리프리팹생성은LaboratoryArtSetup/WorldArtSetup의실제InstantiatePrefab사용. Astar·네트워크·절차생성제외. 구현/검증은GAME_SPEC3.3.10에서구분 |
| S04 CrystalCheckPoint · F-P10, F-S03 | 후보·F1회 지연 활성/취소·회복/저장·완료 사건 | [Interactable.cs][s-interact] `OnTriggerEnter2D/Exit2D`, `Interactive`141의 재진입 거부·지연 후 완료 사건; [InteractableChecker.cs][s-checker] `AddInteractable`, `RemoveInteractable`, `OnInteractionStart` | — | 후보/실행·지연 완료/취소 분리만 채택. 성공은 활성 확정·Heal/Groggy0·3키 Set/Save→Trigger/후보 닫기→OnActivated/OnActivationFinished. Map/Player가 자기 후속 구독. 원작 인터랙션이 우리 수정의 회복·저장 정책까지 제공하는 것은 아님. 범용 상호작용 종류·선택기/네트워크 제외 |
| S05 SaveManager · F-S01~03/06 | 3슬롯 캐시·선택한 current/Current 직접 참조·명시적 Save와 내부 오류 책임 | [SaveManager.cs][s-save-manager] `Current`, `Load`, `CreateNew`, `Save`: current 직접 반환·Copy 스냅샷·진행 중 요청 보관 | [SaveManager.cs][d-save] `GetCurrent`, `Initialize`: 현재 저장 데이터 접근 비교 | Sephiria의 요청자 `Current.Set… → Save` 책임 채택. Run/TMP·Steam cloud 제외. Dungreed의 Current 조회 시 Slot1 자동 초기화는 **적용하지 않음**. 우리 SelectSlot 선택 시에만 current 설정 |
| S06 SaveData · F-S02 | 호출자가 key/타입을 아는 Get/Set·파일·XML/암복호화·backup | [SaveData.cs][s-save] `CommonPath`, `Copy`, `Load/Save`, `EncryptString/DecryptString`, `GetString/GetInt`: 문서 경로·사전 typed cast·스냅샷 | — | 문서/Project_Chrono와 ProjectChrono 기반 키, 우리 sav/bak 정책 적용. 원작 키 문자열·클라우드·20개 backup·런 데이터 정책 그대로 복사하지 않음 |
| S07 CharacterBuff · F-P07/08/11, F-B04, F-S04 | 고정 Fever/Slow 다음프레임·개별 갱신/제거·Remove우선·scaled수명, Boss Groggy 위임 | [CharacterBuff.cs][s-buff] `Initialize`, `Update`, `Destroy`, `ApplyStatus/RemoveStatus`; [CharacterBuff_StatusInstance.cs][s-buff-stat] 적용/제거; [CharacterBuff_Heal.cs][s-buff-heal] `OnUpdate_Server` 회복  [CharacterDebuff.cs][s-debuff]의 해로운 효과 분리; [CharacterDebuff_StatusInstance.cs][s-debuff-stat] InitializeInner·ApplyStatusInner59·RemoveStatusInner70의 독립 적용/원복; | — | 적용/원복·시간·회복 전달만 참고. 원작 주기Heal·스택/증폭·네트워크 제외. 획득마다Heal·다음프레임/Remove우선·기본수치/Draw상한은 우리 Q13. Slow는 SP만 비중첩 갱신·Fever와 단일 합성. ClearDebuffs는 Fever 보존, OnDie/OnRevive는 자기 RemoveAll. Boss Groggy 원본 유지, DOT/AS슬로우 콘텐츠·DB 미도입 |
| S08 Hitbox · F-P03/07/08, F-B06 | Collider→Character/Projectile/Structure·분류/PartId Awake캐시·수신위임 | [Hitbox.cs][s-hitbox] `Awake`, `GetCombatBehaviour`; [MeleeCollision.cs][s-melee] `Attack`에서 수신자 조회 | — | 공간질의·피해허용·탄반응을 분리. Character ReceiveHit, 독립 Structure ApplyDamage, 탄 Slash OnSlashed. 높이레이어 대신 TargetKind/PartId. 특수구조물 Normal거부·몸체/피격Collider분리 유지 |
| S09 CollisionBox · F-P03/07, F-B06 | 재사용 사각질의·일반피해와 Slash 탄/구조물/부위 분기·중복·취소·Gizmo | [MeleeCollision_Rectangle.cs][s-melee-rect] `CheckCollision`33, `OnDrawGizmos`8; [MeleeCollision.cs][s-melee] `Attack`168, `ClearAttackedInSwing`28, 결과 배열483 | — | zero-distance 사각/수신자 중복만 참고하고 포화확장·회전Gizmo·queryVersion중단 보완. Slash는 수신자피해1회·탄/구조물반응1회·고유PartId보고/완료통지·벽LOS없음. 초기/확장할당과 NonAlloc 반복조회 구분 |
| S10 DamageInstance(struct) · F-P03/07/08, F-B06 | source·amount·kind·공용 NextAttackId·Hitbox여부 값 전달 | [DamageInstance.cs][s-damage] `GetDamage`, `Set`: 공격 출처·값·방향·피해 종류를 묶어 수신자에 전달 | — | 원작20개 재사용 class 풀과 우리 struct는 다르다. 값 전달만 채택하며 공용 NextAttackId로 일반/Slash/탄 번호를 구분. 치명타·속성/강탈·공유 객체 풀 제외 |
| S11 TutorialController · F-S05 **후속** | 단계/허용 입력·완료 저장. 현재 미구현 | [OpenTutorialTrigger.cs][s-tutorial-trigger] `LateUpdate`: 저장 스위치 확인/변경·안내 UI; [TutorialProp.cs][s-tutorial-prop] `Start`: 저장 단계별 표시 | [TutorialManager.cs][d-tutorial] `Start`, `OnEndCutScene`, `Update`: intro와 완료 저장·이동 | 작은 단계 안내/완료 기록만 부분 유사. Dungreed Update의 완료는 test mode 단축 경로이며 실제 전체 단계 제어기로 오해하지 않음. 현 제출 범위에 추가하지 않음 |
| S12 DebugController · F-S07 | Editor 전용 ~ GUI·수치 변경. 미구현 | [UI_DevCommandlinePanel.cs][s-debug] `Connect/Disconnect`, `OnOpened`: 대상 연결·명령 입력/도움 표시 | — | 개발 도구 책임만 참고. 원작 명령어·자동완성·서버치트를 복사하지 않음. 우리 간단 GUI와 Editor 제한은 별도 구현 |
| S13 데이터 enum · 관련 전투/연출 기능 | CameraMode·CutsceneKind·BuffType·DamageType·SlashResponse·TargetKind. FSM 별도 도입 안 함 | [EDamageType.cs][s-damage-type], [EDamageFromType.cs][s-damage-from] 선언과 [DamageInstance.cs][s-damage] `Set`의 사용: 데이터 식별값 분리 | — | 이름/값은 우리 사양 기준. 원작 Slice/Projectile/Fall 등 값을 그대로 가져오지 않음. 나머지 5종에 대한 동일 enum은 미확인; 각 소비자에 필요한 최소 값만 정의 |

## static Instance와 초기화 순서에서 확인한 점

- Sephiria [PlayerInputController][s-input]는 `Awake`에서 중복을 제거하고 Instance와 자신의 ActionMap을 준비한다. Player 연결은 이후 [PlayerSpawner][s-spawner] `OnStartAuthority`에서 한다. `OnDestroy`는 본인이 현재 Instance일 때만 비운다. 입력 오브젝트는 MainWorld의 별도 `InputSystem`으로 확인되어 Player prefab의 생성/복제 수명과 같다고 가정하면 안 된다.
- Sephiria [UIManager][s-ui]는 `Awake`에서 Instance를 등록하고 ScreenFader 이벤트에 연결한다. [GameCamera][s-camera]는 `Awake`에서 자신의 Camera를 확보하고 `SetObserver`에서 기존 대상 이벤트를 해제한 뒤 새 대상을 연결한다. 연결 해제 없이 대상만 덮어쓰는 구조가 아니다.
- 제공된 Sephiria assets의 `.meta`에서 **ScreenFader -6000, UIManager -700, PlayerInputController -98, GameCamera 0, PlayerSpawner 0**을 확인했다. MainWorld 씬에는 InputSystem·UIManager·Camera가 별도 배치되어 있다. 이 값은 해당 원작 자산의 의존 관계를 설명하는 근거이지, 우리 프로젝트나 모든 동적 생성에서 같은 순서가 보장된다는 뜻은 아니다. 같은 0의 상대 순서에 의존하지 않는다.
  - 순서 출처: `docs/reference/sephiria/assets/ExportedProject/Assets/Scripts/Assembly-CSharp/` 아래 `ScreenFader.cs.meta`, `UIManager.cs.meta`, `PlayerInputController.cs.meta`, `GameCamera.cs.meta`, `PlayerSpawner.cs.meta`.
  - 배치 출처: [MainWorld.unity](reference/sephiria/assets/ExportedProject/Assets/Scenes/MainWorld.unity). 코드 구현은 앞 표의 `code/ingame code` 파일을 기준으로 읽는다.
- Dungreed [GameCamera][d-camera]는 `Awake`에서 중복 제거·DontDestroyOnLoad·Camera 캐시, `Start`에서 환경 초기화를 수행한다. [GameManager][d-gm]는 Awake에 여러 관리 도구를 초기화한 뒤 자신을 등록한다. 이 순서를 그대로 복사하면 새 도구가 GameManager.Instance를 역참조할 때 문제가 생길 수 있으므로 각 실제 호출을 검토한다.
- 우리 연결에서는 **전역 참조 등록**, **자기 컴포넌트 준비**, **Player 대상 연결**, **입력 허용**을 서로 다른 시점으로 확인해야 한다. Instance가 null이 아니라는 사실만으로 비동기 준비가 끝났다고 판단하지 않는다. Player 재생성/중복 제거·MainWorld 재진입·Title 복귀의 해제 대상도 함께 검사한다. 실제 채택 순서와 실행 검증 결과는 GAME_SPEC의 해당 F-S01/F-P01 기록에 남긴다.
- Sephiria [PlayerInputController][s-input]의 `Update`는 `GameCamera.Instance.Camera`를 반복 조회한다. 이번 우리 변경은 그 부분을 그대로 복사하지 않는다. MainWorld 초기 활성 객체의 모든 Awake에서 Camera·Instance를 준비한 뒤 PIC·UI의 Start에서 고정 참조를 한 번 보관하고, Stage.Generate는 첫 프레임 대기 이후 Player를 생성한다. PIC의 기존 -100은 입력 Update 순서용으로 유지한다. Scene 서비스는 소비자와 함께 제거·재생성되므로 다음 MainWorld의 Start가 새 Camera를 확보한다.
- 반복 조회 감사에서는 고정된 참조와 변하는 상태를 구분한다. HP·세션/로딩 상태·카메라 위치와 슬롯 선택 때 바뀌는 SaveManager.Current를 최초 값으로 고정하지 않는다. Stage·Spawner의 생성/연결/정리 시 조회와 매 프레임 조회도 구분한다. GUIStyle처럼 GUI 컨텍스트가 필요한 객체는 첫 OnGUI에서 생성하여 재사용하며, 이번 실제 수정·검증 상태는 GAME_SPEC 2.1.1·3.5.1을 따른다.
- 이번 Spawner 자동 초기화는 `Map 복제·Awake 등록 → Map.Initialize 완료 → Player 복제·Spawner.Awake → Bind`로 선행 준비를 명시한다. Spawner는 Map Instance를 캐시하고 Current에서 최초 저장 HP/CheckpointId를 조회하고 이후 수정 활성화 사건에서 SpawnPoint 캐시를 갱신한다. Map의 Instance 존재만으로 준비 완료를 추정하지 않으며 Stage가 생성 전후 상태를 확인한다.
- 이동(2026-11-16 사용자 변경)은 [UnitAvatar][s-unit]의 의도 보관·Update1183 요청과 [TopdownRigidbody][s-body]의 렌더 Update183/Move328 충돌·위치를 읽고 PlayerCharacter.Update(0) → Body.Move(일반+대시 변위) 즉시 적용 → Rendering.SetMovement(일반 변위, AimDirection) → Rendering.Update(3)로 변경했다. Body/Rendering의 FixedUpdate·요청/관찰 누적과 사용하지 않던 SpeedMultiplier를 제거했다. Body 결과는 모션으로 되돌리지 않으며 넉백은 추가하지 않는다. 이번 변경은 같은 Unity6000.3.21f1 격리본 RunArt19묶음(success=true·error 빈값·컴파일/배치0)에서 확인했다. 새3묶음은 물리 스텝 없는 이동·모션, 뒷걸음/정지/비활성화, 벽 차단과 의도 기반 걷기를 검사했고 기존16묶음도 통과했다. 실제 결과/한계는 GAME_SPEC2.1.1과 project/Logs/MovementUpdateVerification-result.json을 따른다. 이전 FixedUpdate 구조29묶음·fixedDeltaTime0.01/0.05 검사는 당시 결과이며 새 변경의 검증이 아니다.
- F-P02는 [CharacterDash_FastMove][s-dash-fast] OnDashBegin의 이동 입력 우선/정지 시 aim 방향과 고정 방향을 참고한다. 원작 [CharacterDash][s-dash]는 이동 종료와 Dodge 타이머를 분리하며 충전 횟수·회복·서버 전송을 함께 보유한다. 우리 Character가 시작 허용·방향·설정·네 남은 시간과 모든 변위를 Update에서 관리하고 Body는 물리 유효성·충돌·적용만 담당한다. 자원/충전·RPC·구덩이 무시는 생략한다. 이전 대시 구현28묶음과 이번 Update/Body 책임 변경29묶음의 실제 결과를 GAME_SPEC 2.1.2에서 구분한다. 이동은 같은 Update 호출에서 끝나며 일반 입력0은 대시를 취소하지 않는다. CancelDash는 마지막 Body 결과·관성만 정리하고 ResetMotion·이동 불가·비활성화는 Rendering 이동 표시도 초기화한다.
- 대시 자체 입력차단과 홀드 복원은 우리 N2·Q9 확정 사양이며 원작의 동일 dashNoInputTime 구현으로 설명하지 않는다. 원작 대시 중 일반 변위는 UnitAvatar.Update에서 DashMoveUpdate가 대체한다. 무기가 StopDash(true)로 대시를 공격으로 전환하는 원작 경로도 우리 Q10 규칙과 달라 복제하지 않는다. 원작 숫자와 우리 임시 튜닝값·현재 검증/남은 아트는 GAME_SPEC 2.1.2에서 구분한다.
- F-P03은 입력의 버튼 유지 사실→Character만의 공통 시작 허용·커서 좌표 확보→고정 무기 AttackController의 방향·공격·콤보·사각 판정으로 연결한다. PlayerCharacter.Awake가 자기 OnDie·OnCutScene에 CancelAttack을 연결하고 OnDestroy에서 해제한다. Attack은 자기 Character를 수치 원본으로 캐시하지만 사망/컷씬/준비 상태를 다시 검사하거나 OnDie에 별도 구독하지 않는다. 원작 UnitAvatar.ForceDie→CancelCurrentAction→자기 IAvatarStuckModule인 WeaponControllerSimple.CancelAction의 중단 책임을 참고했으며, CutScenePlayer의 입력 차단은 우리 IsCutScene 진입 사건으로 변형했다. Input.Update(-100)→Character.Update(0)의 Body.Move 즉시 적용·일반 변위 전달→Rendering.Update(3)→Attack.Update(10) 순서다. 공격·쿨타임은 현재 AS, 콤보 유지는 게임 시간, deltaTime0은 상태 보존이다. 실제 치명 피해·진입 사건·질의 도중 취소와 기존 회귀35묶음·5개 Sequence Case는 GAME_SPEC 2.1.3을 따른다.

- F-P06은 원본 Line을 PlayerCharacter.CurrentLine 한 곳에 저장하고 SkillController가 그 값의 총수명 스냅샷·남은 시간·만료를 관리한다. 재Draw 취소는 원본과 수명을 보존하고 게임 시간0은 수명·경고·소멸 표현을 함께 정지한다. 소비 직전에 같은 게임 시각의 만료를 다시 확인하고 유효한 Line만 한 번 전달한다. BulletBlinkBeforeDestroy의 비율 기반 예고와 FlameGroundFadeFx의 재생 초기화/종료 정리를 부분 참고했으며 원작의 동일 Line 시스템이라고 설명하지 않는다. 기존 Skill 보유/P07 타이머 배치는 이번 사용자 요청으로 대체했다. 실제 구현·확인 상태는 GAME_SPEC2.1.5·2.1.6을 따른다.

## 파일 위치

아래 링크는 문서 위치에서 시작하는 실제 상대 경로다. Sephiria 코드 루트는 `docs/reference/sephiria/code/ingame code/Assembly-CSharp`, Dungreed 코드 루트는 `docs/reference/dungreed/code/ingame code/Assembly-CSharp`이다. 작업 시 링크의 최신 본문을 다시 읽는다.

[s-unit]: <reference/sephiria/code/ingame code/Assembly-CSharp/UnitAvatar.cs>
[s-input]: <reference/sephiria/code/ingame code/Assembly-CSharp/PlayerInputController.cs>
[s-player]: <reference/sephiria/code/ingame code/Assembly-CSharp/PlayerAvatar.cs>
[s-animator2d]: <reference/sephiria/code/ingame code/Assembly-CSharp/Animator2D_Basic.cs>
[s-animator2d-multiple]: <reference/sephiria/code/ingame code/Assembly-CSharp/Animator2D_MultipleSpriteRenderer.cs>
[s-animation-set]: <reference/sephiria/code/ingame code/Assembly-CSharp/AnimationSet.cs>
[s-keyframe-data]: <reference/sephiria/code/ingame code/Assembly-CSharp/KeyFrameData.cs>
[s-pinkrabbit-animation]: <reference/sephiria/assets/ExportedProject/Assets/MonoBehaviour/PinkRabbit.asset>
[s-animation-wrapper]: <reference/sephiria/assets/ExportedProject/Assets/AnimatorController/AnimationWrapper.controller>
[s-greatsword-attack1]: <reference/sephiria/assets/ExportedProject/Assets/AnimationClip/GreatSword_Attack1.anim>
[s-greatsword-attack2]: <reference/sephiria/assets/ExportedProject/Assets/AnimationClip/GreatSword_Attack2.anim>
[s-greatsword-attack3]: <reference/sephiria/assets/ExportedProject/Assets/AnimationClip/GreatSword_Attack3.anim>
[s-spawner]: <reference/sephiria/code/ingame code/Assembly-CSharp/PlayerSpawner.cs>
[s-weapon]: <reference/sephiria/code/ingame code/Assembly-CSharp/WeaponController.cs>
[s-weapon-simple-controller]: <reference/sephiria/code/ingame code/Assembly-CSharp/WeaponControllerSimple.cs>
[s-weapon-simple]: <reference/sephiria/code/ingame code/Assembly-CSharp/WeaponSimple.cs>
[s-weapon-sword]: <reference/sephiria/code/ingame code/Assembly-CSharp/WeaponSimple_SwordAndShield.cs>
[s-skill]: <reference/sephiria/code/ingame code/Assembly-CSharp/SkillController.cs>
[s-wskill]: <reference/sephiria/code/ingame code/Assembly-CSharp/WeaponSkillController.cs>
[s-melee-rect]: <reference/sephiria/code/ingame code/Assembly-CSharp/MeleeCollision_Rectangle.cs>
[s-melee]: <reference/sephiria/code/ingame code/Assembly-CSharp/MeleeCollision.cs>
[s-line-blink]: <reference/sephiria/code/ingame code/Assembly-CSharp/BulletBlinkBeforeDestroy.cs>
[s-line-fade]: <reference/sephiria/code/ingame code/Assembly-CSharp/FlameGroundFadeFx.cs>
[s-breaker]: <reference/sephiria/code/ingame code/Assembly-CSharp/BulletBreaker.cs>
[s-body]: <reference/sephiria/code/ingame code/Assembly-CSharp/TopdownRigidbody.cs>
[s-dash]: <reference/sephiria/code/ingame code/Assembly-CSharp/CharacterDash.cs>
[s-dash-fast]: <reference/sephiria/code/ingame code/Assembly-CSharp/CharacterDash_FastMove.cs>
[s-boss-ai]: <reference/sephiria/code/ingame code/Assembly-CSharp/UnitAI_BossBasic.cs>
[s-boss-spawner]: <reference/sephiria/code/ingame code/Assembly-CSharp/BossSpawner.cs>
[s-armadillo]: <reference/sephiria/code/ingame code/Assembly-CSharp/UnitAI_ArmadilloGolem.cs>
[s-askard-ai]: <reference/sephiria/code/ingame code/Assembly-CSharp/UnitAI_Askard.cs>
[s-askard]: <reference/sephiria/code/ingame code/Assembly-CSharp/Unit_Askard.cs>
[s-bullet]: <reference/sephiria/code/ingame code/Assembly-CSharp/Bullet.cs>
[s-bullet-destroy]: <reference/sephiria/code/ingame code/Assembly-CSharp/BulletDestroyModule.cs>
[s-laser]: <reference/sephiria/code/ingame code/Assembly-CSharp/BulletMoveModule_Laser.cs>
[s-darkness-laser]: <reference/sephiria/code/ingame code/Assembly-CSharp/AskardDarknessLaser.cs>
[s-laser-warning]: <reference/sephiria/code/ingame code/Assembly-CSharp/AskardLaserWarning.cs>
[s-prop]: <reference/sephiria/code/ingame code/Assembly-CSharp/BreakableProp.cs>
[s-hitbox]: <reference/sephiria/code/ingame code/Assembly-CSharp/Hitbox.cs>
[s-render]: <reference/sephiria/code/ingame code/Assembly-CSharp/TopdownActorRenderingMetadata.cs>
[s-hit-overlay]: <reference/sephiria/code/ingame code/Assembly-CSharp/UnitOverlaySprite.cs>
[s-silhouette]: <reference/sephiria/code/ingame code/Assembly-CSharp/SilhouetteFx.cs>
[s-hit-warning]: <reference/sephiria/code/ingame code/Assembly-CSharp/UI_HitWarning.cs>
[s-ui]: <reference/sephiria/code/ingame code/Assembly-CSharp/UIManager.cs>
[s-hp]: <reference/sephiria/code/ingame code/Assembly-CSharp/UI_PlayerHP.cs>
[s-bosshp]: <reference/sephiria/code/ingame code/Assembly-CSharp/UI_BossHPBar.cs>
[s-cooldown]: <reference/sephiria/code/ingame code/Assembly-CSharp/UI_SkillQuickSlotElement.cs>
[s-title]: <reference/sephiria/code/ingame code/Assembly-CSharp/UI_TitleLobby.cs>
[s-slots]: <reference/sephiria/code/ingame code/Assembly-CSharp/UI_SaveProfilePanel.cs>
[s-fader]: <reference/sephiria/code/ingame code/Assembly-CSharp/ScreenFader.cs>
[s-options]: <reference/sephiria/code/ingame code/Assembly-CSharp/UI_OptionsPanel.cs>
[s-sound]: <reference/sephiria/code/ingame code/Assembly-CSharp/SoundManager.cs>
[s-camera]: <reference/sephiria/code/ingame code/Assembly-CSharp/GameCamera.cs>
[s-tracker]: <reference/sephiria/code/ingame code/Assembly-CSharp/TargetTracker.cs>
[s-cutscene]: <reference/sephiria/code/ingame code/Assembly-CSharp/CutScenePlayer.cs>
[s-time]: <reference/sephiria/code/ingame code/Assembly-CSharp/GameTimeManager.cs>
[s-dungeon]: <reference/sephiria/code/ingame code/Assembly-CSharp/DungeonManager.cs>
[s-floor]: <reference/sephiria/code/ingame code/Assembly-CSharp/FloorGenerator.cs>
[s-door]: <reference/sephiria/code/ingame code/Assembly-CSharp/DungeonDoor.cs>
[s-door-point]: <reference/sephiria/code/ingame code/Assembly-CSharp/DungeonDoorCustomPoint.cs>
[s-interact]: <reference/sephiria/code/ingame code/Assembly-CSharp/Interactable.cs>
[s-checker]: <reference/sephiria/code/ingame code/Assembly-CSharp/InteractableChecker.cs>
[s-save-manager]: <reference/sephiria/code/ingame code/Assembly-CSharp/SaveManager.cs>
[s-save]: <reference/sephiria/code/ingame code/Assembly-CSharp/SaveData.cs>
[s-buff]: <reference/sephiria/code/ingame code/Assembly-CSharp/CharacterBuff.cs>
[s-buff-stat]: <reference/sephiria/code/ingame code/Assembly-CSharp/CharacterBuff_StatusInstance.cs>
[s-debuff]: <reference/sephiria/code/ingame code/Assembly-CSharp/CharacterDebuff.cs>
[s-debuff-stat]: <reference/sephiria/code/ingame code/Assembly-CSharp/CharacterDebuff_StatusInstance.cs>
[s-buff-heal]: <reference/sephiria/code/ingame code/Assembly-CSharp/CharacterBuff_Heal.cs>
[s-damage]: <reference/sephiria/code/ingame code/Assembly-CSharp/DamageInstance.cs>
[s-tutorial-trigger]: <reference/sephiria/code/ingame code/Assembly-CSharp/OpenTutorialTrigger.cs>
[s-tutorial-prop]: <reference/sephiria/code/ingame code/Assembly-CSharp/TutorialProp.cs>
[s-debug]: <reference/sephiria/code/ingame code/Assembly-CSharp/UI_DevCommandlinePanel.cs>
[s-damage-type]: <reference/sephiria/code/ingame code/Assembly-CSharp/EDamageType.cs>
[s-damage-from]: <reference/sephiria/code/ingame code/Assembly-CSharp/EDamageFromType.cs>
[d-gm]: <reference/dungreed/code/ingame code/Assembly-CSharp/GameManager.cs>
[d-camera]: <reference/dungreed/code/ingame code/Assembly-CSharp/GameCamera.cs>
[d-player-tracker]: <reference/dungreed/code/ingame code/Assembly-CSharp/PlayerTracker.cs>
[d-save]: <reference/dungreed/code/ingame code/Assembly-CSharp/SaveManager.cs>
[d-tutorial]: <reference/dungreed/code/ingame code/Assembly-CSharp/TutorialManager.cs>

[d-title-ui]: <reference/dungreed/code/ingame code/Assembly-CSharp/TitleUIManager.cs>
[d-title-basic]: <reference/dungreed/code/ingame code/Assembly-CSharp/UI_TitleBasicControl.cs>
