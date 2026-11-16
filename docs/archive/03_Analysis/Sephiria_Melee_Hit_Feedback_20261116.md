# Sephiria 대검 적중 피드백과 프로젝트 음향 연결 분석

2026-11-16. 원작 코드·프리팹·SpriteSet과 추출 매핑을 확인한 분석이다. 현재 구현은 `PlayerFeedback`, `AttackController`, `CollisionBox`, `PlayerCharacter`에 연결했으며 공격/피드백23묶음 격리 Unity 실행 확인을 마쳤다. 사용자 선택은 FMOD 설치 대신 원본 음원을 WAV로 추출해 Unity 내장 AudioSource로 재생하는 방식이다.

아래 원작 C#의 기준 경로는 `docs/reference/sephiria/code/ingame code/Assembly-CSharp/`, 프리팹·Sprite·Material의 기준 경로는 `docs/reference/sephiria/assets/ExportedProject/Assets/`다. 프로젝트 구현 경로는 `project/Assets/Scripts/Player/PlayerFeedback.cs`·`AttackController.cs`·`PlayerCharacter.cs`와 `project/Assets/Scripts/Systems/CollisionBox.cs`다.

## 1. 어떤 사건에서 무엇을 재생하는가

원작 `MeleeCollision.cs:168~210`은 피해 성공 뒤 RpcCollision을 호출하고, `321~335`는 Success 또는 Fail_Block에 적중 FX를 표시한다. `337~343`의 소리는 Success이며 CombatBehaviour가 있는 경우에만 재생한다. `DamageInstance.cs:44~70`은 피해 좌표와 정규화 방향을 전달한다. `MeleeCollision_Circle.cs:32~35`의 zero-distance CircleCast 결과 `hits[i].point`가 원작 위치 근거이며 피격자 중심이나 별도 ClosestPoint 계산이 아니다.

우리 일반 공격은 `CollisionBox.Query(center,size,angle,onHit)`에서 `collider.ClosestPoint(Results[i].point)`와 해당 호출의 angle을 피해 처리 전에 보존한다. `Hitbox.ReceiveHit`가 true인 수신자마다 선택적 callback을 한 번 호출한다. Character는 기존 수신자 중복 집합, 구조물은 기존 구조물 집합을 사용한다. 무적·회피·피해 거부에는 재생하지 않는다. 피해 사건이 공격을 취소해도 이미 성공한 타격의 좌표·각도는 보존하고 나머지 질의는 기존 취소 계약대로 중단한다. 사용자가 요청한 **플레이어가 피해를 준 대상의 피드백**이며 플레이어 자신이 맞을 때의 별도 피격음은 이번 범위에 없다. 원작의 Fail_Block 표시를 새로 복제하지 않는다.

첫 실제 Play 검사에서 사각 Cast의 raw point=(80.13,80.82)가 중심(80,81)·extents(0.04,0.04)의 작은 수신 Collider 밖에 약0.166 떨어져 관측됐다. 따라서 zero-distance 사각 Cast 접점을 그대로 FX에 쓰던 초기 연결을 수정했다. 수신 shape의 ClosestPoint로 먼저 붙이고 그 뒤 원작 반지름0.2 난수를 적용한다. Results의 raw point는 변경하지 않으며 피해 콜백 이전에 보정값을 snapshot한다. 이는 원작 CircleCast의 raw 접점 사용과 구분한 프로젝트 이식 보정이다. 수정 후 최종23묶음에서 성공 callback과 접점/난수 범위를 재확인했다. 추가 관측에서 ClosestPoint 결과는 물리 접촉 여유0.01까지 포함했다. 수학적 Collider bounds보다0.01 밖이라는 이유만으로 런타임 실패로 판단하지 않으며 검사는 contact offset을 허용한다. 이는 별도 런타임 수정을 요구한 결함과 구분한다.

## 2. 위치·각도·반사

원작 기본 `GreatSwordSwing 1.prefab`·`GreatSwordSwing 2.prefab`의 `fireShape=0`은 Directional이다. `MeleeCollision.cs:170~183`에서 진행 방향은 motionDataEnd−motionDataBegin이며, FX 설정은 angleOffset90·angleError20이다. `MeleeCollision.cs:327~334`는 다음 값을 사용한다.

```text
위치 = hitPoint + Random.insideUnitCircle × 0.2
각도 = GetAngleFromVector(direction) + 90 × Sign(owner.localScale.x) + Random(-10,10)
FX localScale.x *= Sign(owner.localScale.x)
```

`HorayUtility.cs:33~39,58~60`의 각도는 +Y가0인 좌표계다. 우리 공격 각도 θ는 Atan2(y,x)의 +X=0이므로 변환은 다음과 같다.

```text
sign = 공격 방향 x가 음수면 -1, 그 외 +1
rotationZ = θ - 90 + 90 × sign + Random(-10,10)
localScale = (sign,1,1)
```

원작은 공격자 root의 좌우 반사를 읽으며 우리는 공격 시작 방향의 x 부호로 그 반사를 투영한다. 원본 PNG의 긴 축이 +Y이므로 이 변환 뒤 긴 선은 공격 방향에 수직이다. 원작 위치 오차 반지름0.2·회전 오차±10°를 유지한다. null parent로 생성하고 FollowParent를 지정하지 않는 원작과 같이 FX는 생성 당시 월드 좌표에 남긴다. 우리 자식 렌더러도 저장한 월드 위치를 매 Update에 적용하므로 이후 플레이어나 피격자를 따라가지 않는다.

## 3. 실제 FX 자산과 시간

원작 `Resources/meleecollision/GreatSwordSwing 1.prefab:150~152`·2의 FX GUID는 `75832f9f895c46a45ac32476439be6fa`이며 `Resources/fx/SwordSlashFx.prefab`으로 연결된다. 연결된 `MonoBehaviour/SwordSlashFx.asset`은 scaled 모드·7장·20fps·반복 없음·animationSpeed1이다. 필요한 최소 이미지는 다음7개다.

```text
docs/reference/sephiria/assets/ExportedProject/Assets/Texture2D/Slash_Player0.png
docs/reference/sephiria/assets/ExportedProject/Assets/Texture2D/Slash_Player1.png
docs/reference/sephiria/assets/ExportedProject/Assets/Texture2D/Slash_Player2.png
docs/reference/sephiria/assets/ExportedProject/Assets/Texture2D/Slash_Player3.png
docs/reference/sephiria/assets/ExportedProject/Assets/Texture2D/Slash_Player4.png
docs/reference/sephiria/assets/ExportedProject/Assets/Texture2D/Slash_Player5.png
docs/reference/sephiria/assets/ExportedProject/Assets/Texture2D/Slash_Player6.png
```

각 이미지5×57px·PPU16·중앙 pivot(0.5,0.5)·offset0·scale1이다. SpriteSet의 frame GUID는 PNG meta가 아닌 `Sprite/Slash_PlayerN.asset.meta`를 거쳐 연결되므로 import 근거를 구분한다. 첫 장은 흰색, 다음 장은 노란색, 이후 주황색으로 소멸한다. 새 색상을 임의로 입히지 않는다.

| 장 | 명목 표시 구간(게임초) |
| --- | --- |
| 0 | 0~0.05 |
| 1 | 0.05~0.10 |
| 2 | 0.10~0.15 |
| 3 | 0.15~0.20 |
| 4 | 0.20~0.25 |
| 5 | 0.25~0.30 |
| 6 | 0.30~0.35 |

20fps는 장당1/20=0.05초이며 전체 명목 수명은7/20=0.35게임초다. 공격속도 AS는 이 적중 FX에 적용하지 않는다. 원작 `Animator2D_Basic.cs:79~139,386~433`·`Timer.cs:95~115`는 Update당 최대 한 장 진행하며 timer 초과분을 버리므로 실제 수명은 프레임 간격에 따라 명목값보다 늘 수 있다. 우리 구현은 `Time.timeAsDouble−StartedAt`의 절대 경과로 장을 선택해 그 양자화 오차를 복제하지 않는다. 정지 중에는 이 scaled 경과가 멈춘다.

`SwordSlashFx.prefab`의 SpriteFxFlashProperty에는 `_Intensity`를2에서0.25로0.5초 동안 내리는 곡선이 있다. 그러나 실제 연결된 CommonSlashFx.mat의 shader `Universal Render Pipeline_2D_Sprite-Unlit-Default.asset`에는 `_Intensity` 속성이 없다. 따라서 이 값 변경이 원작 화면의 밝기를 바꾼다고 단정하지 않으며 우리도 효과 없는 속성을 모방하지 않는다. 원본 texture·흰 tint·Unlit로 표시한다. 원작 별도 sortingLayer/order0을 현재 프로젝트에 그대로 복제하지 않고 기본 레이어의 order30으로 타일·몸체 위에 둔다.

## 4. 원작 음원 매핑과 현재 선택

원작 `MeleeCollision.cs:303~305`의 RpcSpawn은 swingSoundEvent.Play를 호출한다. 따라서 우리도 공격 시작 입력 자체가 아니라 각 검기/타격 생성 시점에 휘두름 소리를 요청한다. 새3타 두 번째 B 시점은 사용자 확정 VFX 시작을 따른 프로젝트 모션 연결이다.

실제 Bank의 GUID→event→sample 연결은 [추출 README](../../../art/archive/audio/player-feedback-r01/README.md)·[manifest](../../../art/archive/audio/player-feedback-r01/manifest.json)·source-events.json에 있다. Bank 정적 매핑이며 FMOD 이벤트를 실제 실행한 결과와 구분한다.

| 용도 | 원작 event / sample | 우리 파일·선택 |
| --- | --- | --- |
| 대검 기본 휘두름 | attackSwish07 / attackSwish07 | AttackSwingA.wav, 1타와3타 첫 베기 |
| 구분용 두 번째 휘두름 | attackSwish06 / attackSwish06 | AttackSwingB.wav, 2타와3타 둘째 베기 |
| 적중 | CrashSlice / Material별11sample | AttackHit.wav의 hitSlash01 한 개 |
| 바닥 발소리 | footstepRoad / Road01·02·03 | FootstepTile01~03.wav 순환 |

원작 대검1·2타 swingSoundEvent GUID는 둘 다 `8b1b83c1-18c8-4d6b-820d-7ba509da9e18`로 같으며 실제 sample은07 하나다. B의06은 사용자 요구에 맞춘 구별용 선택이지 원작2타 전용음으로 확인한 값이 아니다. CrashSlice GUID는 `114d7ae3-8f7d-4012-8893-14975c761859`이며 원작은 피해 대상 BodyMaterial 파라미터로11개 sample을 선택한다. 우리는 hitSlash01 하나를 사용하며 원작 Material별 분기를 도입하지 않는다. footstepRoad GUID `c3ffb56f-1d96-4826-ac47-15cc9f9d589c`와 Road3개 연결은 확인했으나 원작의 특정 타일 호출부 매핑은 미확인이다.

6개 WAV는48kHz stereo PCM16의 전체 sample이며 trimming·resampling·정규화·pitch 변경 없이 추출했다. 원작 FMOD 이벤트의 gain·random pitch·3D 속성까지 같은 재생으로 간주하지 않는다. 현재 Unity AudioSource.PlayOneShot을 사용하므로 FMOD 설치가 필요하지 않다. 기존 Q22의 FMOD 학습 선택은 이전 이력으로 보존하고 이번 후속 선택을 현재 Player 음향 범위에 적용한다. BGM·전체 음량 UI·대시/Fever/Boss 음향 전체 연결은 미구현이다.

## 5. 프로젝트의 책임과 재생 시점

- `AttackController.Awake`가 자기 PlayerFeedback과 PlayImpact callback을 한 번 캐시한다. 판정0.15게임초·중복 방지·기존 공격 전진은 그대로 유지한다. `SpawnEffect`가1타11/30초·2타7/30초에 각각 A/B를 요청한다. 새3타는 F23(22/60초)에 A, F25(24/60초)에 B를 요청하며 모션 시간에 AS를 적용한다. B는 A의 끝을 기다리지 않고 겹쳐 재생한다.
- `PlayerCharacter.Awake`가 자기 Feedback을 캐시한다. Body.Move 직후의 ActualMovement 중 일반 걷기만 전달한다. 벽에 막힌 변위0·공격/강제 전진·대시에는 누적을0으로 리셋한다. 실제 이동거리1.2마다 Road01→02→03을 순환하고 한 호출에 최대 한 번 재생해 긴 프레임의 소리 몰림을 막는다. 거리1.2·Volume0.8·FootstepVolume4는 프로젝트 초기 튜닝값이며 원작 수치가 아니다. 원본 Road의 작은 sample 진폭을 발소리 배율로 보정한다.
- `PlayerFeedback`은 자기 FeedbackAudio 자식만 소유한다. SlashPresentationFx의 기존 Source를 가져오거나 정지하지 않는다. Awake가 serialized6음원·7frame·Source·Material을 확인하고 FX8개를 미리 준비한다. 동시 적중이 넘칠 때만 늘린 뒤 재사용한다. Update(20)가 적중 Sprite를 진행하며 OnDisable은 자기 Source.Stop·FX 숨김·거리 누적 초기화를 실행한다.
- `OnSoundPlayed`는 PlayOneShot 호출을 관측하는 사건이다. 실제 스피커 청취 품질·최종 음량 승인까지 증명하지 않는다. Source는2D·ignoreListenerPause=true이므로 게임의 scaled 정지와 적중 FX 정지는 이미 재생 중인 음원의 자동 정지를 뜻하지 않는다.

## 6. 확인 상태

**최종 실행·반영(2026-11-16)**: Unity6000.3.21f1 격리 Editor의 공격/피드백23묶음이 success=true·error 빈값·배치 종료0으로 통과했고 컴파일2.672131초·assembly 오류 없음이다. `project/Logs/PlayerFeedbackIntegration-result.json`·`PlayerFeedbackIntegrationFinal.log`·`PlayerFeedbackIntegration-metrics.json`을 근거로 한다. AS1/2의 A/B/A→B,3타 단일 피해·적중음,무적/죽음/회피 거부와 취소 snapshot,수직각/난수·원본7장0.35scaled·월드 고정,실제 일반 걷기2.4566803거리에서 발소리2회,벽/대시/공격 전진/정지/컷씬의 발소리 생략,복제·재활성·Title 정리를 확인했다. PlayOneShot 관측20회는 A6·B4·Hit8·Foot2이며 연결 PCM은 비무음이다.

**접점 수정·적용 범위**: 최초 raw point(80.127304,80.817978)는 작은 수신자 밖에 놓여 ClosestPoint(80.045235,80.951477)로 먼저 보정했다. 접촉 여유0.01을 포함한 허용0.011로 재확인했으며 최초 런타임 결함과 과도하게 작은 검사 오차는 attempt1/2 기록으로 구분한다. `PlayerFeedbackIntegration-verification.json`·`PlayerFeedbackIntegration-publication.json`에서 관련59파일·C#19개·산출물32개가 검증본과 같고 기존 GUID를 보존했다. Player 프리팹은 Feedback 컴포넌트/FeedbackAudio 자식만 추가해 기존 Slash/설정값을 유지한다. 동시 작업 런타임9개와 경비 배치가 바뀐 Map은 동기화/전체 원본 검증 범위에서 제외했다. 카메라 코드는 직전 CameraFollowQuarter 확인본과 같다.

**확인 한계**: `project/Logs/PlayerFeedbackIntegration/hit-feedback-visible.png`는 흰 배경의 실제 Sprite 렌더이며 타일 겹침 검사가 아니다. 수동 스피커 청취·원본 프로젝트 직접 Play·제출 빌드는 미실행이다. 기존 Editor Search 초기화 ArgumentOutOfRangeException은 관찰됐으며 위 assembly/게임 검사 성공과 구분한다.

원본 코드·프리팹·SpriteSet과 추출 매핑의 읽기 분석, 실제 Play 결과, 원본 출판/동일성 확인을 구분해 기록했다. BGM·전체 음량·원작 Material별 라우팅 및 최종 청감 승인까지 완료한 것으로 기록하지 않는다.
