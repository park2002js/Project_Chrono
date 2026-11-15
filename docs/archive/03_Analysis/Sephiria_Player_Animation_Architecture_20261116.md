# Sephiria Player 애니메이션 구조 분석

- 확인일: 2026-11-16 (Asia/Seoul)
- 대상: 로컬 Sephiria 코드·내보낸 에셋의 기본 `PlayerAvatar`와 `PinkRabbit` 스킨, 기본 대검 1·2·3타의 몸체 연결
- 방법: C# 호출부, Unity YAML의 GUID·fileID, AnimationSet의 상태·프레임, Animator Controller·AnimationClip의 직렬화 연결을 대조
- 상태: **정적 분석 완료**. 원작 실행·프레임 녹화·우리 게임의 변경·Unity 실행 검증은 이 문서 작업에 포함하지 않음
- 우리 코드 대응표는 이 문서 작성 시점의 스냅샷이며, 이후 구현 상태의 원본은 `GAME_SPEC.md`

## 1. 먼저 구분해야 하는 두 재생기

Sephiria의 기본 Player는 몸체와 무기를 하나의 Unity Animator가 모두 재생하는 구조가 아니다. **몸체 Sprite 교체는 자체 `Animator2D_Basic` 계열과 `AnimationSet`**, **무기의 위치·회전은 Unity `Animator`와 `.anim`**이 담당한다. 대검 공격의 Unity Animation Event가 몸체 쪽 요청 플래그를 바꾸어 두 재생기를 연결한다. 따라서 `GreatSword_Attack1.anim`의 `m_SampleRate: 30`을 몸체 Idle·Move의 FPS로 해석하면 안 된다.

| 대상 | 실제 재생기·데이터 | 주된 책임 |
| --- | --- | --- |
| 몸체, 스텐실, 물 반사 | `Animator2D_MultipleSpriteRenderer` → `Animator2D_Basic` → `AnimationSet/PinkRabbit` | 상태 판단 결과에 맞는 Sprite 프레임 교체, 프레임별 발소리 |
| 무기·어깨 | `WeaponControllerSimple` → Unity `Animator` → `AnimationWrapper.controller` | 무기별 레이어, 궤적, 공격 이벤트, 콤보 대기·전환 |
| 몸체 위치·좌우 | `TopdownRigidbody`, `WeaponControllerSimple.Aim` | 실제 위치·높이, 부모 X Scale 반전, 위/아래 조준 분류 |
| 대시 잔상 | `CharacterDash_FastMove` → `SilhouetteFx` | 현재 몸체 Sprite를 복사한 별도 표시 |
| 피격·사망 사건 | `UnitAvatar` → 몸체 상태 조건 및 `PlayerSpawner`의 FX | 경직·공중/착지·죽음 상태, 사망 색, 별도 피격·사망 효과 |

## 2. 실제 파일과 참조 연결

이 문서의 `C/`는 `docs/reference/sephiria/code/ingame code/Assembly-CSharp/`, `A/`는 `docs/reference/sephiria/assets/ExportedProject/Assets/`를 뜻한다. 행 번호는 현재 로컬 파일 기준이다.

| 근거 | 파일·주요 행 | 확인 내용 |
| --- | --- | --- |
| 기본 Player | [PlayerAvatar.prefab](../../reference/sephiria/assets/ExportedProject/Assets/GameObject/PlayerAvatar.prefab), 862·1006·1084·1546·1610·1827·2175 | 몸체 animator 참조는 초기 빈값, 기본 스킨 PinkRabbit, 이동속도 4.4·달리기 배율 2, 무기 Animator와 대시 실제 설정 |
| 스킨 선택 데이터 | [costumeskin/PinkRabbit.asset](../../reference/sephiria/assets/ExportedProject/Assets/Resources/costumeskin/PinkRabbit.asset), 15~22 | `skinID`, 스킨 prefab, `canLookBack: 1`, `containsAttackAnimation: 1` |
| 실제 몸체 prefab | [PinkRabbit.prefab](../../reference/sephiria/assets/ExportedProject/Assets/GameObject/PinkRabbit.prefab), 154~164 | Animator2D 스크립트와 AnimationSet 참조, `frameMoveType: 0`, SpriteRenderer 3개 |
| 몸체 프레임 데이터 | [AnimationSet PinkRabbit.asset](../../reference/sephiria/assets/ExportedProject/Assets/MonoBehaviour/PinkRabbit.asset), 16~313 | 상태별 Sprite timeline·repeat·fps·soundEvents |
| 몸체 장착·전이 | [PlayerAvatar.cs](<../../reference/sephiria/code/ingame code/Assembly-CSharp/PlayerAvatar.cs>), 366·757·796·829·1431 | 스킨 생성, 전이 조건, 속도 delegate, 공격 요청 수신 |
| 공통 재생 | [Animator2D_Basic.cs](<../../reference/sephiria/code/ingame code/Assembly-CSharp/Animator2D_Basic.cs>), 31·55·225·277·291·389·402·597 | 초기화, Update 시간, 상태 전환, 프레임·이벤트 적용 |
| 실제 Renderer 쓰기 | [Animator2D_MultipleSpriteRenderer.cs](<../../reference/sephiria/code/ingame code/Assembly-CSharp/Animator2D_MultipleSpriteRenderer.cs>), 10 | 등록한 SpriteRenderer에 동일한 Sprite 적용 |
| 프레임 캐시·보충 상태 | [AnimatorManager.cs](<../../reference/sephiria/code/ingame code/Assembly-CSharp/AnimatorManager.cs>), 32 / [KeyFrameData.cs](<../../reference/sephiria/code/ingame code/Assembly-CSharp/KeyFrameData.cs>), 9~126 | 세트별 캐시, 누락된 DEAD·SNARE 상태 생성 |
| 타이머 | [Timer.cs](<../../reference/sephiria/code/ingame code/Assembly-CSharp/Timer.cs>), 95~114 | 초과분 폐기, 도달 여부 1회 반환 |
| 조준·무기·몸체 요청 연결 | [WeaponControllerSimple.cs](<../../reference/sephiria/code/ingame code/Assembly-CSharp/WeaponControllerSimple.cs>), 41·55·587·1020·1332 | 조준 반전, 공격 방향 고정, AttackSpeed, Attack_Stop |
| 피격·사망 | [UnitAvatar.cs](<../../reference/sephiria/code/ingame code/Assembly-CSharp/UnitAvatar.cs>), 410·2162·2266·2301·2648·2688·4275·6165 | 이동 가능 조건, 경직, 사망·부활, 무적 카운터 |
| 대시 | [CharacterDash.cs](<../../reference/sephiria/code/ingame code/Assembly-CSharp/CharacterDash.cs>), 121·147 / [CharacterDash_FastMove.cs](<../../reference/sephiria/code/ingame code/Assembly-CSharp/CharacterDash_FastMove.cs>), 13·36·58 | 대시 상태, 이동량·잔상·방향별 FX |
| 시작·피격·사망 외부 처리 | [PlayerSpawner.cs](<../../reference/sephiria/code/ingame code/Assembly-CSharp/PlayerSpawner.cs>), 94·355·675·759 | Player 사건 구독, 저장 상태 복구, 별도 FX·게임 오버 |
| 실제 무기 Controller | [AnimationWrapper.controller](../../reference/sephiria/assets/ExportedProject/Assets/AnimatorController/AnimationWrapper.controller), 164·4140·4189 | Greatsword 레이어·Attack1/2 상태 |
| 기본 대검 궤적 | [GreatSword_Attack1.anim](../../reference/sephiria/assets/ExportedProject/Assets/AnimationClip/GreatSword_Attack1.anim), 179·210·258 / [GreatSword_Attack2.anim](../../reference/sephiria/assets/ExportedProject/Assets/AnimationClip/GreatSword_Attack2.anim), 197·228·276 | 30Hz 저작 기준, 클립 길이, 몸체 Attack_Start 이벤트 |

### 2.1 몸체 연결을 확인한 순서

1. `PlayerAvatar.prefab:1610`의 `defaultCostume`는 `PinkRabbit`이다. 저장된 다른 스킨을 사용하면 다른 AnimationSet이 붙으므로 아래 프레임 수는 모든 코스튬의 공통값이 아니다.
2. `Resources/costumeskin/PinkRabbit.asset:19`의 `skinPrefab` GUID `6d92c59f330186943a2846da47781c16`은 `GameObject/PinkRabbit.prefab`이다.
3. `PinkRabbit.prefab:154`의 스크립트 GUID `4354491fea0e497a5cd0d2a2fe75a512`는 `Animator2D_MultipleSpriteRenderer`다. `:157`의 currentSet GUID `a5a5ec293ad1c9040875ae0b95eccf77`은 `MonoBehaviour/PinkRabbit.asset`이다.
4. `PlayerAvatar.OnCurrentCostumeSkinChanged:829` → `UpdateCostumeOutfit:757` → `Instantiate(..., TopdownActor.body):796`으로 몸체를 붙인다. 로컬 위치·회전은 0으로 맞춘다. `:808~816`에서 Renderer·Animator2D 참조, 상태 전이, 상태별 속도 delegate를 연결한다.
5. `Animator2D_Basic.Awake:31`에서 세트를 초기화하고 `Start:40`에서 `SetEntryState:389` → `ChangeState("IDLE")`를 실행한다. PinkRabbit의 초기 프레임 오프셋은 0이다.

### 2.2 초기화·갱신 순서 근거

`A/Scripts/Assembly-CSharp/*.cs.meta:9`의 실행 순서는 `AnimatorManager -10020`, `PlayerAvatar 0`, `UnitAvatar 0`, `WeaponControllerSimple 0`, `Animator2D_MultipleSpriteRenderer 3`이다. 몸체 Animator는 먼저 준비된 전역 프레임 캐시를 사용하고, 캐릭터·무기 쪽 상태 갱신 뒤 몸체 프레임·전이를 검사하도록 의도한 배치다. 동적으로 생성한 몸체의 Awake 초기화와 생성 직후 PlayerAvatar가 하는 연결을 구분해야 한다. 숫자가 같은 PlayerAvatar와 WeaponControllerSimple 사이의 상대 순서는 이 meta만으로 고정되지 않는다.

무기 Animator의 실제 참조는 `PlayerAvatar.prefab:1546`의 fileID `95149520701623706` → 같은 prefab `:1827`의 Controller GUID `f7c8d3c08da2ff245a3c8ee7c2dc4c03` → `AnimationWrapper.controller`다. 같은 Player prefab에 있는 Potion용 Animator를 몸체 Animator로 잘못 연결하면 안 된다.

## 3. 몸체 FPS와 시간의 의미

`Animator2D_Basic`은 현재 상태의 `fps`로 `nextFrameTimer.time = 1 / Clamp(fps, 1, 300)`을 정한다. `Update`에서 `Time.deltaTime × 현재 상태의 속도 delegate`를 타이머에 더하고, 타이머가 도달하면 프레임을 하나 진행한다. 현재 PinkRabbit은 `SCALED`다. 일반 정지로 `Time.timeScale`이 0이 되면 프레임 진행과 이 Update 안의 상태 검사도 멈춘다. `UNSCALED`를 선택한 다른 재생기는 `GameTimeManager.UnscaledDeltaTime`을 쓰지만 기본 Player 스킨의 설정은 아니다.

**`fps × multiplier`는 명목 재생률이다.** `Timer.Update`는 도달 시 남은 시간을 빼서 보존하지 않고 0으로 만든다. 몸체 재생기도 while로 여러 프레임을 따라잡지 않고 Update마다 최대 한 프레임만 진행한다. 따라서 렌더 주기·배율·부동소수 오차에 따라 실제 주기가 길어질 수 있다. 아래 값들은 정적 데이터와 계산이며 원작에서 관측한 실효 FPS가 아니다.

| 몸체 상태 | Sprite 수 / 인덱스 | 세트 FPS | 반복 | 추가 배율·명목 길이 | 세트 시작 행 |
| --- | --- | --- | --- | --- | --- |
| IDLE | 6 / 0~5 | 12 | O | 배율 1, 한 순환 0.5초 | 16 |
| IDLE_BACK | 6 / 0~5 | 12 | O | 위쪽 조준 뒷모습, 0.5초 | 98 |
| MOVE | 8 / 0~7 | 4 | O | `moveSpeed × moveSpeedMultiplier × 달리기 배율` | 35 |
| MOVE_BACK | 8 / 0~7 | 4 | O | MOVE와 동일, 위쪽 조준 뒷모습 | 117 |
| AIRBORNE | 3 / 0~2 | 12 | O | 한 순환 0.25초 | 76 |
| FALL | 1 / 0 | 1 | O | 한 장 유지 | 89 |
| ATTACK | 3 / 0~2 | 20 | X | 마지막 장 도달 0.10초, 종료 판정 명목 0.15초 | 158 |
| ATTACK_BACK | 3 / 0~2 | 20 | X | 위쪽 조준 몸체 공격 | 171 |
| HITFEEDBACK | 4 / 0~3 | 20 | X | 마지막 장 도달 0.15초, 종료 판정 명목 0.20초 | 224 |
| GREATSWORDSWING_UPPER | 10 / 0~9 | 20 | X | 공격속도 배율 × greatsworSwingSpeed | 239 |
| GREATSWORDSWING_LOWER | 10 / 0~9 | 20 | X | 위 상태와 동일 | 266 |
| GREATSWORDSWING_SWEEP | 7 / 0~6 | 30 | X | 별도 sweep 요청 상태 | 293 |
| WEAPONSKILL_WHIRLWINDREADY | 5 / 0~4 | 20 | X | 완료 후 whirlwind 조건으로 전환 | 207 |
| WEAPONSKILL_WHIRLWIND | 8 / 0~7 | 20 | O | 한 순환 0.4초 | 184 |

상태 진입 시 0번을 즉시 표시하므로 마지막 프레임에 도달하는 시간 `(N-1)/fps`와 마지막 프레임까지 한 간격을 유지하고 종료를 판정하는 시간 `N/fps`는 다르다. 반복하지 않는 상태는 끝에서 마지막 Sprite를 유지하며, 화면 상태를 벗어나는 시점은 별도의 전이 조건이 결정한다. 프레임 재생 완료 자체가 공격·경직 같은 게임 행동을 항상 종료시키는 구조가 아니다.

MOVE의 실제 delegate는 `PlayerAvatar.cs:813~814`다. Player prefab의 `moveSpeed=4.4`, `runSpeedMultiplier=2`이고 다른 배율을 1로 가정하면 걷기 명목 17.6fps, 달리기 명목 35.2fps다. `fps:4`만 보고 0.25초마다 걷기 프레임이 바뀐다고 해석하면 틀린다. 예를 들어 일정한 60Hz Update와 배율 4.4를 가정하면 프레임 간격 문턱을 4번의 Update에서 넘으므로 실제 진행은 15fps가 되는 단순 모델이다. 이것은 코드 동작을 설명하는 계산 예시이며 실행 측정이 아니다.

PinkRabbit 세트의 `frameEvents`와 `transformAttributes`는 비어 있다. MOVE·MOVE_BACK은 3·7번 프레임에 발소리 이벤트가 있으며 `Animator2D_Basic.SetFrame:597` → `PlaySound:630`으로 실행한다. 몸체 Sprite 교체에 Unity AnimationClip을 사용하지 않으면서도 자체 프레임 이벤트 기능은 갖춘 구조다.

## 4. 행동별 실제 경로

### 4.1 Idle·Move·조준 방향

- 입력 전달: `PlayerInputController` → `UnitAvatar.Move`/`Stop` → 입력 상태와 이동 가능 조건 갱신
- 몸체 선택: `PlayerAvatar.SetAnimationTransition:366`에 등록한 조건 → `Animator2D_Basic.Update:118`의 순서대로 전이 검사 → `ChangeState` → Sprite 교체
- 이동 조건: `UnitAvatar.IsAvailableMoveByInput:410`은 `IsInputReceived && CanMove && blockMoveByInput == 0`이다. 충돌 후 실제 위치 변화량을 재서 MOVE 여부를 결정하는 조건이 아니다.
- 조준 조건: `WeaponControllerSimple.Aim:41`이 위쪽 조준 여부를 `isUpsideAimed`에 저장하고 X 방향을 `TopdownRigidbody.Direction`으로 전달한다.
- 좌우 반전: `TopdownRigidbody.cs:18~29`에서 루트 `transform.localScale.x`의 부호를 바꾼다. 기본 흐름은 몸체 SpriteRenderer.flipX만 바꾸는 방식이 아니다.

| 현재 상태 → 다음 상태 | 주요 조건 |
| --- | --- |
| IDLE ↔ IDLE_BACK | 위쪽 조준 여부 변경 |
| MOVE ↔ MOVE_BACK | 위쪽 조준 여부 변경 |
| IDLE → MOVE | 아래쪽 조준, 접지, 입력에 의한 이동 가능 |
| MOVE → IDLE | 아래쪽 조준, 입력에 의한 이동 불가능 |
| IDLE_BACK → MOVE_BACK | 위쪽 조준, 접지, 입력에 의한 이동 가능 |
| MOVE_BACK → IDLE_BACK | 위쪽 조준, 입력에 의한 이동 불가능 |

`MOVE_BACK`은 **뒤를 보이는 걷기 Sprite 세트**다. 여기의 BACK을 조준 반대 방향으로 걷는 후진·프레임 역재생으로 해석하면 안 된다. 확인한 PlayerAvatar 전이와 Animator2D 재생기에는 이동 방향·조준 방향의 내적으로 프레임 증가량을 음수로 만드는 분기가 없다. `Animator2D_Basic.Update:81`의 기본 진행은 항상 `currentFrameIdx++`다.

상태를 바꿀 때 새 상태의 0번부터 시작하므로 전·후면 상태 변경 시 프레임 위상을 그대로 공유한다고 보장하지 않는다. 일반 전이 리스트에서 먼저 참이 된 하나를 적용하므로 여러 조건이 동시에 참이면 등록 순서가 영향을 준다.

### 4.2 대검 공격과 몸체 공격

실제 기본 대검 궤적은 `GreatSword_Attack1.anim`, `GreatSword_Attack2.anim`이며 각각 30Hz 저작 기준 0.6초·약 0.733333초다. Unity Animator 상태의 `AttackSpeed`가 이 시간 진행을 배율 조정한다. 상세 궤적·검기 분석은 [대검 1·2타 분석](Sephiria_GreatSword_Attacks_20261115.md)을 따른다.

| 원작 클립 | 몸체 요청 시점 | 몸체가 받는 요청 | 실제 몸체 상태 |
| --- | --- | --- | --- |
| GreatSword_Attack1 | 클립 0.233333초 / F7@30Hz | `CustomAnimationRequest("Attack_Start")` | ATTACK 또는 ATTACK_BACK |
| GreatSword_Attack2 | 클립 0.2초 / F6@30Hz | 같은 요청 | ATTACK 또는 ATTACK_BACK |

이 두 클립의 요청 문자열은 `GreatSwordSwing_Start`가 아니다. 세트에 `GREATSWORDSWING_LOWER/UPPER`라는 긴 몸체 모션이 있다는 이유만으로 기본 1·2타에 그 모션이 연결된 것으로 해석하면 안 된다.

`PlayerAvatar.RequestCustomSpriteAnimation:1431`에서 `Attack_Start`는 `attackingAnimationRequest=true`, `Attack_Stop`은 false로 바꾼다. `PlayerAvatar.cs:379~396`의 전이 조건이 이 플래그를 읽는다. ATTACK의 3장을 20fps로 진행한 뒤에는 마지막 장을 유지하고, 요청 플래그가 내려가면 IDLE 계열로 복귀한다. 이 ATTACK/ATTACK_BACK에는 스킨 장착 코드의 별도 공격속도 delegate가 지정되어 있지 않다. 대검 무기 Animator 속도가 바뀐다고 이 3장의 고유 20fps까지 같은 비율로 자동 변경되는 것으로 보아서는 안 된다.

종료는 `WeaponControllerSimple.EndAttckAnimation:587` → `RpcEndAttackAnimation` → `UserCode_RpcEndAttackAnimation:1332` → `RequestCustomSpriteAnimation("Attack_Stop")`로 이어진다. 강제 행동 취소도 `PlayerAvatar.OnRpcCancelCurrentAction:432`에서 공격·대검·sweep·whirlwind 플래그를 지운다.

`WeaponControllerSimple.Update:55`는 평소 최신 조준점을 사용하지만 공격 또는 시전 중에는 공격 방향을 기준으로 `Aim`한다. 기본 흐름에서 몸체의 위/아래·좌우 선택도 무기 조준 계산을 공유하므로 공격 중 몸체 방향을 매번 새 커서로 바꾸는 현재 우리 코드와 다르다.

**기본 3타는 다른 몸체 경로다.** `AnimationWrapper.controller:4238~4254`의 `GreatSword_Attack3` 상태는 클립 GUID `7467670456da227428059d372f6ebab7`을 참조하고 `AttackSpeed`를 사용한다. 실제 [GreatSword_Attack3.anim](../../reference/sephiria/assets/ExportedProject/Assets/AnimationClip/GreatSword_Attack3.anim)은30Hz 저작 기준·길이0.93333334초(:233·264)이며, `:305~307`에 **0초/F0 `GreatSwordSwing_Start`**, `:340~342`에 **0.76666665초/F23 `GreatSwordSwing_Stop`**을 보낸다.1·2타의 `Attack_Start`와 동일한 몸체 모션이 아니다.

실제 전달은 Player prefab의 `WeaponControlAnimator`(:1847~1850) → [WeaponControlAnimator.CustomAnimationRequest:20](<../../reference/sephiria/code/ingame code/Assembly-CSharp/WeaponControlAnimator.cs>) → `WeaponControllerSimple.CustomAnimationRequest:956` → `PlayerAvatar.RequestCustomSpriteAnimation:1431`이다. Start 분기(:1459~1463)는 `isGreatSwordSwing=true`, `greatsworSwingSpeed=1`을 설정하고 Stop 분기(:1479~1482)는 플래그를 false로 만든다. 강제 취소는 앞서 설명한 `OnRpcCancelCurrentAction:432`에서도 이 플래그를 정리한다.

| 몸체 선택 | 실제 조건·종료 | Sprite·시간 근거 |
| --- | --- | --- |
| GREATSWORDSWING_LOWER | 아래쪽 조준의 IDLE/MOVE + isGreatSwordSwing에서 진입(:381~384), false에서 IDLE 복귀. 뒤 보기 미지원 스킨도 LOWER 사용(:410~413) | PinkRabbit10장·20fps·비반복(:266~289). `SetAnimationSpeed`(:815)가 `(100 + ATTACKSPEED) × 0.01 × greatsworSwingSpeed`를 적용 |
| GREATSWORDSWING_UPPER | 위쪽 조준의 IDLE_BACK/MOVE_BACK + isGreatSwordSwing에서 진입(:394~397), false에서 IDLE_BACK 복귀 | PinkRabbit10장·20fps·비반복(:239~262), 같은 delegate(:816) |
| GREATSWORDSWING_SWEEP | `GreatSwordSweep_Start/Stop`(:1467·1499)이 별도 `isGreatSwordSweep`를 설정. IDLE/IDLE_BACK/MOVE/MOVE_BACK에서 진입하고 false에서 IDLE 복귀(:422~426) | PinkRabbit7장·30fps·비반복(:293~310). 위 장착 코드의 LOWER/UPPER 속도 delegate는 SWEEP에 등록하지 않음 |

기본3타의 Start는 `GreatSwordSwing_StartFast`가 아니다. StartFast 분기(:1486~1490)는 같은 Swing 플래그와 배율3을 설정하지만, 이를 기본3타의 배율로 적용하면 안 된다. SWEEP도 별도 요청/플래그이며 기본3타 클립에는 해당 요청이 없다. 기본3타 몸체의 명목 재생률은 `20 × (1 + ATTACKSPEED/100)`fps이고, 무기 클립 이벤트 시점은 무기 Animator의 AttackSpeed에 따라 진행한다. 몸체10장의 종료와 Swing_Stop 이벤트는 서로 다른 조건이므로 마지막 몸체 프레임을 Stop까지 유지할 수 있다. 원작 Timer 양자화·전이 검사 순서 때문에 화면상 정확한 지연은 런타임 확인이 필요하다.

**우리 이관과 구분:** 우리 Attack3에는 원작 무기 Transform 곡선을 적용했지만, 현재 일반 공격의 전용 몸체 아트가 없어 GREATSWORDSWING_UPPER/LOWER/SWEEP을 복제하지 않았다. 생성한 Idle/Attack1/2/3 클립에는 몸체 요청·피해 Animation Event를 넣지 않았고, 기존 공격 타이머·피해 책임은 AttackController가 유지한다. 따라서 '원작3타의 몸체10장까지 재현 완료'로 보고하지 않는다. 이번 적용·확인 상태의 원본은 GAME_SPEC3.3.11이다.

### 4.3 Dash

`PlayerAvatar.prefab:1034`의 CurrentDashModule은 같은 prefab의 `DashModule_FastMove`에 연결된다. 실제 직렬화 값은 **대시 타이머 0.14초(:2175), 시작 속도 24(:2178)**다. `CharacterDash_FastMove.cs` 필드 기본값 0.33초·20을 기본 Player의 실제 값으로 사용하면 안 된다.

`CharacterDash.StartDash` → `NetworkIsDashing=true` → `OnDashBegin` → 방향·속도 초기화 → `DashMoveUpdate`에서 `Time.deltaTime`으로 이동량·감속·종료를 처리한다. 입력이 있으면 그 입력 방향, 없으면 조준 방향으로 시작한다. 기본 감속은 초당 87이며 다른 스탯 배율·정지 조건의 영향은 별도다.

시각 효과는 `DashMoveUpdate:15~19`에서 약 1/30초 간격의 타이머로 **현재 `bodyRenderer.sprite`**를 복사하는 잔상이다. 각 잔상은 흰색 alpha 0.75, 수명 0.25초, Player의 localScale을 받는다. 시작 시에는 방향 배열에서 별도의 Dash SpriteFx도 선택한다. 이 값들 역시 Timer의 렌더 단위 양자화 영향을 받는다.

확인한 PinkRabbit AnimationSet에는 DASH 상태가 없고 PlayerAvatar의 몸체 전이에도 대시 상태가 없다. 기본 대시 모듈은 몸체 전용 Sprite를 지정하거나 몸체 프레임 재생기를 멈추지 않는다. 따라서 현재 우리 게임의 '방향별 대시 한 장을 고정하고 잔상을 남김'은 우리 아트·규칙에 맞춘 별도 동작이며 원작 기본 몸체 구현과 동일하지 않다.

### 4.4 생성·Spawn·Revive

몸체 재생기의 기본 진입은 IDLE이다. PinkRabbit에는 SPAWN·GETUP 상태가 없으며, 확인한 기본 스킨 장착 경로는 별도의 6프레임 기상 모션을 실행하지 않는다.

`PlayerSpawner.Initialize:355`는 저장 상태와 위치를 복구하며 필요한 경우 `PlayerAvatar.Revive`를 호출하고 다음 층 위치로 이동시킨다. `UnitAvatar.Revive:2301`은 IsDead·HP·사건·보호 상태를 갱신한다. `UserCode_RpcRevive:6165`는 corpse hidden 상태를 풀고 몸체 색을 흰색으로 되돌린다. DEADFALL → IDLE 전이는 `!IsDead`에 따른다.

이는 **기본 Player 몸체 경로에서 별도의 기상 애니메이션을 확인하지 못했다**는 뜻이다. 게임 전체의 층 이동·포탈·컷씬·연출 prefab에 Spawn 효과가 전혀 없다고 결론 내린 것은 아니다. 현재 우리 Spawn 6장은 우리 전용 애니메이션으로 연결해야 하는 영역이다.

### 4.5 피격·경직·보호

`UnitAvatar.StartHitFeedback:2688`는 유효한 hitLevel에 대해 hitStuck을 켜고, 이동을 막고, 현재 행동을 취소하고, 회복 타이머를 시작한다. 몸체 상태는 IDLE → HITFEEDBACK으로 진입한다. 높은 hitLevel에서는 접지 여부에 따라 AIRBORNE/FALL로 이어지고, 경직 해제 후 IDLE로 돌아간다. HITFEEDBACK → IDLE에는 `transitionOnEnd=true`가 있어 경직 해제와 프레임 종료 조건을 함께 본다.

경직 지속시간은 몸체 4장/20fps의 0.2초와 별개다. 예를 들어 level1은 `staggeringScale × 0.25 + 0.25초`, level2·10은 0.66초, level999는 customTime이다. 강제 취소·추가 상태 전환이 있으면 4장을 끝까지 보지 않을 수 있다.

`StartInvulnerable:4275`와 `StopInvulnerable:4281` 자체는 무적 카운터의 증감이다. 이것만으로 빨강/흰색 점멸을 실행하지 않는다. 기본 Player의 `OnDamagedClientside`는 `PlayerSpawner.ShowDamagedUI:675`로 연결되어 피격 음향·별도 damagedFx·CameraShake를 만든다. 확인한 이 몸체·피격 경로에서는 우리 F-P08과 같은 '보호 시간 내내 0.08초마다 빨강/원본색 교대'를 확인하지 못했다. 다른 재질·효과 클래스 전체의 색 변화를 조사한 결과로 확대하지 않는다.

### 4.6 사망

`UnitAvatar.Die:2162` → `StartHitFeedback` → HP 0 / IsDead true → 사망 RPC·사건으로 진행한다. 기본 몸체 상태 그래프의 핵심은 `IDLE 또는 HITFEEDBACK → DEADAIRBORNE → 접지 시 DEADFALL`이며, 공중으로 떠오르면 DEADFALL → DEADAIRBORNE, 부활하면 DEADFALL → IDLE이다.

**PinkRabbit.asset에 DEAD 이름이 없지만, 실행용 프레임 데이터에는 생긴다.** `KeyFrameData.cs:44~62`가 AIRBORNE를 복제해 DEADAIRBORNE를, `:89~107`이 FALL을 복제해 DEADFALL을 추가한다. 이 기본 스킨에서는 사망 공중 3장/12fps 반복, 착지 1장 유지라는 데이터가 된다. 같은 생성기는 SNARE가 없을 때 AIRBORNE의 0번 한 장을 비반복 상태로 추가한다.

`UnitAvatar.DieClientside:2266`은 입력을 비우고 corpse 표시 상태로 바꾸며, Player prefab의 `useCorpseColor:1`에 따라 몸체 RGB를 0.5로 어둡게 만든다. 기본 Player의 `hideCorpseAfterDead`는 0이다. `PlayerSpawner.HandleDieClientside:759`는 사망 FX·음향과 게임 오버 흐름을 처리한다. 우리 전용 Death 6장과 고정 1.42초 연출을 원작 기본 Player에서 그대로 가져온 것으로 기록하면 안 된다.

### 4.7 Draw·Slash

현재 우리 게임의 시간 정지 → 선 편집 → 선 소비 → Slash는 확인한 Sephiria 기본 Player 몸체 상태 목록과 직접 대응하는 기능이 아니다. 원작의 일반 ATTACK, GreatSwordSwing, Whirlwind 상태를 이름이 비슷하다는 이유로 Draw/Slash 대응 상태라고 보지 않는다.

우리 코드에서는 `SkillController.ApplyDraw:98`와 `ApplySlash:206`이 입력·게임 시간·UI·카메라를 전환하고 `CharacterRendering.SetDrawHighlight`로 정렬 순서를 바꾼다. 작성 시점에는 발도 R02 Sprite를 재생하는 몸체 상태 호출이 이 경로에 없다. 보유 아트의 존재, 능력 로직 구현, 몸체 모션 연결은 서로 구분해야 한다.

## 5. 현재 Project_Chrono와의 대응·차이

이 표는 적용 결정을 대신하지 않는다. 원작에서 확인한 책임과 기존 우리 구현의 차이를 보여 주며, 고정 한 무기·서버 없는 소규모 게임에 불필요한 코스튬·네트워크·무기 교체 구조를 추가할 근거가 아니다.

| 기능 | Sephiria의 실제 책임 | 작성 시점 우리 코드 | 적용 시 보존·결정할 차이 |
| --- | --- | --- | --- |
| 몸체 재생기 | Animator2D_Basic + AnimationSet + 파생 Renderer | [CharacterRendering.cs](../../../project/Assets/Scripts/Player/CharacterRendering.cs):80에서 Sprite 배열·phase 직접 진행 | 상태·프레임 데이터·Renderer 적용을 나눌 수 있으나 원작의 전체 코스튬 관리까지 필요하지 않음 |
| Idle | 6장·명목12fps | IdleFrameDuration 0.2초, 방향별 기존 아트 | 원작 수치와 사용자가 확정한 우리 Idle 속도는 다름 |
| Move 판단·속도 | 입력 가능 조건, `4fps × 이동속도 × 배율` | FixedUpdate의 실제 이동량을 모아 LateUpdate에서 판단, 간격0.1초; SpeedMultiplier는 관찰값 | 실제 이동 기준과 우리 속도 규칙을 임의로 입력 기준·원작 배율로 바꾸지 않음 |
| 앞뒤·좌우 | 위쪽 조준 상태 분리 + 루트 X Scale 부호 | 사분면 + SpriteRenderer.flipX | 우리 부모 구조·무기·Collider에 맞춘 반전 책임 필요 |
| 후진 | 별도 역재생 경로 미확인; MOVE_BACK은 뒷모습 | 이동/조준 내적이 음수면 phase 역진행 | 우리 후진 역재생은 별도 확정 규칙으로 유지할 대상 |
| 공격 판단 | PlayerAvatar·WeaponControllerSimple·무기 클래스 | [AttackController.cs](../../../project/Assets/Scripts/Player/AttackController.cs) | 판정·행동 상태는 공격 담당, 표현 재생은 표시 담당으로 연결 가능 |
| 무기 궤적 | AnimationWrapper Unity Animator·위치/각도 곡선 | AttackController의 시간 진행·선형 각도 Lerp | 현재 각도만 보간하는 궤적과 원작 피벗 이동 곡선은 다름 |
| 공격 몸체 | Unity Animation Event → 요청 플래그 → 자체 Sprite FSM | 작성 시점에는 별도 공격 몸체 모션 상태 없음 | 보유한 몸체 아트와 연동 시점 필요 |
| 대시 | 기존 몸체 Sprite 잔상, 기본 prefab 0.14초 | 방향별 전용 한 장 + 잔상8개 재사용 | 우리 대시 아트·길이를 원작 값으로 덮어쓸 이유는 없음 |
| Spawn·Death | IDLE 진입 / 공중·착지 상태 재사용 | Spawn·Death6장, 외부 지정 생명주기 시간, unscaled 진행 | 우리 전용 모션·실시간 연출을 표현 재생기로 옮겨도 의미 보존 필요 |
| 피격 | 경직4장·공중/착지·별도 피격FX | 보호 사건 기반 빨강/원본색 점멸, 경직 없는 확정 규칙 | 원작 경직을 우리 피격 보호와 혼동하여 추가하지 않음 |
| Draw·Slash | 기본 몸체 직접 대응 없음 | SkillController 모드 전환·강조, 발도 모션 미연결 | 우리 전용 상태·시간 정지 중 재생 정책이 필요 |

현재 CharacterRendering의 일반 Idle/Move·잔상은 `Time.deltaTime`, Spawn/Death는 `Time.unscaledDeltaTime`을 사용한다. Slash 능력의 종료 시간은 `SkillController`의 unscaled 시각 차이로 진행한다. 따라서 재생기를 바꿀 때도 일반 정지·Draw 정지·Slash 연출·사망/부활의 시간 정책을 하나로 뭉치면 안 된다.

## 6. 확인 범위와 남은 확인

- 확인: 기본 Player/스킨의 실제 참조, 상태별 프레임 수·FPS·repeat, 속도 delegate, 전이 조건, 기본 대시 prefab 값, 대검 1·2타가 요청하는 몸체 상태, 피격·사망·부활의 몸체 경로
- 확인: 조사한 우리 Player의 현재 표현 코드와 Draw/Slash 호출부. 이 문서 작업은 코드·씬·프리팹을 수정하지 않음
- 미실행: 원작 런타임 캡처, 렌더 FPS별 실효 프레임 간격 측정, Unity Animator 평가와 몸체 Update 사이의 화면상 지연 측정
- 미확인: 모든 코스튬·무기·특수 버프·컷씬에서의 예외, 모든 재질/FX의 피격 색 처리, 각 플랫폼·프레임률에서의 동일한 표시 결과
- 적용 전 확인할 항목: 우리 전용 Spawn/Death·Dash·후진·Draw/Slash 시간 정책, 보유 아트의 실제 프레임/피벗, 공격 취소·사망·씬 복귀 때 재생 상태 복구
- 원작 재생기의 Timer 초과분 폐기와 다중 프레임 추월 불가는 분석 사실이다. 동일한 결함까지 복제하거나 우리 재생 정책으로 채택하기로 결정한 것은 아님
