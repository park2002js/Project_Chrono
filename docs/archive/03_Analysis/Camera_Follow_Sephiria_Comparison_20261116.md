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
