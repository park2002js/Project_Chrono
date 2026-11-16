# 카메라 추적 UX — Sephiria 비교 및 주사율 후보 분석

확인일: 2026-11-16 (Asia/Seoul). 사용자 요청은 원작 코드를 먼저 분석하고 현재 구현과의 차이를 사실로 확인한 뒤 원인을 추론하는 것이다. 해결·튜닝·런타임 코드·씬·프로젝트 설정 변경은 수행하지 않았다. 추가 요청인 화면 주사율도 후보에 포함했다.

## 1. 결론과 근거의 수준

- **확인한 사실:** 양쪽 모두 카메라를 LateUpdate에서 보간한다. 우리 카메라가 일반 이동 중 매 프레임 Player 위치로 즉시 이동한다는 설명은 실제 코드와 다르다.
- **가장 강한 원인 가설:** 우리는 Player 위치를 50Hz FixedUpdate에서만 갱신하고 렌더 위치 보간을 사용하지 않는다. 카메라는 매 렌더 프레임 움직이므로 Player와 카메라의 상대 좌표가 불규칙하게 변할 수 있다. Sephiria의 일반 이동 적용은 Update이다.
- **추가 후보:** 서로 다른 카메라 감쇠 방식·반응, 모니터 주사율과 렌더 FPS의 불일치, VSync/프레임 제시 간격, 실제 CPU/GPU 지연이다. 이들은 같은 현상이라고 단정할 수 없다.
- **미확인:** 증상 발생 당시의 실제 FPS·프레임 시간·활성 품질/VSync·물리 모니터 주사율·VRR·CPU/GPU/GC 비용. 정적 분석과 수식 모델은 실제 프레임 드랍의 원인 확정을 대신하지 않는다.

## 2. Sephiria에서 실제로 읽은 흐름

아래 코드 경로의 공통 루트는 `docs/reference/sephiria/code/ingame code/Assembly-CSharp/`이다. 자료는 제공된 디컴파일 코드와 내보낸 자산이며 원작 실행 관측과 구분한다.

| 확인한 내용 | 근거 |
| --- | --- |
| GameCamera는 Awake에서 Camera를 확보하고 SetObserver가 Player Transform을 TargetTracker에 전달 | `GameCamera.cs:34–40,110–124` |
| 최초 권한 획득 때 SetObserver(PlayerAvatar,true)로 즉시 위치 연결 | `PlayerSpawner.cs:291–299`; `TargetTracker.cs:236–250` |
| PlayerAvatar.Update가 base.Update를 실행 | `PlayerAvatar.cs:349` |
| UnitAvatar.Update가 일반 이동을 계산해 Body에 요청 | `UnitAvatar.cs:1108–1132,1183` |
| 일반 이동 변위는 속도 × Time.deltaTime | `UnitAvatar.cs:3060–3069` |
| Body는 Update에서 요청을 소비하고 Move가 Transform.Translate로 위치 적용 | `TopdownRigidbody.cs:183,275–277,322–324,328–392` |
| 카메라의 실제 추적 함수는 TargetTracker.LateUpdate | `TargetTracker.cs:51` |
| 목표는 Target.transform.position이고 경계·잠금 등으로 보정 | `TargetTracker.cs:82–129` |
| 일반 추적은 current += 9 × Time.deltaTime × (target − current) | `TargetTracker.cs:130–141` |
| 임시 대상은 계수4, 오차 제곱이440을 넘으면 즉시 이동 | `TargetTracker.cs:131–137` |
| 오프셋·블렌딩·흔들림 처리 뒤 최종 Transform 위치 대입, 이 추적 경로에 픽셀 반올림 없음 | `TargetTracker.cs:143–214` |
| 일반 위치 감쇠는 scaled Time.deltaTime, 줌/커서 오프셋은 GameTimeManager.UnscaledDeltaTime | `TargetTracker.cs:62,69,137,141`; `GameTimeManager.cs:19` |

실행 순서는 내보낸 `assets/ExportedProject/Assets/Scripts/Assembly-CSharp/*.cs.meta:9`에서 Input −98, PlayerAvatar/UnitAvatar/TopdownRigidbody/GameCamera 0, TargetTracker 300을 확인했다. 따라서 Update 단계의 이동 결과를 LateUpdate에서 추적하는 구조다. PlayerAvatar와 Body가 같은0이므로 **둘 중 어떤 Update가 먼저인지까지 보장된다고 주장하지 않는다.** 요청이 다음 렌더 프레임에 소비될 수 있어도 Body 소비 자체는 렌더 Update 주기이다.

내보낸 MainWorld에서 TargetTracker는 활성이고 `lookAtMouseDirection:0`이다(`MainWorld.unity:432119,432126`). 커서 방향 추적 코드가 있다는 이유로 원작의 일반 이동에 그 효과가 켜져 있다고 설명하면 안 된다. Camera의 직교 크기는5.625, TargetTexture는null이고 Main Camera 구성에 PixelPerfectCamera는 없다(`:75024,75030,6867–6883`). 전 프로젝트의 후처리 효과 부재까지 뜻하지 않는다. 내보낸 Fixed Timestep은0.05지만 이 일반 이동 경로는 FixedUpdate를 사용하지 않으므로 그 값으로 Player 이동 주기를20Hz라고 해석하지 않는다.

## 3. 현재 게임과의 차이

| 항목 | 현재 게임 | Sephiria |
| --- | --- | --- |
| 이동량 계산 | PlayerCharacter.Update, deltaTime 사용 | UnitAvatar.Update, deltaTime 사용 |
| 실제 Player 위치 적용 | Body.FixedUpdate에서 누적량 전체 소비 | Body.Update에서 누적량 소비 |
| 카메라 대상 | 실제 Player Transform | 실제 Player Transform |
| 카메라 실행 시점 | GameCamera.LateUpdate | TargetTracker.LateUpdate |
| 일반 감쇠 | SmoothDamp, smoothTime0.5, 속도 상태 유지 | 현재 오차에9×deltaTime을 곱해 접근 |
| 위치 감쇠의 시간 | unscaledDeltaTime | deltaTime |
| 일반 이동의 렌더 위치 보간 | Rigidbody2D Interpolation None, 별도 표시 위치 보간 없음 | 일반 위치를 Update에서 적용 |
| 일반 이동 중 강제 snap | 발견하지 못함. Follow(snap:true)는 생성·배치 경계 | 최초 연결·큰 거리 차이 등에 예외 존재 |

우리 근거:

- `project/Assets/Scripts/Player/PlayerCharacter.cs:123–135`: 속도/대시 시간을 반영한 변위를 RequestMove에 전달한다.
- `project/Assets/Scripts/Player/PlayerBodyController.cs:30–41,50–55,105`: 다음 FixedUpdate에서 누적량을0으로 비우고 Body 위치를 이동한 뒤 Transform을 직접 맞춘다.
- `project/ProjectSettings/TimeManager.asset:6`: Fixed Timestep0.02, 정지하지 않은 정상 시간에서50Hz이다.
- `project/Assets/Prefabs/Startup/Player.prefab:479`: m_Interpolate0(None). SP는4이다(`:386–387`). CharacterRendering은 FixedUpdate의 이동량을 수집하고 LateUpdate에서 Sprite를 갱신하지만 별도 렌더 위치 보간은 하지 않는다(`CharacterRendering.cs:69–76,111–139`).
- `project/Assets/Scripts/Systems/GameCamera.cs:46–70`: LateUpdate의 SmoothDamp와 unscaledDeltaTime, 현재 줌을 포함한 y경계 보정이다. 매 프레임 velocity를 초기화하지 않는다. Follow의 snap은`:80–85`, 실제 snap:true 호출은 `PlayerSpawner.cs:101,113`의 Place/Bind이다.
- MainWorld는 WorldCamera 프리팹을 참조한다(`MainWorld.unity:178`). 해당 프리팹의 실제 값도 Follow0.5/Draw0.3/Zoom5.625/VerticalLimits0..77이다(`WorldCamera.prefab:110–113`). 기본 Main Camera는 비활성이다(`MainWorld.unity:310`).

실행 순서 Attribute는 Input−100, Character0, Body100, Rendering200이다. **이 숫자는 Update와 FixedUpdate를 하나의 함수 목록처럼 정렬하지 않는다.** Unity의 FixedUpdate는 렌더 프레임 앞부분에서0회 또는 여러 번 실행될 수 있다. 그러므로 같은 프레임의 Character.Update에서 만든 변위를 Body.FixedUpdate100이 바로 뒤에서 소비한다고 해석하면 틀린다. [Unity 6.3 고정 갱신 설명](https://docs.unity.com/en-us/engine/6000.3/manual/scripting/managing-time-and-frame-rate/fixed-updates)

실제 흐름은 다음과 같다. 게임 시간1·일반 이동을 전제로 한다.

```text
우리:     [FixedUpdate 0~여러 회: 이전 요청 소비/Player 위치 변경]
          → Input.Update → Character.Update: 새 요청 누적
          → Camera.LateUpdate: 마지막 물리 위치를 보간 추적 → 화면 출력

Sephiria: Input.Update → [Character.Update / Body.Update: 계산·소비·위치 변경]
          → TargetTracker.LateUpdate: 위치 감쇠 추적 → 화면 출력
          ※ [] 안 두 Update의 상대 순서는 제공된 meta만으로 확정 불가
```

## 4. 화면 주사율과 렌더 FPS 후보

모니터가 화면을 갱신하는 Hz, 게임이 만드는 FPS, Player 좌표를 적용하는50Hz는 서로 다른 값이다. VSync가 켜진 Desktop에서는 렌더 목표가 주사율/vSyncCount에 묶이고 targetFrameRate는 무시된다. VSync가 꺼지면 두 주기가 자동으로 일치한다고 보장할 수 없다. [Unity 6.3 VSync API](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/qualitysettings/vsynccount), [targetFrameRate API](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/application/targetframerate)

저장 설정에서 확인한 사실:

- 우리 QualitySettings의 저장 current는0/Very Low, vSyncCount0이다(`QualitySettings.asset:7,10,32`). 그러나 Standalone 기본 품질은5/Ultra이며 해당 vSyncCount는1이다(`:270,292,331`). **Editor와 빌드의 동기화 상태가 같다고 가정하면 안 된다.**
- 저장된 두 GameView 레이아웃도 m_VSyncEnabled0이다(`project/UserSettings/Layouts/default-6000.dwlt:599`, `CurrentMaximizeLayout.dwlt:79`). 저장 파일은 증상 발생 당시의 live 값 확인을 대신하지 않는다.
- 프로젝트 C#의 targetFrameRate·vSyncCount·SetQualityLevel·Screen.SetResolution 설정 호출은 발견하지 못했다.
- Sephiria 내보낸 QualitySettings는 current0/Default의 vSyncCount1이다(`docs/reference/sephiria/assets/ExportedProject/ProjectSettings/QualitySettings.asset:7,10,31`). 내보낸 파일만으로 원작 실행 중의 실제 동기화·FPS를 보장하지 않는다.
- 읽기 전용 OS 화면 DC 조회는1920×1080/60Hz를 반환했지만 장치 열거는 비어 있고 CIM도 접근할 수 없었다. 격리된 가상 화면일 수 있으므로 **사용자 물리 모니터의 주사율로 확정하지 않는다.** 실제 게임이 표시된 모니터와 VRR/드라이버 상태는 미확인이다.

### 일정한 높은 FPS에서도 계단 이동이 가능한가

아래는 실제 게임 측정이 아닌 **현재 코드의 누적/소비 규칙을 적용한 산술 모델**이다. 속도4u/s, Fixed0.02초, 충돌/정지 없음, 일정 FPS, 첫1초 제외 후1초를 계산했다. 매 프레임 고정 시간 누적→도래한 Fixed마다 pending을 위치에 더하고0으로 소비→Update에서4/FPS를 pending에 더하는 순서다. 모니터 Hz를 FPS라고 측정하거나 가정한 표가 아니다.

| 일정 렌더 FPS | 1초 중 Player 좌표가 그대로인 렌더 프레임 | 좌표가 변한 렌더 프레임 | 한 번의 위치 변화 범위(u) |
| --- | ---: | ---: | ---: |
| 50 | 0 | 50 | 0.08 |
| 60 | 10 | 50 | 0.066667~0.133333 |
| 100 | 50 | 50 | 0.08 |
| 120 | 70 | 50 | 0.066667~0.1 |
| 144 | 94 | 50 | 0.055556~0.083333 |
| 165 | 115 | 50 | 0.072727~0.096970 |
| 240 | 190 | 50 | 0.066667~0.083333 |

50/100FPS처럼 주기가 정렬돼도100FPS에서는2프레임에 한 번만 Player 좌표가 바뀐다.60/120/144FPS 등에서는 갱신 간격과 누적 소비량도 고르게 일치하지 않는다. 카메라가 매 렌더 프레임 움직이는 동안 Player가 제자리에 있다가 다음 프레임에 더 크게 이동하면, **Player의 화면 위치 = Player 월드 위치 − 카메라 월드 위치**에서 흔들림이 발생할 수 있다. Rigidbody2D 보간의 공식 설명도 그래픽 갱신이 물리보다 잦을 때 계단처럼 보일 수 있는 현상을 명시한다. [Unity 6.3 Rigidbody2D.interpolation](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/rigidbody2d/interpolation)

이것은 높은 주사율 자체가 나쁘다는 뜻이 아니다. 높은 주사율에 맞춰 렌더 FPS만 올라가도 Player 좌표의50Hz 제한은 그대로이므로, 주사율 증가는 현재 구조의 문제를 제거하지 않는다. 반대로 렌더 FPS가 낮아 여러 FixedUpdate가 실행되면 첫 Fixed가 누적 요청을 모두 소비하고 나머지는0을 소비하므로, 일정한 고정 변위를 매 물리 tick에 적분하는 구조와도 다르다.

## 5. 원인 가설의 우선순위와 한계

1. **가장 강함 — 이동 갱신 주기와 표시 주기의 불일치.** 코드·프리팹·시간 설정이 함께 뒷받침한다. 매 프레임 부드럽게 움직이는 카메라와 보간 없는 Player의 상대 위치가 흔들릴 수 있다. 실제 FPS가 떨어지지 않아도 가능한 현상이다. 카메라 월드 좌표 자체가 불규칙하게 점프하는지와 구별해서 측정해야 한다.
2. **추가 UX 후보 — 서로 다른 감쇠와 반응.** SmoothDamp0.5와 오차×9×deltaTime은 같은 알고리즘/같은 시간값이 아니다. 우리 카메라는 속도 상태를 가진 지연 추적이며 원작과 시작·정지·방향 전환의 감각이 다르다. 그러나0.5값 하나만으로 계속 직진 중의 끊김을 입증하지 않는다. TimeScale1에서는 scaled/unscaled 차이도 사라지므로 그 차이만을 정상 직진의 원인으로 삼지 않는다.
3. **추가 표시 후보 — 주사율·FPS·VSync/제시 간격.** Editor 저장 설정은 동기화가 꺼진 후보 근거다. 화면에 프레임이 불규칙하게 제시되는 현상은 FPS 평균과 별도로 확인해야 한다. 고정50Hz 문제와 동시에 존재할 수 있으며 VSync만 원인이라고 확정하지 않는다.
4. **실제 성능 저하 — 미확인.** Profiler의 CPU/GPU/GC·프레임 시간 자료가 없다. 카메라 LateUpdate에는 반복 Find/GetComponent/객체 생성이 없고 일반 이동 경로만으로 CPU 과부하를 입증하지 못했다. Editor.log 마지막 구간에서도 현재 게임플레이 예외의 반복 발생을 확인하지 못했다. 과거 검사 오류를 현재 원인으로 사용하지 않는다.
5. **조건부 보조 후보 — 픽셀 표시·경계·줌 복구.** 현재 Camera에는 PixelPerfectCamera/TargetTexture가 없고 Player는 Point·PPU16이다. 픽셀 표시의 소수점 이동 체감은 가능하지만 원작 Main Camera에도 PixelPerfectCamera는 없어 이 차이로 원인을 확정할 수 없다. y경계의 강제 clamp와 모드 종료의 즉시 줌 복구는 별도 조건에서 영향을 줄 수 있으나, 방 내부에서 한 방향으로 계속 이동하는 일반 증상을 설명하는 우선 근거는 약하다.

원인 확정에 남은 관측은 동일 구간 직진 중 프레임별 deltaTime/Fixed 호출 수/Player·Camera 위치 차이, 활성 VSync와 실제 FPS·주사율, CPU/GPU/GC 프레임 시간이다. 프레임 시간이 안정적인데 상대 위치만 튀는지, 프레임 시간 자체가 치솟는지를 구분하면 위 가설을 분리할 수 있다. **이번 작업은 코드 비교·산술 분석 완료이며 런타임 원인 확정·문제 해결 완료가 아니다.** 런타임 변경이 없어 Unity 컴파일/Play 재검사와 Notion 구현 시퀀스 변경은 수행하지 않았다.

## 6. 후속 분석 — Player 렌더링의 실제 갱신 순서

2026-11-16 추가 요청에 따라 코스튬 생성 → 이동/방향 → Sprite 프레임·상태 → 카메라를 추적했다. 이 절의 렌더링 갱신은 **화면에 그릴 Sprite와 Transform 값을 준비하는 코드**를 뜻한다. GPU가 실제 화면을 그리는 작업이나 Sprite의 앞뒤 그리기 순서(sortingOrder)와 스크립트 실행 순서는 구분한다.

### 6.1 실제 몸체 애니메이터 연결

`PlayerAvatar.UpdateCostumeOutfit`은 선택한 `CostumeSkinEntity.skinPrefab`을 body 자식으로 생성하고, 그 코스튬의 SpriteRenderer와 Animator2D_Basic을 TopdownActor에 저장한다(`PlayerAvatar.cs:795–809`). 이후 앞/뒤·이동/정지 전환 조건과 MOVE/MOVE_BACK의 속도 delegate를 연결한다(`:812–816`). TopdownActorRenderingMetadata 자체에는 Update/FixedUpdate/LateUpdate가 없고, 몸체·Animator·그림자 참조와 설정 API를 보관한다(`TopdownActorRenderingMetadata.cs`).

실제 자산 예시는 내보낸 `Assets/Resources/costumeskin/PinkRabbit.asset:19` → `Assets/GameObject/PinkRabbit.prefab:152–164`이다. PinkRabbit은 PlayerAvatar 프리팹의 기본 코스튬이며(`Assets/GameObject/PlayerAvatar.prefab:1610`), 조회 실패 시 fallback으로도 사용된다(`PlayerAvatar.cs:782`). 이 코스튬 프리팹의 활성 몸체 애니메이터는 **Animator2D_MultipleSpriteRenderer**이며 `frameMoveType:0`(SCALED), body/stencil/reflection 세 SpriteRenderer를 연결한다. PlayerAvatar 프리팹의 TopdownActor.animator/bodyRenderer가 처음에는 비어 있다는 사실만으로 런타임 애니메이터가 없다고 판단하면 안 된다. 모든 선택 코스튬을 PinkRabbit과 동일하다고 일반화하지 않는다.

### 6.2 단계와 실행 순서 근거

아래 `.meta`의 공통 위치는 `docs/reference/sephiria/assets/ExportedProject/Assets/Scripts/Assembly-CSharp/`이며 각 `executionOrder`는9행이다.

| 단계 | 코드/설정 | 실행 시점·확인한 책임 |
| --- | --- | --- |
| 입력 | PlayerInputController, −98 | Update에서 Player 입력 전달 |
| 캐릭터 | PlayerAvatar/UnitAvatar, 0 | Update에서 입력 허용·일반/대시 변위 계산·RequestMove·좌우 방향 적용(`UnitAvatar.cs:1108–1188`) |
| 몸체 | TopdownRigidbody, 0 | Update에서 높이/시각 몸체 위치 보정·누적 요청 소비·충돌 후 Transform.Translate(`:183,255,275–277,392,639–691`) |
| Sprite 애니메이터 | Basic1, Single2, Multiple3 | 실제 컴포넌트의 Update에서 타이머·Sprite 프레임·상태 전환 처리 |
| 그림자 | Shadow, 0 | LateUpdate에서 높이에 따른 alpha 보정(`Shadow.cs:19–36`) |
| 카메라 | TargetTracker, 300 | LateUpdate에서 최종 대상 위치를 감쇠 추적(`TargetTracker.cs:51–214`) |
| 실제 그리기 | Unity 렌더 파이프라인 | 스크립트들이 준비한 Transform/Sprite/Camera 상태로 화면 렌더링 |

Basic1 → Single2 → Multiple3이 한 몸체에서 모두 호출되는3단계가 아니다. **PinkRabbit의 실제 컴포넌트는 Multiple3 하나**이고, 그 클래스가 상속한 Animator2D_Basic.Update를 실행한다. 따라서 확인 가능한 순서는 다음과 같다.

```text
Update:
  Input(−98)
  → [PlayerAvatar/UnitAvatar(0), TopdownRigidbody(0)]
  → 몸체 Animator2D_MultipleSpriteRenderer(3)
      → 타이머·프레임 진행·Sprite 대입·상태 전환
LateUpdate:
  Shadow(0) → TargetTracker(300)
→ 화면 렌더링
```

대괄호 안0끼리의 상대 순서는 제공된 meta만으로 확정하지 않는다. 그러나 **두0의 Update가 모두 끝난 뒤 몸체 Animator3의 Update가 실행되는 설정**은 확인했다. Animator 갱신은 카메라와 같은 LateUpdate가 아니다. LateUpdate는 Update 이후 실행되며, 실행 순서 숫자는 서로 다른 콜백을 한 목록처럼 섞어 정렬하지 않는다. [Unity LateUpdate 설명](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/monobehaviour/lateupdate), [이벤트 실행 순서](https://docs.unity.com/en-us/engine/6000.3/manual/scripting/managing-update-order/execution-order)

### 6.3 Sprite 애니메이션 Update 내부의 순서

1. `Animator2D_Basic.Update:55–79`가 준비/정지/종료 상태를 확인한 뒤 scaled/unscaled deltaTime과 현재 상태의 속도 delegate를 계산한다. PinkRabbit의 SCALED 설정은 Time.deltaTime을 사용한다.
2. 타이머 만료 시 프레임 인덱스를 진행하고 `SetFrame`을 호출한다(`:79–116`). `SetFrame:597–604`가 AnimationSet의 baked frame Sprite를 찾고 가상 SetSprite로 전달한다.
3. `Animator2D_MultipleSpriteRenderer.SetSprite:9–14`가 등록된 Renderer들의 `.sprite`를 직접 바꾼다. 단일 Sprite형도 같은 방식이다(`Animator2D_SpriteRenderer.cs:8–10`). 두 파생 클래스에는 별도 Update/LateUpdate가 없다.
4. 프레임 처리 뒤 전환 조건 delegate를 평가한다(`Animator2D_Basic.cs:118–139`). 조건이 맞으면 ChangeState가 같은 호출 안에서 전환 상태의 첫 Sprite를 바로 적용하고, 상태별 `1/fps` 간격·타이머를 초기화한다(`:402–464`). 따라서 상태 전환 시 한 Update 안에 이전 상태의 프레임 대입과 새 상태의 첫 프레임 대입이 함께 발생할 수 있다.

`AnimatorManager.cs:14–41`은 Instance와 AnimationSet별 KeyFrameData 캐시를 제공한다. 모든 Animator를 중앙에서 순회 갱신하는 관리자가 아니다. 위 몸체 Sprite 재생을 Unity Animator의 내부 애니메이션 갱신 단계나 Animate Physics 모드로 설명하면 안 된다. PlayerAvatar 프리팹에 있는 별도 ItemAnimator 등을 몸체 Sprite 애니메이터로 혼동하지 않는다.

### 6.4 우리 코드와 비교해 의미가 있는 차이

- **움직임과 Sprite 준비가 같은 렌더 Update 주기이다.** 원작은 Body.Update0 → 몸체 Animator.Update3이며, 우리는 Body.FixedUpdate100 → Rendering.FixedUpdate200에서 물리 이동 결과 수집 → Rendering.LateUpdate200의 Sprite 표시이다. 원작 Player 몸체·애니메이터 경로에 FixedUpdate가 끼어 있지 않다.
- **걷기/정지는 물리 틱의 ActualMovement를 재수집해 결정하지 않는다.** 원작 전환 조건은 IsGrounded·IsAvailableMoveByInput과 무기 위/아래 조준을 검사한다(`PlayerAvatar.cs:371–405`). IsAvailableMoveByInput은 `IsInputReceived && CanMove && blockMoveByInput==0`이다(`UnitAvatar.cs:410–415`). 실제 이동량을 수집하는 우리의 Rendering.FixedUpdate와 판단 원본이 다르므로, 원작을 단순히 Update라는 함수 이름만 복사한 것으로 재현할 수 없다.
- **갱신 주기와 애니메이션 그림 수는 다르다.** 원작도 매 렌더 프레임 타이머를 확인하되, Sprite는 해당 클립 간격에 도달하거나 상태가 전환될 때 바꾼다. VSync 값을 애니메이션 속도에 곱하는 처리는 위 경로에 없다.
- **원작 애니메이션을 모든 FPS에서 완벽한 기준선으로 간주하지 않는다.** Basic은 타이머 만료를 if로 한 번만 처리하고, Timer.Update는 만료 시 누적 시간을0으로 비운다(`Timer.cs:95–115`, `Animator2D_Basic.cs:79–116,743`). 초과시간 보존/여러 프레임 따라잡기가 없으므로 낮은 FPS 등에서 애니메이션 진행 시간이 달라질 가능성은 별도로 존재한다. 이는 실제 원작 지연을 측정한 결과가 아니다.

이 근거는 현재 **50Hz 이동 적용·물리 결과 수집과 렌더 표시를 섞은 구조**가 유력한 후보라는 설명을 보강한다. FixedUpdate 자체가 항상 잘못이거나, Rendering을 Update로 옮기기만 하면 실제 FPS 저하까지 해결된다고 확정하지 않는다. 실제 원작/현재 게임 실행 추적과 수정은 이번 추가 분석에서도 수행하지 않았다.

## 7. 후속 비교 — 현재 일반 카메라가 따라오는 시간

2026-11-16 직전 비교 요청 당시 FollowDuration=0.5를 다시 읽은 결과다. 이7절의 값은 **변경 전 비교 이력**이며 이후 사용자 요청으로 적용한 값은8절을 따른다. 위1~6절의50Hz Body.FixedUpdate·물리 결과 수집도 이전 구조의 분석 이력이다. 이동은 PlayerCharacter.Update(0)→Body.Move 즉시 위치 적용→CharacterRendering.Update(3)→GameCamera.LateUpdate이며, 비교만 수행한 이7절 작업에서는 카메라 코드·자산을 변경하지 않았다.

### 7.1 별도 유예 타이머와 보간값의 구분

**양쪽 모두 일반 추적을 시작하기 전 별도로 기다리는 시간은0이다.** Player가 움직이면 다음 LateUpdate부터 카메라가 반응한다. 뒤따라오는 느낌은 추적을 늦게 시작해서가 아니라, 목표까지 한 번에 이동하지 않고 매 프레임 접근하기 때문에 생긴다.

| 항목 | 비교 당시 프로젝트의 일반 추적 | 원작의 일반 추적 |
| --- | --- | --- |
| 별도 추적 시작 대기 | 없음 | 없음 |
| 저장/실제 적용 값 | FollowDuration=0.5초 | 오차에9×deltaTime을 곱하는 코드 상수 |
| 위치 계산 | Vector3.SmoothDamp, 속도 상태를 다음 프레임에 유지 | 현재 위치 +=9×deltaTime×(목표−현재) |
| 값의 의미 | 접근 반응의 기준 시간이며0.5초 뒤 정확히 도착하는 예약이 아님 | 9초 대기가 아니라 초당 접근 계수9 |
| 시간 기준 | Time.unscaledDeltaTime | Time.deltaTime |
| 갱신 | GameCamera.LateUpdate | TargetTracker.LateUpdate, meta 실행 순서300 |

현재 근거는 [GameCamera.cs](../../../project/Assets/Scripts/Systems/GameCamera.cs)10·46~64·85~91과 [WorldCamera.prefab](../../../project/Assets/Prefabs/Startup/WorldCamera.prefab)110이다. 실제 MainWorld.unity178이 이 프리팹 GUID를 참조하며 FollowDuration의 씬 override는 없다. 런타임 Scripts에서0.5를 덮는 대입은 없고, Editor 검사들의0.03/0.05 임시값은 각 검사 finally에서 원값을 복원한다. Follow(target,true)는 생성/재배치 경계에서 즉시 맞추는 별도 경로다.

원작 근거는 [TargetTracker.cs](<../../reference/sephiria/code/ingame code/Assembly-CSharp/TargetTracker.cs>)51·130~141·236~250이다. 실제 소스에는 velocityScaleMultiplier라는 필드가 없으며9는141행의 상수다. 임시 대상 추적의4·임시 대상 유지 타이머·줌/카메라 블렌딩 타이머를 일반 Player 추적의 대기 시간으로 해석하지 않는다. 오차 제곱이440을 넘는 큰 거리 이동은 즉시 맞추는 예외다.

원작 내보낸 자산에는 독립 Main Camera 프리팹이 없어서 실제 [MainWorld.unity](../../reference/sephiria/assets/ExportedProject/Assets/Scenes/MainWorld.unity)를 확인했다. Main Camera는6885~6890에서 활성이고,432118~432126의 TargetTracker도 활성·lookAtMouseDirection=0이다. 같은 Script GUID가 TargetTracker.cs.meta와 일치한다. 일반 추적 계수9를 덮는 직렬화 필드는 없다.

### 7.2 같은 조건의 접근률 비교 — 실제 플레이 측정과 구분

다음은 **처음 카메라 속도0, 목표가1월드 단위를 이동한 뒤 정지, 게임 속도1, 계산 간격1/60초**로 놓은 수식 계산이다. 맵 경계·좌표 잠금·줌/흔들림·임시 대상·큰 거리 snap은 제외한다. Player가 계속 움직이는 상황의 정확한 도착 시각이나 실행 중 FPS/성능 측정값은 아니다.

| 비교 기준 | 변경 전 SmoothDamp0.5 | 원작 오차×9 |
| --- | ---: | ---: |
| 첫 계산에서 새 오차를 줄인 비율 | 약0.206% | 15% |
| 0.1초 뒤 접근률 | 약6.12% | 약62.29% |
| 0.5초 뒤 접근률 | 약59.32% | 약99.24% |
| 90% 접근 시간 | 약0.983초 | 약0.250초 |
| 95% 접근 시간 | 약1.200초 | 약0.317초 |

현재 방식도 첫 프레임부터 움직이지만 초기에 속도를 올리므로 반응이 작고, 원작은 첫 계산부터 남은 오차의15%를 줄인다. 이 조건에서 원작이 더 빨리 붙으며 현재0.5초 값을 “0.5초 기다린 뒤 추적” 또는 “0.5초면100% 도착”으로 설명하면 틀린다.

SmoothDamp의 정의는 설치된 Unity6000.3.21f1의 `Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.xml:67722~67731`에서 확인했다. smoothTime은 목표에 접근하는 데 걸리는 대략적인 시간으로 설명되며 작을수록 빠르다. 숫자 계산은 [Unity 공식 공개 Vector3.SmoothDamp 구현](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Math/Vector3.cs)을 사용했다. 초기속도0·무제한 maxSpeed에서 omega=2/smoothTime과 해당 참조식의 속도 갱신을 반복하고, 원작은 매 계산마다 남은 오차×9/60을 더했다. 공개 구현을 통한 비교 계산이며 설치된 엔진 바이너리나 원작 실행을 계측한 결과는 아니다.

### 7.3 일반 추적과 구분하는 현행 경계

- **Draw는 별도다.** 현행 GameCamera.LateUpdate58~61은 Draw의 Player+커서 오프셋 위치를 즉시 적용하고 속도를0으로 만든다. 위 SmoothDamp0.5 비교를 현재 Draw 카메라에도 적용하지 않는다.
- **맵 끝에서는 목표가 달라진다.** 현재 ClampY는 줌의 세로 반높이까지 포함해 목표·최종 위치를 제한한다. 원작도 경계/잠금으로 목표를 바꿀 수 있다. 따라서 경계 때문에 Player 중앙에 도달하지 않는 경우와 일반 추적 지연을 구분한다.
- **정지 시간의 반응도 다르다.** 현재 일반 추적은 unscaled time이라 게임 시간이 멈춰도 남은 오차에 접근하고, 원작 일반 위치 추적은 deltaTime0이면 진행하지 않는다. 위 표는 시간 배율1 조건이다.

이번 비교로 카메라 값을 수정하거나 기존 이동 끊김의 원인을 다시 판정하지 않았다. 새 Unity 컴파일/플레이·카메라 성능 측정·Notion 구현 Case 변경은 수행하지 않았다.

## 8. 후속 정책 — 일반/컷씬의90% 접근 목표를0.25초로 조정

2026-11-16 사용자 후속 요청은 앞 절에서 비교한 **90% 접근 시간이 약0.25초**가 되도록 현재 카메라를 빠르게 하는 것이다. SmoothDamp 방식은 유지하고 내부 `FollowDuration`은0.5에서 **0.1283초**로 조정한다. 사용자에게 설명한0.25초와 Inspector의 raw smoothTime0.1283은 같은 값이 아니다. 이 절은 선택한 값·계산 조건·실제 호출 관계의 기록이며 컴파일/플레이 확인 결과는 GAME_SPEC의 해당 구현 기록을 따른다.

### 8.1 내부값0.1283을 고른 계산과 한계

초기속도0·고정 목표의 연속 임계감쇠 모델에서 남은 오차는 `(1+omega*t)*exp(-omega*t)`, `omega=2/smoothTime`이다. 남은 오차10%의 해가 약3.88972이므로90% 접근을0.25초로 맞추는 연속식의 smoothTime은 약0.128544다. Unity 공개 SmoothDamp 근사식으로 다시 계산해0.25초 표본에서90%를 조금 넘기는0.1283을 선택했다.

| 계산 간격 | 0.1283에서 처음90% 이상이 된 시각 | 0.25초 표본의 접근률 |
| --- | ---: | ---: |
| 1/30초 | 약0.2667초 | 해당 표본 없음 |
| 1/60초 | 0.2500초 | 약90.0181% |
| 1/120초 | 0.2500초 | 약90.0062% |
| 1/144초 | 0.2500초 | 약90.0092% |
| 1/240초 | 0.2500초 | 약90.0215% |

조건은7.2와 같은 초기속도0·목표가1월드 단위 이동 후 정지·게임 속도1·경계/줌/흔들림 제외다. 실제 FPS나 프레임 시간·기기 성능을 측정한 값이 아니다.30Hz 계산에는0.25초 표본 자체가 없어 다음 계산인0.2667초에90% 통과를 관측한다.

참고 후보0.12855도0.25초에 약89.95~89.96%로 매우 가깝지만 엄격한90% 이상 판정은 다음 계산으로 넘어간다. raw FollowDuration을 단순히0.25로 바꾸면90% 접근은 약0.49~0.5초이므로 사용자 요청과 다르다.90% 시각을 맞춰도 SmoothDamp의 초기 가속과 속도 상태는 유지되며 원작 오차×9와 전체 움직임이 같아지는 것은 아니다. 움직이는 목표·남아 있는 속도·맵 경계에서는 특정 시각의 접근률을 보장하는 도착 타이머로 해석하지 않는다.

### 8.2 우리 컷씬은 같은 Follow 설정을 공유

실제로 읽은 [CutsceneController.cs](../../../project/Assets/Scripts/Systems/CutsceneController.cs)의 흐름은 다음과 같다.

1. `Prepare():43~49`는 이전 요청 Cancel 뒤 해당 MainWorld의 입력/UI/GameCamera 참조를 보관한다. 새 카메라 목표를 직접 지정하는 함수는 아니다. 진행 중 요청이 있었다면 Cancel의 Restore 경로가 먼저 실행된다.
2. `Play():54~82`는 기존 행동을 정리한 뒤 previousTarget을 보관하고 `Follow(boss.transform)`을 호출한다. snap 인자를 생략하므로 기본false이며 현재 카메라 위치에서 새 Boss 목표로 보간한다.
3. `Complete()/Cancel()→Restore():96~127`는 `Follow(previousTarget)`으로 원래 목표를 복원한다. 이때도 기본false이므로 직전 화면 위치에서 복귀 목표까지 같은 설정으로 접근한다.
4. GameCamera.Follow85~91은 목표를 바꿀 때 velocity를0으로 만든다. 따라서 고정 목표·경계 제외라면 목표 변경 직후의 초기속도0 계산 조건과 맞는다. 다만 컷씬 전체 길이와 카메라 접근 시간은 별개다.

Play82의 `SetBossCutscenePaused(true)`는 GameManager의 독립 정지 상태를 통해 Time.timeScale을0으로 만든다(GameManager156~159·241~243). 카메라는 unscaledDeltaTime, 컷씬 남은 시간도 unscaledDeltaTime을 사용하므로 게임 시간이 정지해도 새 목표로 접근하고 복귀한다. 현재 일반 이동과 이 컷씬 목표 전환은 같은 FollowDuration0.1283을 공유하며 별도 대기 타이머를 추가하지 않는다.

전체 연출 길이는 별도다. 실제 [GameSystems.prefab](../../../project/Assets/Prefabs/Startup/GameSystems.prefab)59~61은 첫 조우0.6초·재조우0.2초·처치0.5초이며 StageManager249~250·279가 이 시간을 Play에 전달한다. 따라서 재조우는 같은 추적 설정을 쓰더라도90% 접근 약0.25초 전에 Player로 복귀할 수 있다. 이번 요청으로 이 연출 길이는 변경하지 않았다. 아래 실제 카메라 왕복 측정은1초짜리 BossEncounter를 사용한 공유 Follow 경로의 검사이며,0.2초 재조우에서90%까지 접근했다고 해석하지 않는다.

### 8.3 즉시 이동·Draw·원작 컷씬과의 구분

- **생성/재배치:** PlayerSpawner101·113의 `Follow(player,true)`는 즉시 위치를 맞춘다. 일반/컷씬90%0.25초 정책을 이 snap 경로에도 강제하지 않는다.
- **Draw:** GameCamera58~61의 Player+커서 오프셋 위치는 즉시 적용하고 velocity를0으로 만든다.0.1283 보간은 이 Draw 분기에 적용하지 않는다.
- **원작 컷씬:** [CutSceneSetCameraNode.cs](<../../reference/sephiria/code/ingame code/Assembly-CSharp/CutSceneSetCameraNode.cs>)15~16은 ResetTempTarget 뒤 SetTempTarget(...,immediately)을 호출한다. [TargetTracker.cs](<../../reference/sephiria/code/ingame code/Assembly-CSharp/TargetTracker.cs>)254~281은 임시 대상을 보관/복원하고,임시 추적은137행의 계수4·일반 추적은141행의9를 사용한다. immediately=true면236~250의 즉시 위치 경로다. 따라서 “우리 일반/컷씬이 같은 SmoothDamp값을 공유한다”는 현재 프로젝트의 동작을 “원작 컷씬도 일반9와 같다”로 설명하지 않는다.
- **카메라 블렌딩:** 원작303~318의 SetBlendCamera/ClearBlendCamera는 별도 blendTimer 경로다. 이번 조정은 그 원작 연출 기능을 복제하거나 우리 컷씬에 새 보간 모드를 추가하는 요청이 아니다.

값의 의미는 설치된 Unity XML과7.2의 [Unity 공식 공개 구현](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Math/Vector3.cs)으로 확인했다.8.1~8.3 초기 작성은 정적 분석/정책 정리 범위여서 분석 담당이 카메라 코드·자산을 수정하거나 Notion 구현 Case를 새로 작성하지 않았다. 이후 실제 반영/검증은8.4를 따른다.

### 8.4 실제 반영과 Play 확인 — 정적 계산과 구분

8.1~8.3의 “문서 작업에서 코드·자산을 수정하지 않았다”는 문장은 당시 분석 담당의 연구 범위다. 그 뒤 실제 GameCamera.cs 기본값과 WorldCamera.prefab110에 FollowDuration0.1283을 반영했다. 정적 계산 표와 별개로 다음 격리 Unity Play 결과를 확인했다.

`project/Logs/CameraFollowQuarter.log`·`CameraFollowQuarter-result.json`은 최종 컴파일 성공·10항목 성공·success=true·error 빈값·배치 종료0이다. 실제 일반 추적/시간 정지 추적,실제 CutsceneController.Play의 Boss 진입과 자동 Player 복귀,Follow(true) 즉시 이동·Draw 즉시 위치·기존 Draw5항목·Title 정리를 확인했다.90% 도달 전후의 실제 unscaled 관측 위치를 선형 교차 보간한 값은 다음과 같다.

| 실제 관측 경로 | 관측 수 | 90% 교차 보간 시간 |
| --- | ---: | ---: |
| 일반 Follow(false) | 3 | 약0.249570~0.249577초 |
| 시간 정지 중 Follow(false) | 3 | 약0.249581~0.249585초 |
| 실제 컷씬 Boss 진입, timeScale0 | 1 | 약0.249563초 |
| 실제 컷씬 자동 Player 복귀, timeScale0 | 1 | 약0.249218초 |

이는 **8개 실제 시간 관측이며30/60/144FPS에서 각각 실측했다는 결과가 아니다.** 검사에서 해당 capture 간격을 요청했지만 unscaled 관측 간격이 그 값으로 고정되지 않았다. 각 관측의 최대 표본 간격은 약0.000858~0.010258초였다. 경계 제외·초기속도0·한 표본 허용 조건이며,자동 복귀 관측의 시작 거리는 약0.9996719로 일반/진입의1.0과 조금 다르다. 도달 전/후 관측 하한·상한과 교차 보간값은 `project/Logs/CameraFollowQuarter-metrics.json`의 각8개 레코드에 보존한다. 위 보간 시간을 모든 환경에서 정확히 같은 시각에 도착하는 보장값으로 설명하지 않는다.

`CameraFollowQuarter-verification.json`은 관련13파일의 원본/격리 일치·최종 baseline 변경0과 카메라 프리팹 GUID 보존을 기록하며 baseline은 `CameraFollowQuarter-source.json`이다. 해당 카메라 범위의 확인이고 동시 무관7개 런타임은 미동기화 상태여서 전체 최신 원본 검증으로 확대하지 않는다. 원본 Editor 수동 Play·제출 빌드·성능 측정은 미실행이다. 로그에서 기존 UnityEditor.Search.SearchInit.IndexationOnStartup의 ArgumentOutOfRangeException을 관찰했으며,이 검색 초기화 예외의 미해결 상태와 이후 게임 검사10항목/배치0 성공을 구분한다.
