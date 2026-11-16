# 주요 갱신 실행 순서 — Project Chrono와 Sephiria 비교

**현행 변경:** 2026-11-16 사용자 확정에 따라 Player 이동/몸체를 Update0/3으로 전환했고, 이후 사용자가 직접 플레이에서 드득거림 해소를 확인했다. 아래1~11절은 전환 전 분석 스냅샷이며, 현재 적용·자동 검증은 **12절**, 도입 이유부터 해결 확인까지의 정리는 **13절**과 GAME_SPEC을 따른다. 이전 F100/F200/L200 표를 현행 구현으로 사용하지 않는다.

확인일: 2026-11-16 (Asia/Seoul). 사용자 요청에 따라 Player·Boss·투사체·UI·월드·공통 시스템과 예정 기능을 조사했다. **실제 콜백 종류, 같은 콜백 안의 실행 순서, 수동 호출·사건·코루틴을 구분**한다. 최초 조사인1~11절에서는 게임 코드·프리팹·설정 변경과 Unity 실행·Profiler 측정을 수행하지 않았다. 후속 적용과 확인 결과는12~13절에 구분해 기록한다.

## 1. 범위와 표기

- `C/` = `project/Assets/Scripts/`, `R/` = `docs/reference/sephiria/code/ingame code/Assembly-CSharp/`, `M/` = `docs/reference/sephiria/assets/ExportedProject/Assets/Scripts/Assembly-CSharp/`.
- 현재 런타임 C# **44파일**, 직접 Update/FixedUpdate/LateUpdate를 선언한 **17클래스·18메서드**를 조사했다. 상속으로 실행되는 DummyBoss·탄환 파생형은 별도로 연결한다. OnGUI는5개, Trigger 콜백은3개다. Assets의 C#은 Scripts 또는 Editor 아래에 있으며, Editor 제작/검사 도구를 빌드의 매 프레임 갱신으로 분류하지 않는다.
- 원작 **74개 코드 파일**과 관련 `.meta`·프리팹/씬을 조사했다. 확인한 파일·직접 콜백·실행 순서·SHA-256은 원본 목록에 보관했다. 목록의 직접 콜백이 비어 있어도 상속/수동 호출로 동작할 수 있다.
- 우리 순서는 `[DefaultExecutionOrder]`에서 확인했다. 현재 런타임44파일의 `.meta`에는 executionOrder override가 없다. 원작 순서는 `M/<클래스>.cs.meta:9`에서 확인했다. **같은0 또는 같은 숫자끼리의 상대 순서를 임의로 정하지 않는다.**
- 숫자는 현재 설정이지 수정 제안이 아니다. 미구현 기능에는 실제 숫자가 없으므로 `미정`으로 기록한다. `BossPattern.Priority` 같은 게임 데이터는 Unity 실행 순서가 아니다.
- `U/F/L`은 각각 Update/FixedUpdate/LateUpdate다. `수동`은 호출자의 현재 실행 문맥에서 즉시 실행된다는 뜻이며 독립적인 Unity 우선순위가 없다.
- 현재 프로젝트는 Unity6000.3.21f1, 원작 export는2022.3.62f1이다. Fixed Timestep은 각각0.02/0.05이며 export 설정은 원작 실행 측정과 구분한다. 원작 Body가 Update를 쓰므로0.05를 원작 일반 이동의20Hz 제한으로 해석하지 않는다.

## 2. FixedUpdate·Update·LateUpdate를 읽는 기준

| 구분 | 호출 기준 | 이번 분석에서 주의할 점 |
| --- | --- | --- |
| FixedUpdate | 고정 게임 시간 간격. 한 렌더 프레임에0회 또는 여러 번 | 우리 Fixed Timestep0.02는 TimeScale1에서50Hz. F100은 U0 뒤라는 뜻이 아니다 |
| Update | 활성 객체에서 렌더 프레임당 한 번 | 같은 Update 안에서 작은 순서의 클래스들이 먼저 실행. 같은 숫자의 순서는 미보장 |
| LateUpdate | 모든 Update 이후 렌더 프레임당 한 번 | L0도 U500 뒤다. L0끼리는 순서 미보장 |
| OnTriggerEnter/Exit2D | 물리 시뮬레이션의 접촉/분리 통지 | 일반 Update 목록에 끼워 넣지 않는다. 매 렌더 프레임 실행된다고 가정하지 않는다 |
| OnGUI | IMGUI 이벤트별 호출 | 한 프레임에 여러 번 가능. 한 번 실행되는 Update 대체가 아니다 |
| 사건·일반 함수 | 호출 즉시 동기 실행 | OnDie가 발생하면 구독자는 해당 피해 처리 호출 안에서 실행. 구독자의 기본순서0 때문에 다음 Update로 미뤄지는 것이 아니다 |
| 코루틴 | 최초 실행은 시작 호출에서 진행, 이후 yield 조건에 따라 재개 | yield null·씬 로드·WaitForSeconds·WaitForEndOfFrame은 서로 다르다. MonoBehaviour 순서 숫자로 코루틴 재개 전체를 정렬하지 않는다 |
| async/UniTask | await 대상·PlayerLoopTiming·스레드에 따라 재개 | SaveData 파일 IO는 thread pool. UniTask.Yield 기본 Update는 패키지 PlayerLoop이며 스크립트 Update와 동의어가 아니다 |
| Unity Animator | Animator.updateMode와 엔진 애니메이션 평가 단계 | Normal 평가와 스크립트의 명시적 Animator.Update(0)을 구분. Sprite용 커스텀 Animator2D.Update와도 다르다 |

Unity 공식 근거: [고정 갱신](https://docs.unity.com/en-us/engine/6000.3/manual/scripting/managing-time-and-frame-rate/fixed-updates), [스크립트 실행 순서·같은 순서의 비보장·Editor override](https://docs.unity.com/en-us/engine/6000.3/manual/scripting/managing-update-order/script-execution-order), [LateUpdate](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/monobehaviour/lateupdate), [OnGUI](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/monobehaviour/ongui).

우리 UniTask2.5.11 소스에서도 `UniTask.Delay.cs:24–27`의 Yield 기본값은 PlayerLoopTiming.Update이며 `PlayerLoopHelper.cs:447–449`는 Update 루프 앞쪽에 runner를 주입한다. 이를 `GameManager.Update(0)` 뒤에서 재개된다고 기록하지 않는다. 일반 코루틴의 정확한 재개 슬롯을 실제 실행 추적한 것은 아니다.

## 3. 우리 게임의 현재 순서

### 3.1 FixedUpdate — 현재2개

| 순서 | 클래스·근거 | 실제 작업 | 시간/입력 원본 |
| ---: | --- | --- | --- |
| 100 | `C/Player/PlayerBodyController.cs:30–41` | pendingMovement 전체 소비 → Cast 충돌 → Rigidbody2D.position과 Transform 적용 → ActualMovement 공개 | **이전 Update들이 deltaTime으로 계산한 누적 변위**. fixedDeltaTime으로 새 속도 적분을 하는 코드가 아님 |
| 200 | `C/Player/CharacterRendering.cs:78–87` | Body.ActualMovement와 관찰 시간 누적 | 변위는 Body 결과, 시간은 fixedDeltaTime |

Body→Rendering의 **같은 FixedUpdate 안 순서**는 지정돼 있다. 하지만 Character.Update0에서 만든 변위가 같은 렌더 프레임의 뒤쪽 FixedUpdate100에서 소비된다는 보장은 없다. Player Rigidbody2D 보간은None이다(`Assets/Prefabs/Startup/Player.prefab`의 m_Interpolate0). 이전 [카메라 분석](Camera_Follow_Sephiria_Comparison_20261116.md)의50Hz 계단 이동 모델이 해당한다.

### 3.2 Update — 현재9개

| 순서 | 클래스·근거 | 실제 작업 | 시간/후속 |
| ---: | --- | --- | --- |
| −100 | `C/Player/PlayerInputController.cs:66–151` | 캐시된 입력 소비, 이동/조준 전달, Draw·Dash·F 요청 판정 | 액션 콜백170–225는 캐시. 호출한 Character/Skill 함수는 이 U−100 안에서 즉시 실행 가능 |
| 0 | `C/Player/PlayerCharacter.cs:88–136` | 보호 종료 통지, 방향, 홀드 공격 요청, 일반/대시 변위 계산·대시 타이머 | deltaTime. RequestMove129는 요청만 누적 |
| 0 | `C/Enemy/BossCharacter.cs:48–75` | 그로기 시간, 현재 패턴 Tick 또는 다음 패턴 선택 | deltaTime. 같은0의 Player·Projectile과 상대 순서 미보장 |
| 0 | `C/Enemy/Projectile.cs:49–79` | 수명·반사 처리·가상 TickFlight | deltaTime. 파생 Normal/Gold/Area/Laser도 이 상속 콜백 사용 |
| 0 | `C/Systems/DrawVfx.cs:47–52` | Slash flash 잔여시간 | unscaledDeltaTime. Line 표현 전체를 이 Update가 진행하는 것은 아님 |
| 0 | `C/Systems/GameManager.cs:57–60` | 현재 세션 PlayTime을 저장 데이터 **메모리**에 반영 | SessionPlayTime26은 realtimeSinceStartupAsDouble 차이. 매 프레임 디스크 저장 아님 |
| 10 | `C/Player/AttackController.cs:54–95` | 공격/콤보/쿨, 타격창·CollisionBox.Query, 무기 Animator 속도 | deltaTime×AS. GetHitArea276–284는 Owner 위치·고정 공격 방향 사용 |
| 20 | `C/Player/SkillController.cs:72–88` | Line/쿨 수명, Slash 완료 판정·복구 | 일반은 Time.timeAsDouble 차분276–295, Slash는 unscaledTimeAsDouble |
| 30 | `C/Player/CharacterBuff.cs:57–107` | 이전 프레임 획득/Slow/Groggy 예약 적용 → scaled 수명/만료 | frameCount로 같은 프레임 적용 제외. 수명은deltaTime |

0그룹의 열거 순서는 문서상 분류이며 실제 실행 순서를 뜻하지 않는다. Character의 ApplyAttack 등은 호출 시 실행되므로, AttackController의 모든 API가 U10에만 실행된다는 뜻도 아니다.

### 3.3 LateUpdate — 현재7개

| 순서 | 클래스·근거 | 실제 작업 | 의존 관계/시간 |
| ---: | --- | --- | --- |
| 0 | `C/Systems/GameCamera.cs:46–70` | 목표·SmoothDamp·모드 줌·y경계 | Target Transform, unscaledDeltaTime |
| 0 | `C/UI/UIController.cs:91–106` | HP/Groggy/쿨타임을 읽어 HUD 이미지 갱신 | Update의 상태 처리 뒤 현재 값을 폴링. HP 사건 자체가 이미지 갱신은 아님 |
| 0 | `C/Systems/CrystalCheckPoint.cs:37–48` | 수정의 F 안내 Sprite 표시 | 입력 후보/현재 Player 허용 상태 |
| 0 | `C/Systems/MapDoor.cs:23–28` | 문 Animator의 최종 open/전이 종료 확인 후 Collider 해제 | SetOpen은 호출 즉시 차단·Play, Normal Animator의 완료 결과 관찰 |
| 0 | `C/Systems/DrawLineVfx.cs:21–30` | 선의 lossyScale에 맞춘 widthMultiplier | 타이머·논리 상태 변경 없음 |
| 200 | `C/Player/CharacterRendering.cs:90–145` | 피격색·잔상·이동 상태·Sprite 시간 진행 | 일반deltaTime, 생명주기unscaledDeltaTime. SetFacing/BeginDash 등의 호출 중 즉시 Sprite 변경도 가능 |
| 500 | `C/UI/UI_Draw.cs:32–36` | 최종 카메라 줌에 맞춰 암전 크기 갱신 | Camera L0 뒤라는 순서가 명시됨. 암전은 카메라 자식 |

Camera L0와 Rendering L200의 차이는 존재하지만 Camera가 읽는 것은 Player Transform이다. Sprite 교체가 늦다는 사실만으로 Camera가 이전 Player 위치를 읽는다고 결론 내리면 안 된다.

### 3.4 OnGUI·Trigger

| 종류 | 대상 | 역할 |
| --- | --- | --- |
| OnGUI, 기본0 | `UI_Title:33`, `UI_Loading:12`, `UI_Death:31`, `UIController:110` | Title/슬롯 입력·로딩/사망 문구·복귀/안내 |
| OnGUI, 500 | `UI_Draw:40` | 편집 안내. L500 다음에 한 번만 호출된다고 단정하지 않음 |
| Trigger | `CrystalCheckPoint:51,67` | 수정 후보 등록/해제 |
| Trigger | `EncounterTrigger:17` | Stage.BeginEncounter 요청 |

### 3.5 한 렌더 프레임의 읽는 법

```text
필요한 만큼 Fixed 사이클(0~여러 회):
  Body F100 → Rendering F200 → 물리 시뮬레이션/관련 접촉 통지
일반 Update 단계:
  Input U−100 → [Player/Boss/Projectile/DrawVfx/GameManager U0]
  → Attack U10 → Skill U20 → Buff U30
  ※ 수동 함수·사건은 각 호출 위치 안에서 실행
LateUpdate 단계:
  [Camera/HUD/CrystalPrompt/Door/LineWidth L0]
  → Rendering L200 → DrawShade L500
→ 렌더 파이프라인의 화면 그리기
```

이것은 핵심 콜백 관계의 요약이다. Input System·UniTask·코루틴·Unity Animator·Canvas/IMGUI의 모든 엔진 슬롯을 완전하게 재현한 런타임 trace는 아니다.

## 4. Player·전투·표시의 원작 비교

원작 수치는 실제 컴포넌트의 `.meta` 기준이다. Basic1/Single2/Multiple3은 한 객체에서 세 번 실행되는 파이프라인이 아니다. 실제 기본 코스튬은 Multiple3에서 상속한 Basic.Update를 실행한다.

| 책임 | 우리 | Sephiria 실제 코드·순서 | 차이 |
| --- | --- | --- | --- |
| 입력 전달 | U−100 캐시 소비 | `R/PlayerInputController.cs:165` U−98. Dash351–383/Fire387–440는 **액션 콜백에서 직접 요청** | 숫자−100/−98보다 전달 시점·콜백 경로가 중요 |
| 일반 이동·방향 | Player U0 → Body F100 | `PlayerAvatar.Update273→UnitAvatar.Update871`, 이동1108–1188 → `TopdownRigidbody.Update183/소비275–277`, 모두U0 | 원작 일반 이동은 렌더 Update 안에서 적용. 같은0 사이 순서는 미보장 |
| 대시 이동·회피 타이머 | Character U0가 이동·타이머 소유 | `UnitAvatar:1177–1179`가 DashMoveUpdate를 수동 호출. `CharacterDash.Update66–90` U0는 회피/회복 타이머 | 수동 DashMoveUpdate를 별도 Unity Update로 세면 안 됨 |
| 일반 공격 홀드·시작 | Character U0 요청 → Attack U10 | `WeaponControllerSimple.Update55–122` U0, `WeaponSimple_GreatSword.Update47` U0는 충전, **LateUpdate99–118** L0는 홀드 재시도/Animator Trigger. 검방도Late114 | 일반 공격 시작이 우리와 다른 콜백 단계에 위치 |
| 무기 시각/타격 시점 | 코드 타격창 U10. Animator Normal, 시작/취소 Play→Update(0) | `WeaponControllerSimple.BeginAttackAnimation479`·`WeaponSimple.StartBasicAttackAnimation285`·판정 생성451. 실제 GreatSword_Attack1.anim의 공격 사건229–280 | 원작은 클립 사건과 별도 판정 객체를 사용. 단순 Update 우선순위만 바꿔 같아지지 않음 |
| 스킬 쿨·캐스팅 | Skill U20/요청 때 절대 시각 동기화 | `SkillController.Update109–125` U0, `WeaponSkillController.Update118–135` U0, cast 시작261/animation begin379/end457 사건 | **동일한 Draw/Line/Slash 구현은 미확인**. 쿨/전환 책임의 부분 대응 |
| Buff·Debuff | Buff U30, 다음 프레임 예약 | `CharacterBuff.Update104–118` U0, `OnStartServer158–163`에서 즉시 ApplyStatus. `CharacterDebuff`는 CharacterBuff를 상속해 같은 Update 경로를 사용. UnitAvatar.ApplyBuff3811–3835가 생성/초기화 | 원작에 우리와 같은 전체 +30·frameCount 예약 체계 없음 |
| 피격 보호 만료 | Player 절대 기한26–28, Update는 종료 통지 | `UnitAvatar:1027–1038` U0에서 bool 해제, 피해 경로1445가 bool 조회 | 우리 보호 조회는 Update 전에도 현재 기한 반영. 순서 차이가 곧 열화는 아님 |
| 몸체 애니메이션 | F200 수집/L200 진행, 호출 중 즉시 상태 표시 가능 | 기본 `Animator2D_MultipleSpriteRenderer` **U3**, `Animator2D_Basic.Update55–139` → SetFrame597–604 → Multiple.SetSprite9–14. Single형은U2 | 원작은 Body U0 뒤 Sprite U3. 물리 이동 수집 과정 없음 |
| 걷기·정지 판단 | 실제 Body 이동 결과 | `PlayerAvatar:371–405` 조건 → `UnitAvatar.IsAvailableMoveByInput410–415`: 입력·CanMove·block 상태 | 원작과 판단 원본이 다름. 벽/정지 체감도 별도 비교 대상 |
| 잔상·피격 표시 | 고정8개 잔상, Rendering L200에서 alpha/색 | `CharacterDash_FastMove.DashMoveUpdate15–19`에서 생성; `SilhouetteFx.Update19–30` U0; `UnitOverlaySprite.Update15–40` U0 | 원작 효과 갱신은 개별 Update. 생성 흐름/자원 수명 차이도 있음 |
| 애니메이션 데이터 | PlayerSpriteAnimationSet, 콜백 없음 | AnimationSet/KeyFrameData는 데이터·조회. **AnimatorManager 순서−10020이지만 Update 없음**, Awake14/GetKeyFrame29 캐시 | 큰 음수 순서가 매 프레임 애니메이션을 먼저 진행한다는 뜻 아님 |
| Line 편집 | Input−100→Skill→LineDrawer 수동 | 동일 구현 미확인 | 원작 순서를 임의 대응시키지 않음 |
| Line/Slash 질의 | Skill20→LineSlasher.ApplyHits24–31의 마지막1회 | `MeleeCollision.Update42–74` U0 반복 질의·수명, **LateUpdate78–89** L0는 owner 위치 부착. Rectangle.CheckCollision33–44. BulletBreaker.Update16–32 U0 | 같은 판정 함수 이름보다 반복/1회·위치 부착 단계 차이가 큼 |
| Line/flash 표현 | DrawVfx U0 flash, Line은Skill20이 수동진행; Line폭 L0 | `BulletBlinkBeforeDestroy.Update47–65` U0·`FlameGroundFadeFx.Update8–21` U0는 부분 유사 | 같은 Line 시스템이 아님. 표현 전부U0라는 표기는 틀림 |
| 생성·죽음·부활 | PlayerSpawner Awake/Start·사건·unscaled 코루틴 | `PlayerSpawner.Update187–223` U0는 권한·핑, **LateUpdate226–232** L0는 이름표. 죽음/부활736/759/780은 사건 | 원작 Spawner의 Update를 우리 자동 체크포인트 부활과 동일 역할로 대응하면 안 됨 |

## 5. Boss·탄환·위험물의 원작 비교

| 책임 | 우리 | Sephiria 실제 코드·순서 | 판정 |
| --- | --- | --- | --- |
| Boss 공통 진행 | BossCharacter U0, Groggy/Pattern.Tick(deltaTime) | `UnitAI_NewBasic.Update147–167` U0 → OnAIUpdate. `UnitAI_BossBasic.Update66` U0는 base 후 사망 연출 타이머 | 둘 다 주된 AI는Update. 우리에 AI/Unit 분리와 같은 구조는 없음 |
| 패턴 선택 | Boss U0 안의 Selector.GetNextPattern | `UnitAI_NewBasic.OnAIUpdate294` → 파생 `UnitAI_Askard.OnAIUpdate_FoundEnemy47` 등 | Selector 자체는 Unity 콜백 아님 |
| 패턴 실행 | BossPattern.Tick62→OnTick. 일반 C# | `UnitAI_Askard:83,98,101` → `Unit_Askard` 요청·코루틴1499, Update140. `Unit_ArmadilloGolem.Update190`는 미사일/레이저 진행 | 원작도 보스마다 Update/코루틴/사건이 다름. 모두 동일 Boss.Update 하나로 일반화 불가 |
| 발사 간격 | 시험 DummyPattern.OnTick99/while102→Spawn127 | `Unit_Askard:1519,1534,1562–1563`: 공격 사건을 WaitForEndOfFrame으로 확인하고 WaitForSeconds(0.05) 발사 간격 | 우리 while은 지연 frame에 여러 발을 같은 Update에 생성 가능. 비용은 미측정 |
| 일반·금색 탄 | Projectile U0 상속, Normal.TickFlight→CircleCast/이동 | `Bullet.Update129/Move178/ClientMove274` U0 → `BulletMoveModule.Move67/ClientMove96` 수동 → `UniformVector2.OnMove20–49` deltaTime | 원작도 일반 탄이 FixedUpdate/Trigger만으로 움직이지 않음 |
| 장판·레이저 | Projectile U0 → Area.TickFlight54–79, Laser는shape설정25 | `AskardDarknessLaser.Update37–57` U0 예고·반복 피해·Overlap, `BulletMoveModule_Laser.OnMove49`는 Bullet에서 수동 호출 | 우리 1회 피해와 원작 예고/주기 피해 등 수명 차이. 같은0 순서 미보장 |
| 구조물·부위 | BossStructure.Receive/ApplyDamage/Slash 수동 | 원작 Hitbox/Melee/Bullet의 수신/질의 흐름 부분 대응 | 구조물이라는 이유로 FixedUpdate 우선순위가 존재하는 것은 아님 |
| 조우 감지 | EncounterTrigger의 **물리 Trigger** → Stage 코루틴 | `BossSpawner.Update68/214/245` U0의 좌표 영역 검사 → 등장 코루틴369 → AI 비활성/활성455/471 | 진입 감지의 샘플링 주기가 다름. 원작도 AI는 등장 완료 뒤 열림 |

이 표의 원작 AI/Unit/Bullet/MoveModule/레이저/Spawner `.meta`는 확인한 대상 모두0이다. `UnitAI_NewBasic.LateUpdate171`은 감지 FX 표시용이며 AI 판단 본체 Update와 구분한다. MoveModule의 메서드 이름이 Move여도 Bullet.Update가 호출하므로 실행 문맥은Update다.

**현재 실제 콘텐츠 구분:** `Assets/Prefabs/Startup/GameSystems.prefab:52`는 `GuardRobot.prefab`을 사용한다. 이 프리팹62–66은 DummyBoss가 아니라 기본 BossCharacter를 붙이며, 기본 InitializePatterns217은 비어 있다. 시험용 Dummy의4패턴은 `BossTestArena.unity:135–137`의 BossPrefab override에 연결된다. 따라서 일반 MainWorld 이동의 끊김을 시험 Boss 발사 부하 때문이라고 할 근거가 없으며, 구체 경비 로봇5패턴을 이미 실행 중인 기능으로 기록하지 않는다.

## 6. UI·월드·공통 시스템의 원작 비교

| 책임 | 우리 | Sephiria 실제 코드·순서 | 차이/보장 |
| --- | --- | --- | --- |
| Player HP | UIController L0 폴링 | `UI_HPBar.Update10–30` U0. `UI_PlayerHP.Update10` U0는 **ping**이며 Connect32가 HPBar 연결 | 양쪽 HP는 폴링. 원작 전체가 HP 사건으로만 갱신된다는 설명은 틀림 |
| Boss HP | UIController L0 폴링 | `UI_BossHPBar.Update28→UpdateHP88/fill126` U0. OnOpened50은 즉시 첫 갱신, 목숨 감소 사건은 별도 아이콘152–174 | 우리 Late는 그 프레임 앞선 피해를 반영. 차이만으로 문제 아님 |
| 스킬 HUD | UIController L0에서 남은 쿨 조회 | `UI_SkillQuickSlotBar.Update8–27` U0의 scaled Timer→Element.SetCooldown21. Element는 자체 Update 없음 | 원작 전용 바의 주기와 우리 매 Late 표시가 다름 |
| UI 전역 연결 | UIController.Start60/SetPlayer134/SetBoss141 | **UIManager−700**, Awake70·Connect198/Disconnect222의 수동 전달. 자체 Update 없음 | −700을 HP바의 Update 우선순위로 옮겨 적으면 안 됨 |
| Draw/Death 암전 | UI_Draw L500, UI_Death.Show/Hide 수동·OnGUI | **ScreenFader U−6000**, Update126. alpha139/대기149/179는 clamped unscaled, 색 Timer130은 scaled | 원작 전역 Fader와 우리 모드별 암전은 다른 구성. 우리 Camera L0→Shade L500은 명시됨 |
| Camera | GameCamera L0, 일반 추적 unscaled | GameCamera U0는 loading alpha60. **TargetTracker L300**이 위치 추적51, 일반 위치는 scaled141·줌은 unscaled69 | Camera 역할을 GameCamera.Update와 혼동하지 않음 |
| 세션시간·정지 | GameManager U0의 메모리 PlayTime, 정지는 수동 setter | `DungeonManager.Update244/266` U0는 이름 playedRealtime에도 deltaTime 사용. `GameTimeManager.Update42` U0는 Shader 시간44, Pause48/Reset57/UpdatePauseState63에서 timeScale 변경 | 기록시간/정지 정책 차이. 우리도 정지를 Update에서 매번 재설정하지 않음 |
| 맵생성·씬준비 | Stage.Generate39 코루틴, Map/Boss/Player 명시 순차 | `FloorGenerator.Update73` U0가 요청을 감지해 Generate147 코루틴, WaitForSeconds(0.2)149. DungeonManager.LocalLoadStage778 요청 | 우리 Stage에는 Update 없음. 코루틴 시작/대기가 실제 순서를 정함 |
| 컷씬 | Cutscene.Play54 중첩 코루틴, unscaledDelta90 | CutScenePlayer.Play29→CombatManager.StartCoroutine44→clip.CutScene57, 입력 차단/복구52/61–67. 자체 Update 없음 | 원작 모든 clip이 unscaled라는 근거 없음. 전체를 같은 우선순위로 할당하지 않음 |
| 문 | MapDoor L0에서 Normal Animator 종료 확인. SetOpen32 수동 | DungeonDoor.Start20→커스텀 Animator2D 상태 조건25. Open31/Cloase37은 bool 변경, Collider43/50은 별도 함수. 자체 Update 없음 | 문 판정의 실제 갱신 주체가 다름. Normal Animator와 Animator2D.Update 구분 |
| 수정/상호작용 | Trigger 후보·Late 안내·unscaled 절대 기한 코루틴 | Interactable.Trigger112/121, Interactive141→scaled WaitForSeconds0.1(153)→사건158/163. Checker.Update58 U0는 근처 선택/타이머111 | 후보 선택 주기·대기시간 원본 차이. 동일한 수정 회복/저장 정책 아님 |
| Map 후속 | MapManager.Initialize63/OnActivated→Handle164→Spawn166/문167 | FloorGenerator.FindSpawnPoint113·DungeonDoor API 부분 대응 | MapManager에 독립 Update 우선순위 없음 |
| 사망표시 | UI_Death.Show49가 HUD/암전/강조 동기 변경, renderer 수집60–67은 진입1회 | PlayerSpawner의 사건별 표현759/780·UI 연결·ScreenFader 부분 대응 | 매 프레임 씬 검색으로 오인하지 않음 |
| Title/슬롯 | OnGUI·Start/async, 시간은 realtime/ignoreTimeScale | UI_TitleLobby.Start12/Update28 U0의 BGM/fade. UI_SaveProfilePanel.OnOpened8/Select34 수동. 둘 다 meta0 | Canvas/패널 사건과 IMGUI/async의 구조 차이 |
| 로딩 | UI_Loading.OnGUI12, realtime 점 문구 | ScreenFader Update−6000·GameCamera.Update0의 alpha 조정 | GameCamera의 이 Update를 추적 코드로 분류하지 않음 |
| 저장 | SaveManager static 요청→snapshot→queue, SaveData thread pool IO | SaveManager.Save103→Copy143/OnSaveBegin166/RunOnThreadPool180. 독립 Update 없음 | 호출·await/IO 문맥이지 독립 Update 단계가 아님 |

문/수정 실제 아트 Animator는 LaboratoryEntrance/Exit와 Amethyst 프리팹에서 enabled1/updateMode0(Normal)을 확인했다. 수정 활성화의 unscaled 대기와 그 Sprite Animator의 scaled 재생을 같은 시계로 쓰지 않는다.

## 7. 독립 Update가 없는 현재 타입과 예정 기능

### 7.1 현재 파일 중 별도 반복 콜백이 없는 타입

| 타입 | 실제 실행 주체 |
| --- | --- |
| Character | 호출된 ApplyDamage/Heal/Revive에서 상태 확정·동기 사건. 원작은 UnitAvatar에 Update 책임도 통합 |
| PlayerSpawner | Awake/Start·OnDie/OnRevive·코루틴. 원작 Spawner의 Update/Late와 역할 구분 |
| PlayerSpriteAnimationSet, Line, DamageInstance | 데이터/조회/값전달. Unity콜백없음 |
| LineDrawer, LineSlasher, CollisionBox | Input/Skill/Attack의 호출 문맥. Slasher→SyncTransforms→Query 순서 명시 |
| Hitbox, BossStructure | 초기 참조 확보·피해 요청 수신. 별도 매 프레임 피해 루프 없음 |
| BossPattern, BossPatternSelector | Boss.Update의 Tick/선택 동기 호출. Priority는 후보 선택 데이터 |
| DummyBoss, NormalProjectile, GoldProjectile, AreaProjectile, LaserProjectile | 별도 Update 선언 없음. BossCharacter/Projectile의 콜백 상속·가상 함수로 연결 |
| BossSpawner, EncounterTrigger | 생성/배치/복구 API·물리 Trigger에 의한 조우. Encounter는 반복 Update 아님 |
| MapManager, StageManager, CutsceneController | 사건·명시함수·코루틴 |
| SaveManager, SaveData | static저장큐/비동기IO |
| UI_Title, UI_Loading, UI_Death | Start/async·사건/API·OnGUI. Update/Fixed/Late는 없음 |

### 7.2 예정 기능 — 구현이 없으면 실제 우선순위도 미정

| 기능 | 현재 상태 | Sephiria에서 확인한 참고 실행 | 우리 우선순위 |
| --- | --- | --- | --- |
| Boss_01 경비로봇5패턴 | 구체 Boss_01.cs 없음. 공통 기반/시험 Dummy와 구분 | AI.Update→Unit.Update/코루틴/공격 사건. Askard/Armadillo 예시이며 같은5패턴 구현 아님 | **미정**. 기존 Boss.Update0에 연결할 경우에도 구체 호출·타이밍 확인 필요 |
| Boss_02~06 | 후속 콘텐츠, 해당 C# 없음 | 원작 보스별 AI/Unit에 분산 | 미정. 제출 범위를 확대하지 않음 |
| UI_Settings | class 없음, Title 설정 버튼 비활성 | UI_OptionsPanel.OnOpened9/OnClosed34/Reset51, 독립 Update 없음, meta0 | 미정. 원작 meta0은 우리 미구현 타입의 설정 아님 |
| AudioController | class 없음, FMOD 음향 연결 미구현 | SoundManager.Awake20/Start44/focus30/pause65·음량 setter88/102/116·PlayBGM172. 독립 Update 없음, meta0 | 미정. 필요하지 않은 매 프레임 루프를 가정하지 않음 |
| TutorialController | class 없음, 후속 범위 | OpenTutorialTrigger.LateUpdate8 L0의 저장/안내, TutorialProp.Start8. 전체 단계 제어기의 동일 구현 미확인 | 미정 |
| DebugController | class 없음 | UI_DevCommandlinePanel.Update511 U0는 자동완성 키, OnOpened175는 입력 사건 구독 | 미정. 예정 IMGUI와 원작 패널을 같은 루프로 간주하지 않음 |
| 최종 HUD animation·일반 Focus/Ending | 현재 즉시 표시/전용 컷씬 기반만 있고 최종 콘텐츠/구체 코드 없음 | ScreenFader/TargetTracker/CutScenePlayer는 부분 참고 | 미정. 미래 Animator mode/코루틴/Update를 임의 선정하지 않음 |
| Update 이동/카메라 시점/VSync 비교 변형 | 앞선 대화의 실험 구상만 있고 미구현 | 이번 조사는 현재 코드/원작 사실 기준 | 미정. 실험안을 현행 설정으로 기록하지 않음 |

## 8. 차이와 순서 위험 목록

아래는 전부 버그 목록이 아니다. **사실**, 그 사실로 가능한 **영향**, 아직 필요한 **확인**을 구분한다. 현재 요청은 비교·분석이므로 수정안의 적용은 하지 않았다.

| ID | 차이/상태 | 사실에 따른 영향·한계 |
| --- | --- | --- |
| D01 | **가장 강한 시각적 끊김 후보** — 이동 U→F | Update가 계산한 변위를 F100이 통째로 소비, 보간 None. 원작은 Body U0. 높은 FPS에도 Player 좌표 정지/누적 이동이 가능하며 카메라와 상대 좌표가 불규칙할 수 있음. 실제 FPS 하락의 입증은 아님 |
| D02 | **주기·상태 원본 차이** — Rendering F수집/L표시 | 원작 몸체 Animator U3는 입력/행동 조건을 봄. 우리는 F결과를 모음. Body만 U로 옮기고 F관찰을 유지하면 오래된/중복/누락 관찰이라는 별도 문제를 만들 수 있음. 현재 코드를 그렇게 바꾸지는 않았음 |
| D03 | **소비가 한 프레임 뒤일 수 있음** — Buff30 | 프레임 N 요청→N+1 Buff30 적용이어도 N+1의 Character0/Attack10/Boss0는 그 전에 실행. 새 SP/AS의 일반 이동/공격 소비는 N+2가 될 수 있고, 예약 Groggy 적용 전 Boss가 한 번 더 진행할 수 있음. 원작 Buff0/즉시 적용과 다르며 현재 정책상 허용 여부 확인 대상 |
| D04 | **쿨타임 경계 입력 후보** | Dash는 Input−100의 요청 검사 뒤 Character0에서 쿨 차감, 홀드 공격은 Character0 검사 뒤 Attack10 차감. 그 프레임 끝에0이 될 쿨을 이전 값으로 보고 거부/재시도 지연 가능. Skill은 요청 함수도 절대 시각 동기화하여 구조가 다름. 실제 체감 미측정 |
| D05 | **공격 시작/진행 단계 차이** | 우리 Character0 시작 뒤 Attack10이 같은 프레임 deltaTime을 누적할 수 있음. 원작 대검 홀드 재시도는 Late0이며 클립 사건 기반. 시작 프레임 표시/타격 시점은 우선순위 숫자만으로 동등하지 않음 |
| D06 | **Camera와 Sprite 선후 차이, 직접 오류 근거 없음** | 원작 Animator U3→Camera L300, 우리 Camera L0→Rendering L200. Camera는 Transform만 읽으므로 Sprite 후행이 이전 월드 좌표 참조를 뜻하지 않음 |
| D07 | **HUD 단계 차이, 우리 후행은 합리적** | 원작 HP/쿨바 U0, 우리 HUD L0. 우리는 앞선 Attack10/Skill20/Buff30 결과를 해당 프레임 Late에 읽을 수 있음. 두 방식이 모두 폴링이며 차이만으로 UX 오류 아님 |
| D08 | **같은0 순서 미보장 — 양쪽 공통** | 우리 Player/Boss/Projectile과 원작 Avatar/Body/AI/Bullet/HUD의0끼리 상대 순서는 명시 안 됨. 관련 소비가 필요한지 호출/사건 흐름으로 판단해야 하며 모든0을 오류로 세지 않음 |
| D09 | **물리 Trigger와 Update 좌표 검사 차이** | 우리 조우/수정 후보는 물리 통지, 원작 Boss 조우는 Spawner U0 영역 검사·Checker U0 후보 선택. 빠른 이동/경계 프레임 시점을 비교할 수 있으나 놓침을 실제로 관측하지 않음 |
| D10 | **정지 시계 차이** | 우리 카메라/생명주기/수정/컷씬은 unscaled 영역, 원작 일반 카메라/Interactable 대기와 기록 PlayTime 일부는 scaled. TimeScale1에서는 차이가 사라짐. 정상 직진의 원인이라고 과장하지 않음 |
| D11 | **시험 패턴의 지연 보충 발사** | Dummy while이 여러 발을 한 Update에 생성할 수 있음. 원작 예시는 공격 사건/EndOfFrame/WaitForSeconds 사용. 프레임 부하 영향 미측정이며 일반 MainWorld 기본 Boss에는 현재 등록 패턴 없음 |
| D12 | **애니메이션 누적 방식 차이** | 우리 최신 Rendering은 경과시간의 소수 잔여를 보존. 원작 Basic은 Timer 만료 시1프레임 진행하고 누적0으로 초기화. 원작도 모든 FPS에서 애니 속도가 완벽하다고 할 수 없음. 위치 지터와 별도 |
| D13 | **UI·문·저장의 실행 모델 차이** | 원작 패널/Animator2D/저장 요청과 우리 IMGUI/Normal Animator/UniTask 큐는 다름. 단순히 유사 클래스의 Update 숫자를 복사할 대상이 아님 |
| D14 | **초기화 경계의 추가 정적 후보, 재현 없음** | PlayerSpawner.Awake0→playerSpawn90→Player.ResetMotion239→CancelDraw269가 Skills를 호출할 수 있음. PlayerCharacter.Awake0이 Skills 참조를 얻었지만 Skill.Awake20의 vfx 준비 전이면 RefreshSkillTime276–287로 미준비 참조에 접근할 가능성이 있음. 같은0 선후/OnEnable 시점 조건의 실제 관측 없음. Update 전수 비교 중 발견한 별도 Awake 검토 후보이지 관측 오류나 전체 초기화 감사 완료가 아님 |

## 9. 순서 문제라고 판정하지 않은 관계

- **Body F100→Rendering F200**, **Camera L0→UI_Draw L500**은 명시적으로 순서가 있다.
- HP/사망 확정 뒤 OnDamaged/OnDie의 구독자가 즉시 실행한다. Stage·Spawner에 Update 번호가 없다는 이유로 사망 복구가 무작위 다음 프레임에 실행되는 구조는 아니다. 구독 중 코루틴을 시작하면 최초 yield 이후부터는 지연된다.
- **Slash 물리 질의 전 동기화가 있다.** `C/Player/LineSlasher.cs:28`은 QuerySlash 직전 Physics2D.SyncTransforms를 호출한다. AutoSyncTransforms0만 보고 Slash가 무조건 이전 물리 위치를 읽는다고 판단하지 않는다. 수정 완료 경계에도 명시적 동기화가 있다(`CrystalCheckPoint:180`).
- Attack10의 GetHitArea는 WeaponMotion/Animator 포즈를 읽지 않는다. 따라서 Normal Animator 평가가 Update보다 뒤라는 이유만으로 낡은 검 포즈 판정 버그를 주장할 수 없다. 코드 타격창과 시각 곡선의 일치는 별도 검사 대상이다.
- 현재 CharacterRendering은 SetFacing/BeginDash/상태 요청에서 Sprite 첫 프레임을 즉시 적용할 수도 있다. 모든 표시 변경을 L200 하나로 제한해 그린 실행도는 부정확하다.
- 세피리아의 Boss·탄환도 상당 부분 Update이며, 원작 전체가 엄격하게 서로 다른 숫자를 가진 완전한 파이프라인은 아니다. 원작의 모든0 사이 순서를 성공 경험만으로 보장된 것으로 간주하지 않는다.

## 10. 확인 범위와 다음 판별

이 문서는 **정적 실행 순서 비교 완료**다. 같은0의 실제 호출 순서, 물리/렌더 프레임별 위치·쿨타임 경계·Groggy 발사 여부, 초기화 후보D14, CPU/GPU/GC 비용은 실행 관측하지 않았다. 명시된 차이가 현재 증상을 얼마나 만드는지는 별도 측정이 필요하다.

먼저 비교할 근거는 D01/D02의 Player·Camera 상대 좌표/Fixed 횟수, D03의 요청·적용·소비 프레임, D04의 입력·쿨만료 프레임이다. 원작과 다르다는 사실만으로 확정 사양을 변경하거나 모든 Update를 Fixed/Late로 이동하지 않는다. 이번 작업에서 Unity 컴파일/Play 재검사·Notion 구현 시퀀스 갱신은 없으며, 이유는 런타임 흐름을 변경하지 않았기 때문이다.

## 11. Update 전환 전 분석 — 이동 수집의 도입 경위와 원작의 판단 원본

2026-11-16 사용자는 Player 위치 적용과 몸체 애니메이션을 세피리아처럼 Update 단계로 맞추는 방향을 제시하고, **먼저 이동 수집이 생긴 이유와 원작의 대응 방식을 분석**하도록 요청했다. 이 절은 그 선행 분석이다. 코드 전환이나 아래 표시 정책의 변경은 아직 수행하지 않았다.

1~10절과 원본 JSON은 앞선 전수 조사 시점의 스냅샷이다. 이번 확인에서도 Body·Character·Rendering 핵심3파일은 그 해시와 일치했다. 별도 작업의 AttackController 변경은 감지했으나 이 이동 수집 분석의 소비 관계를 바꾸지 않으며, 앞선 공격 관련 행 번호를 최신 변경에 대한 검증 기록으로 사용하지 않는다.

### 11.1 왜 우리 Rendering에 이동 수집이 생겼는가

확인 가능한 기록은 다음과 같다.

| 기록 | 확인한 내용 |
| --- | --- |
| [Shared_Instances_Reference_Workflow, 선택4](../01_Choice/Shared_Instances_Reference_Workflow_20261115.md) | Character가 변위를 요청하고 Body가 FixedUpdate에서 누적·충돌·적용. 결과를 Character로 반환하지 않고 표시로 전달하는 책임 분리 |
| [Player_Dash_Sephiria, 선택1·2](../01_Choice/Player_Dash_Sephiria_20261115.md) | 초기 Character.FixedUpdate/Body 구조에서 Character.Update가 모든 일반/대시 변위·타이머를 소유하는 구조로 변경. Body의 Player/Rendering 역참조 제거, Rendering이 Body.ActualMovement 직접 관찰 |
| `GAME_SPEC` P09·F-P01 흐름 | Body.Fixed100의 실제 결과를 Rendering.Fixed200이 수집하고 LateUpdate에서 표시. 실제 결과를 Character로 돌려주지 않음 |
| `CharacterRendering.cs:77–86,127–139` 주석/구현 | 물리 갱신 여러 번의 이동을 보존하고 물리 관찰이 없는 렌더 프레임에는 기존 모션 유지 |

이 기록과 현재 코드에서 확인되는 구조적 원인은 **실제 이동 결과로 모션을 고르는 정책**, **Body의 FixedUpdate 적용**, **Body가 표시를 직접 제어하지 않는 책임 분리**를 함께 사용한 것이다. Rendering이 Body 결과를 읽게 되었고, 서로 다른 물리/렌더 주기의 횟수 차이를 연결하려면 수집·보존이 필요해졌다. 이는 원작에 동일 장치가 있어 그대로 가져온 구조가 아니다.

런타임 파일이 아직 Git 미추적이어서 해당 코드의 최초 도입 diff/commit은 확인할 수 없다. 위 기록은 책임 변경의 근거이며, 당시 작성자가 별도로 남기지 않은 판단 이유나 성능 효과를 추정해 확정하지 않는다.

### 11.2 수집하는 값과 실제 용도

| 값 | 생성/소비 | 현재 의미 |
| --- | --- | --- |
| Body.ActualMovement | `PlayerBodyController:35–39`의 ApplyMovement 전후 Rigidbody 위치 차이 → Rendering85 | Body가 이번에 실제 적용한 변위. 다른 코드의 순간이동 등 모든 외부 위치 변화를 추적하는 값은 아님 |
| movementSinceRender | Rendering85 합산 →136의 SetMovement | 실제 이동 여부와 뒷걸음 방향 결정 |
| movementTimeSinceRender | Rendering86 합산 →132/134–135 | 새 물리 관찰이 있었는지 구분, 관찰용 speedRatio 계산 |
| IsMoving | SetMovement217의 변위 크기 검사 | 실제 이동이0이면 Idle,0이 아니면 Move |
| IsBackpedaling | SetMovement218의 실제 변위·조준 방향 내적 | 조준 반대로 움직이면 이동 프레임 역재생 |
| SpeedMultiplier | SetMovement219 저장·초기화만 존재 | **현재 재생 속도에 사용하지 않는 관찰값**. 런타임 읽는 소비자 없음 |
| FacingIndex | SetFacing195–210, BeginDash226–235 | 평상시 조준 방향, 대시 진입 시 대시 방향의 사분면. 이동 수집으로 결정하지 않음 |

Body.MoveDirection도 현재 Body의 쓰기만 있고 읽는 소비자는 없다. Character.MoveDirection은 이와 다른 값으로 이동 입력 의도이며 일반 변위/대시 방향에 사용한다. 같은 이름의 두 필드를 혼동하지 않는다.

실제 일반 모션 진행은 `AdvanceLocomotion:297–307`의 고정 상태 데이터 시간과 deltaTime이며, 뒷걸음일 때만 시간 방향을 뒤집는다. SpeedMultiplier를 곱하지 않는다. 따라서 **이동 수집을 해야 애니메이션 재생 속도를 맞출 수 있다는 설명은 현재 코드와 다르다.**

### 11.3 수집이 현재 구조에서 막는 두 가지 오판

1. **한 렌더 프레임 앞에서 FixedUpdate가 여러 번 실행되는 경우.** 첫 물리 갱신에서 pending 변위가 적용된 뒤, 다음 갱신은 pending0을 소비해 ActualMovement를0으로 쓸 수 있다. Late에서 마지막 ActualMovement만 읽으면 해당 프레임에 실제로 이동했어도 정지로 오판할 수 있다. Fixed200에서 각 갱신의 변위를 합치는 것은 이 손실을 막는다.
2. **FixedUpdate가 없는 렌더 프레임.** 수집 시간0은 새 관찰이 없다는 뜻이다. LateUpdate132는 이 경우 이동 판단을 갱신하지 않아 기존 IsMoving/IsBackpedaling을 유지한다. 관찰 부재를 실제 정지0으로 취급해 Idle/Move가 왕복하는 것을 피한다. CanMove=false/Body 비활성은 명시적으로 정지 처리한다.

이 장치는 **선택할 모션 상태를 안정화**한다. Player 월드 위치·카메라 위치를 보간하지 않으며,50Hz 위치 계단 자체를 해결하지 않는다. 수집 연산의 비용이 실제 프레임 드랍 원인이라는 측정 근거도 없다.

### 11.4 세피리아는 같은 이동 결과를 수집하지 않고 정보 원본을 나눈다

| 표시 판단 | 원작에서 읽는 값 | 근거 |
| --- | --- | --- |
| 이동 입력 | normalized Input와 isInputReceived 플래그 | `UnitAvatar:386,3093–3108,6281,6298` |
| 걷기 허용 | isInputReceived와 moveSpeedMultiplier>0, CanMove, blockMoveByInput==0 | `UnitAvatar:390–415` |
| Idle↔Move | 위 걷기 허용과 접지 상태, 앞/뒤 조준 조건 | `PlayerAvatar:371–405` |
| 좌우 모습 | 조준점.x−몸체 위치.x의 부호로 localScale.x 반전 | `WeaponControllerSimple.Aim:41–51` → `TopdownRigidbody.Direction:18–30` |
| 앞/뒤 모습 | 위쪽 조준 isUpsideAimed로 IDLE/IDLE_BACK·MOVE/MOVE_BACK 선택 | `WeaponControllerSimple:44,51`, `PlayerAvatar:371–389` |
| 걷기 재생 속도 | 이동속도·배율·달리기 배율의 능력치 곱 | `PlayerAvatar:813–814` |
| Sprite 시간 진행 | deltaTime×상태 속도 delegate로 타이머 진행 | `Animator2D_Basic:69–81,118–137,597–604` |

Body는 Update에서 요청을 소비·충돌 보정한다. 하지만 위 애니메이션 판단은 충돌 후 XY 이동량을 입력으로 사용하지 않는다. 접지 같은 물리 상태는 읽되 실제 변위 합계를 수집하지 않는다. `UnitAvatar.PrevVelocity:1185`도 충돌 전 요청 변위이며 몸체 애니메이터의 이동 수집 값이 아니다. 즉, 세피리아는 **같은 수집 문제를 다른 버퍼로 해결한 것이 아니라, 실제 이동 성공 여부를 걷기의 원본으로 삼지 않아 그 수집이 필요 없는 구조**다.

중요한 용어 차이가 있다. **원작 MOVE_BACK은 우리 IsBackpedaling과 다르다.** 확인한 기본 Player 경로에서 MOVE_BACK은 위쪽을 조준해 등을 보이는 이동 상태다. MOVE와 MOVE_BACK은 동일한 양수 속도 delegate를 쓰고, Animator2D는 프레임을 증가시킨다. 실제 이동·조준의 내적을 보고 프레임을 역재생하는 우리 뒷걸음 처리는 해당 원작 경로에서 확인되지 않았다.

### 11.5 두 기준을 바꾸면 달라지는 동작

아래는 코드에서 도출한 동작 차이이며 원작을 실행 관찰한 결과가 아니다.

| 상황 | 현재 우리 실제 변위 기준 | 원작 입력·행동 기준 |
| --- | --- | --- |
| 벽을 누르며 실제 이동0 | 새 물리 관찰 후 Idle | 이동 입력 허용·접지 조건이 유지되면 Move 가능 |
| 벽을 따라 미끄러짐 | 충돌 보정 뒤 방향으로 뒷걸음 판단 | 걷기 여부는 입력 허용, 앞/뒤 모습은 조준 방향 |
| 이동 수치 변화 | 현재 걷기 FPS는 고정, 관찰 SpeedMultiplier는 미사용 | stat 기반 delegate가 걷기 재생 속도에도 영향 |
| 대시 | 전용 Dash 자세 우선, 대시 중 이동 수집 결과는 폐기 | 확인한 기본 PinkRabbit 전환에는 DASH 몸체 상태가 없고, 대시는 변위/잔상으로 분리 |

원작의 속도0도 필드를 구분해야 한다. moveSpeedMultiplier0은 입력 허용식을 끄지만, moveSpeed0이나 localMoveSpeedMultiplier0은 그 식에 직접 포함되지 않는다. 이 세 경우를 모두 같은 걷기 정지 조건으로 요약하지 않는다. 원작 방식의 도입이 현재 정책을 자동으로 개선한다는 결론도 내리지 않는다.

### 11.6 Update 전환에서 없어질 것과 별도 정책 변경

- **실행 단계만 맞추고 현재 표시 정책을 유지하는 경우:** Character.Update → Body.Update → Rendering.Update로 한 렌더 프레임에 한 번씩 순서를 보장하면, Rendering이 그 프레임의 ActualMovement를 한 번 읽을 수 있다. 다중 Fixed 합산과 물리 관찰 시간 누적은 구조상 필요 없어질 수 있다. 실제 이동 기반 Idle/Move·뒷걸음·고정 재생 속도는 유지할 수 있다.
- **판단 원본까지 원작처럼 맞추는 경우:** Rendering이 입력/행동 상태·조준·수치를 읽게 되어 Body.ActualMovement에 대한 표시 의존도 없어질 수 있다. 대신 벽에서의 걷기·우리 역재생 뒷걸음·고정 재생 FPS의 의미를 따로 검토해야 한다. **Update 전환과 동일한 변경이라고 묶지 않는다.**

현행 검사 `PlayerMovementChecks:129`는 실제 뒷걸음을 확인하고,644–677은 독립 Rendering에 SetMovement를 직접 호출한다.591–625의 물리 간격 변경 검사는 대시 총거리 보존을 확인한다. 이 검사들이 다중 Fixed 합산·관찰 없는 프레임의 상태 유지·실제 시각적 부드러움을 모두 직접 검증한 것은 아니다.

이번 선행 분석에서는 코드를 수정하거나 검사를 실행하지 않았다. 전환 방향과 미적용 상태는 [선택3 기록](../01_Choice/Player_Dash_Sephiria_20261115.md)에 남겼다. 원작의 타이머 초과 시간 버림을 복제하거나 현재 확정 재생 시간 정책을 바꾸는 작업도 수행하지 않았다.

## 12. 사용자 확정 반영 — Character.Update0와 Rendering.Update3

2026-11-16 사용자는 Character가 이동량을 전달하고 현재 넉백을 고려하지 않으므로 누적 수집을 제거하고 Update0/3으로 연결하도록 확정했다. [선택4](../01_Choice/Player_Dash_Sephiria_20261115.md)를 반영했다.

```text
Input.Update(−100)
→ PlayerCharacter.Update(0)
    일반 의도 변위·대시 변위 계산
    → Body.Move(합산 변위): 같은 호출에서 충돌 보정·Rigidbody/Transform 적용
    → Rendering.SetMovement(일반 의도 변위, 조준 방향)
    → Character 행동 타이머 진행
→ CharacterRendering.Update(3): 전달된 모션 상태·Sprite 시간·효과 진행
→ Attack.Update(10) → Skill.Update(20) → Buff.Update(30)
→ Camera.LateUpdate(0) / HUD 등 기존 LateUpdate
```

Body는 별도 Update/FixedUpdate를 갖지 않는다. 같은0의 Character와 Body 콜백 순서에 의존하지 않고, **Character.Update0의 직접 호출로 계산→적용을 보장**한다. Rendering의 FixedUpdate·LateUpdate, Body 참조·이동량/물리 시간 합계·미사용 SpeedMultiplier를 제거했다. Body의 RequestMove는 즉시 동작을 표현하는 Move로 바꾸고 모든 런타임/Editor 호출부를 맞췄다. ActualMovement는 마지막 Move의 진단값으로만 유지하며 표시에서 읽지 않는다.

표시는 충돌 후 변위가 아닌 **Character가 계산한 일반 이동 의도**를 사용한다. 따라서 벽이 위치를 막아도 이동 의도가 있으면 걷는다. 대시 성분은 전용 자세로 처리하고, 일반 이동 잠금·속도0·정지·pause·Body/Character 비활성·재배치에서는 이동 표시를 정리한다. 기존 뒷걸음 역재생, 고정 모션 시간, 대시 잔상, Spawn/Death의 unscaled 시간은 유지한다. 새 넉백이나 외력 시스템은 추가하지 않았다.

### 12.1 실제 검증

- 기존 격리 `project/Temp/ProgressionVerification`을 같은 Unity6000.3.21f1로 실행했다. 원본 Editor의 씬은 조작하지 않았다.
- 초기 샌드박스 실행은 LicenseClient IPC 연결/시스템 조회 제한으로 컴파일에 도달하지 못해 중단했다. 같은 격리 실행을 제한 밖에서 재시도한 로그에서 라이선스 연결·컴파일 종료 ExitCode0을 확인했다. 최종 배치 종료도0이다.
- `StartupFlowChecks.RunArt` **19개 검사 전체 성공**, result.success=true/error 빈값. 기존16개 아트/전투 연결에 아래3개를 추가했다.
  1. 실제 Update0/3 선언과 Fixed가 없는 렌더 프레임의 이동, 연속 모션 시간 진행
  2. 뒷걸음·입력0·pause·Body/Character 비활성에서 이동 표시 정리
  3. 즉시 충돌로 위치가 막혀도 의도 걷기는 유지
- 기존 검사도 문 충돌/수정 F/조우·처치/다음 방,4방향 대시·잔상, 사망/같은 Player 부활, 무기1·2·3타/취소, Title 복귀를 포함해 통과했다.
- 검증 후 원본과 격리본의 **C#69파일·Player 프리팹 바이트 일치**를 확인했다. 새 컴파일·검사는 이 스냅샷 기준이며 과거 성공을 재사용한 것이 아니다.

근거: 결과 JSON, 실행 로그, 검증 소스 해시. 로그의 ALL PASSED는19개이며 배치 종료 로그의 마지막 기록은2026-11-16 05:23:12 KST다. 실제 FPS/CPU/GPU/GC 측정이나 원본 Editor에서 사용자의 체감 검토는 이번 자동 검사와 구분한다.

### 12.2 문서·Notion 반영

GAME_SPEC의 P03/P09/U01과 F-P01/F-P02 및 직접 영향받는 F-P03/F-B06 시퀀스, REFERENCE_CODE_MAP의 현행 연결을 갱신했다. [Notion 정리 페이지](https://app.notion.com/p/41857a6d6d24809a8d15f1a5aa34d48a)의 클래스3개·기존9Case·책임 문단·검증 결과 저장 성공을 확인했다. 기능별 독립 토글과 기존 Case 번호를 보존했다. 새 로컬6개 다이어그램은 구조를 검사했으며 실제 Mermaid 파서 실행은 하지 않았다. 미리보기 모드/화면 확인은 기존 정책대로 작업 범위에 포함하지 않는다.

## 13. 해결 경위 — 이동과 몸체 갱신을 같은 렌더 프레임에 연결

### 13.1 증상과 최종 확인

초기 증상은 한 방향으로 계속 이동할 때 화면이 드득거리며 카메라가 순간적으로 따라오는 듯한 체감이었다. 2026-11-16 Update0/3 변경 뒤 사용자가 직접 플레이하고 **“플레이하면서 느껴졌던 드득거림이 사라졌어”**라고 확인했다. 이번 변경 후 해당 이동 끊김이 해소됐다는 사용자 관측 결과로 기록한다. Editor/빌드·모니터·FPS 등 실행환경 상세는 제공되지 않았다.

이번 사례의 설명은 **수동 이동의 위치 적용·모션 판단·카메라가 참조하는 상태를 같은 렌더 프레임에 맞춘 해결**이다. 애니메이션이 LateUpdate에 있으면 언제나 끊긴다거나, Rigidbody를 쓰는 모든 게임의 이동을 Update로 옮겨야 한다는 일반 규칙으로 확대하지 않는다.

### 13.2 처음에 위치 적용을 FixedUpdate에 둔 경위

초기 구현은 다음 책임을 분리했다.

- PlayerCharacter: 입력·이동속도·일반 이동/대시의 변위·행동 타이머
- PlayerBodyController: 변위 유효성·충돌 검사·최종 위치 적용
- CharacterRendering: 모션 상태·Sprite·잔상·색상

이 구조에서 충돌·위치 적용을 Body의 고정 물리 갱신에 배치하고, Character가 계산한 이동 요청을 누적해 다음 FixedUpdate에서 한 번 소비하도록 구현했다. 목적은 **계산과 적용의 역할을 나누고, Update와 물리 호출 횟수가 달라도 이동 요청을 잃거나 중복 적용하지 않는 것**이었다. 초기 Character.FixedUpdate 계산에서 Character.Update 계산으로 바뀐 뒤에도 Body.FixedUpdate는 남았다.

이 경위는 [공유 객체/Body 선택 기록4](../01_Choice/Shared_Instances_Reference_Workflow_20261115.md)와 [이동 책임 선택 기록1·2](../01_Choice/Player_Dash_Sephiria_20261115.md)에 남아 있다. 기록이 직접 뒷받침하는 것은 역할 분리·누적/단일 소비의 목적이다. FixedUpdate가 다른 방식보다 부드럽거나 빠르다는 비교 검증을 거쳤다는 기록은 없으며, 그 별도 판단 이유는 기록되지 않았다.

특히 이 Body는 Rigidbody2D를 사용하지만, 힘·관성으로 이동을 맡기는 구조가 아니었다. Kinematic 몸체에 Character가 계산한 변위를 받아 Cast로 충돌을 확인하고 위치를 직접 바꿨다. 따라서 **충돌/위치를 담당한다는 책임과 FixedUpdate에서 실행해야 한다는 결정은 별개**였다. 원작 TopdownRigidbody도 요청·충돌·직접 위치 적용을 담당하지만 실행 단계는 Update였다.

### 13.3 왜 Rendering도 FixedUpdate 수집과 LateUpdate 표시로 나뉘었는가

당시 모션의 기준은 입력 의도보다 **충돌 후 실제 이동 결과**였다. Body가 Rendering을 직접 제어하는 역참조는 제거했으므로, Rendering이 Body.ActualMovement를 직접 읽었다.

Body가 고정 주기로 이동하는 이상 렌더 프레임과 관찰 횟수가 달랐다. 한 렌더 프레임에 FixedUpdate가 여러 번 실행되면 마지막 결과가0이어도 앞서 이동한 결과를 보존해야 했다. 반대로 FixedUpdate가 없는 프레임에는 관찰 부재를 실제 정지로 취급하지 않아야 했다. 그래서 Rendering.FixedUpdate(200)가 각 결과와 물리 시간을 모았다.

그 후 일반 Update의 처리까지 끝난 LateUpdate(200)에서 수집값으로 걷기·뒷걸음을 판단하고 Sprite 시간을 진행했다. 이 목적은 **최신 관찰을 모아서 표시 상태를 고르는 것**으로 해석할 수 있으며, 코드 주석과 실행 구조가 이를 뒷받침한다. LateUpdate 선택 자체의 별도 성능·화질 실험 근거는 없다.

여기에는 중요한 한계가 있었다. 수집은 “움직였는데 Idle로 잘못 판단하는 것”을 보완할 뿐, **Player 위치가 바뀌는 간격을 보간하지 않았다.** 애니메이션을 LateUpdate에서 매 프레임 진행해도 Player의 월드 좌표는 Body.FixedUpdate가 올 때만 바뀌었다.

### 13.4 기존 구조에서 끊겨 보일 수 있었던 이유

| 단계 | 변경 전 | 변경 후 |
| --- | --- | --- |
| 이동량 계산 | Character.Update(0) | Character.Update(0) |
| 위치 적용 | 다음 Body.FixedUpdate(100)에서 요청 합계 소비 | 같은 Character.Update 안에서 Body.Move 즉시 호출 |
| 모션 판단 원본 | 충돌 후 실제 이동량을 물리 갱신마다 수집 | Character가 계산한 일반 이동 의도 직접 전달 |
| 몸체 시간 진행 | Rendering.LateUpdate(200) | Rendering.Update(3) |
| 카메라 | GameCamera.LateUpdate의 SmoothDamp | 기존 방식 유지 |

변경 전 Fixed Timestep은0.02초, 즉 TimeScale1에서50Hz였으며 Player 위치 보간도None이었다. 렌더링이 그보다 자주 실행되면 Player 좌표가 그대로인 화면과 누적 이동이 반영된 화면이 섞일 수 있었다. 반면 카메라는 매 렌더 프레임 SmoothDamp로 움직였다. **화면상 Player 위치는 Player 월드 위치와 Camera 월드 위치의 차이**이므로, 두 위치의 갱신 간격 차이가 화면에서 드득거리는 상대 이동으로 나타날 수 있었다.

이 설명은 코드·설정과 앞선 정적 모델에 근거한 기전이다. Rendering이 카메라보다 늦게 Sprite를 바꾼다는 사실만으로 카메라가 이전 Player 위치를 읽었다고 설명하지 않는다. 실제 GPU 렌더링과 CharacterRendering이라는 스크립트의 갱신 단계도 구분한다.

### 13.5 현재 판단과 실제 변경

원작 조사에서 위치 적용은 Update, 몸체 Sprite 진행은 Update3임을 확인했다. 원작 걷기 판단은 충돌 후 변위 수집 대신 입력·행동 허용·조준·수치를 사용했다. 현재 게임에서도 Character가 유일한 일반 이동/대시 변위 생산자이며 넉백 등 별도 외력을 아직 고려하지 않는다는 사용자의 판단에 따라 구조를 단순화했다.

현재 한 렌더 프레임의 연결은 다음과 같다.

```text
PlayerCharacter.Update(0)
  → 일반 이동·대시 변위 계산
  → Body.Move: 즉시 충돌 보정·위치 적용
  → Rendering.SetMovement: 일반 이동 의도·조준 전달
CharacterRendering.Update(3)
  → 걷기/정지·뒷걸음·전용 자세 및 Sprite 시간 진행
GameCamera.LateUpdate
  → 해당 프레임에 갱신된 Player 위치 추적
```

Body에 별도 Update0을 만들어 Character와 같은 우선순위의 상대 순서에 의존하지 않았다. **Character.Update0에서 Body를 직접 호출해 계산→적용 순서를 보장**했다. Body의 요청 누적, Rendering의 물리 결과·물리 시간 수집, Rendering의 Body 참조와 미사용 관찰 속도값을 제거했다.

이제 해당 렌더 프레임에 계산한 이동이 같은 프레임의 위치와 표시 상태에 전달된다. 물리 갱신을 기다리는 화면과 뒤늦게 이동량을 따라잡는 화면을 섞을 필요가 없어졌다. 기존 카메라 보간과 VSync 설정을 변경하는 방식으로 해결한 것이 아니다. 원작의 전체 프레임 재생기나 네트워크도 복제하지 않았으며, 우리 고정 모션 시간·뒷걸음 역재생·대시·생명주기 규칙은 유지했다.

### 13.6 해결 결과와 해석의 범위

| 근거 | 확인된 결과 |
| --- | --- |
| 현재 코드·순서 설정 | Character.Update0의 즉시 이동/의도 전달 → Rendering.Update3, Player 이동/몸체에 FixedUpdate 없음 |
| 같은 Unity 격리 검사 | 컴파일·Play19개·배치 종료0. Fixed 없는 렌더 프레임의 이동과 연속 애니메이션을 실제로 확인 |
| 기존 흐름 회귀 | 벽·문·수정·조우/처치·대시/잔상·사망/부활·무기/Title 흐름 통과 |
| 사용자 직접 플레이 | 변경 후 기존의 드득거림이 사라졌다고 확인 |

이 근거들을 함께 보면 **기존 이동·표시 갱신 파이프라인의 주기 불일치가 이번 체감 문제의 핵심 원인이었다는 판단이 강하게 뒷받침된다.** 단, 위치 적용 단계·몸체 갱신 단계·모션 판단 원본을 함께 바꿨으므로 “Rendering의 LateUpdate 하나만이 원인이었다”는 단독 효과를 분리 입증한 실험은 아니다. CPU/GPU/GC·FPS 수치가 개선됐다고 기록하지 않는다.

이번 해결에서 남길 기준은 **이동의 생산·위치 적용·표시·카메라의 소비 시점을 하나의 흐름으로 검토한다**는 것이다. 고정 주기에서 이동해야 하는 기능을 추후 도입하면 표시 보간과 연결 시점도 함께 설계한다. 이번 결과를 근거로 다른 시스템의 FixedUpdate/LateUpdate를 일괄 변경하지 않는다.

이 절과 GAME_SPEC·Notion의 사용자 확인 기록은 문서 갱신이며 새 런타임 수정이나 검사 재실행은 없었다. 자동19개 검사와 사용자 직접 관측은 서로 다른 근거로 보존한다.
