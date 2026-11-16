# Draw 카메라와 Enter the Gungeon 마우스 추적 비교

- 조사·적용 날짜: 2026-11-16 Asia/Seoul
- 기능: **F-P04**, `GameCamera`의 Draw 중심 계산과 `PlayerInputController → SkillController → GameCamera` 연결
- 현행 사양·실제 컴파일/Play 검사 결과: [GAME_SPEC 2.1.4](../../GAME_SPEC.md#214-f-p04-draw-진입종료입력-전환과-임시-점line-표현)
- 이 문서는 원작에서 읽은 사실, 이전 구현의 문제와 적용 이유를 보관한다. 이후 검사 결과는 위 통합 문서를 원본으로 삼는다.

## 1. 사용자 요청과 선택

- **사용자 확정 방향**: Draw 신호를 받은 카메라는 Enter the Gungeon의 던전 내 플레이어·마우스 조준점 추적 방식을 참고해 조정한다.
- **사용자 이유**: 플레이어 중심에서 보이지 않던 공간까지 커서 방향으로 살펴 Slash 경로를 넓게 선택하려 했으나 기존 움직임이 답답하고 어색했다.
- **적용 판단**: 원작의 마우스 경로에서 실제 사용하는 화면 비율 보정·중앙 무반응 구간·거리별 가중치·이동량 제한과 즉시 중심 갱신을 Draw에 적용한다. 일반 추적/Slash/생성·사망 카메라를 함께 교체하지 않는다.
- 원작에서 확인한 상수를 이번 이식값으로 채택한 것이며, 새 결과의 사용자 체감·최종 튜닝 승인까지 받은 것은 아니다. 숫자별 선택 이유는 아래 원작 근거와 연결된다.

## 2. 실제 읽은 원작과 조사 한계

루트는 `docs/reference/enter the gungeon/`이다. 보관된 `assets/StreamingAssets/version.txt`는 **2.1.9**이다.

| 실제 파일 | 확인한 내용 |
| --- | --- |
| `code/ingame code/Assembly-CSharp/CameraController.cs:169–180` | Start에서 자체 Camera와 PrimaryPlayer 연결 |
| 같은 파일 `:221–239` | Player 중심 선택과 `UseMouseAim` 허용 조건 |
| 같은 파일 `:242–322` | 단일 플레이어의 기준 중심과 마우스 offset 수식 |
| 같은 파일 `:323–368` | 별도 컨트롤러 입력 분기의 SmoothDamp와 RoomLock |
| 같은 파일 `:398–505` | LateUpdate의 중심 계산·offset 합산·마우스 RoomLock 해제·최종 위치 직접 반영 |
| 같은 파일 `:548–551` | 저품질/빠른 균일 배율 옵션에서 최종 위치를 1/16 단위로 양자화 |
| `code/ingame code/Assembly-CSharp/PlayerController.cs:127–142` | `SmoothedCameraCenter`의 실제 계산 |
| `code/ingame code/Assembly-CSharp/GameOptions.cs:1019–1023` | `mouseAimLook = true`, `SuperSmoothCamera`의 필드 기본값 false |
| `code/ingame code/Assembly-CSharp/Pixelator.cs:932–947`, `:3068–3071` | 표시 해상도에 따른 직교 크기와 기본 macro 480×270 |
| `code/ingame code/Assembly-CSharp/BraveCameraUtility.cs:10–56` | 기본 종횡비 16:9와 macro 해상도 계산 |
| `code/README.md`, `assets/README.md` | 보관 코드가 DLL 디컴파일 결과이고 에셋이 Primary Content 출력이라는 자료 범위 |

**정적 코드 분석**이다. 원작을 실행해 던전 입장·프레임·마우스 움직임을 녹화한 결과가 아니다. 에셋의 `Main Camera.glb`는 존재하지만 카메라 MonoBehaviour 설정을 확인할 수 있는 Unity prefab이 아니다. README가 안내한 `project/ExportedProject/`는 현재 이 원작 폴더 아래에 없으므로 실제 프리팹 직렬화값을 읽었다고 주장하지 않는다. `mouseAimLook` 옵션과 원작 플레이어의 실제 저장 옵션값도 서로 구별한다.

## 3. 원작의 던전 마우스 카메라

### 3.1 어떤 때 동작하는가

`UseMouseAim`은 PS4/XboxOne 이외 플랫폼, 마우스 둘러보기 옵션, 키보드·마우스 입력, `!LockToRoom`을 함께 요구한다. 마우스 offset은 단일 플레이어이고 조준 offset 허용 중일 때 계산한다. 총의 종류나 탄환을 카메라의 추가 추적 대상으로 삼는 방식이 아니다. 플레이어 중심이 기준이고, 마우스의 현재 월드 위치가 어느 쪽을 더 보여 줄지 정한다.

일반 단일 플레이어 기준 중심은 `GetPlayerPosition(PrimaryPlayer)`이다. 기본 옵션에서는 `CenterPosition`, SuperSmooth 옵션에서는 `SmoothedCameraCenter`를 사용한다. 이름과 달리 후자는 시간 기반 SmoothDamp가 아니라 hitbox 중심에 Rigidbody 위치의 소수 나머지와 픽셀 배율에 맞는 양자화를 반영하는 계산이다.

### 3.2 정확한 수식

플레이어 기준 중심을 `P`, 현재 카메라로 마우스를 투영한 월드 좌표를 `M`, 화면 종횡비를 `a`, 직교 크기(화면 세로 절반)를 `h`라 한다.

```text
d = M - P
d.x = d.x / a
d.x = clamp(d.x, -1.5h, +1.5h)
d.y = clamp(d.y, -1.5h, +1.5h)

t = clamp01((length(d) - 1) / (h - 1))
w = 0.33333 * sqrt(t)
offset = d * w
if length(offset) < 0.015625: offset = 0

cameraCenter = P + offset
```

- **가로 비율 보정**: 넓은 화면에서 가로 월드 거리만 길어져 카메라가 과하게 옆으로 끌리지 않도록 `x/aspect`를 사용한다. 계산 뒤 aspect를 다시 곱하지 않는다.
- **중앙 구간**: 위 보정을 거친 `|d| ≤ 1`에서는 카메라 offset이 0이다. 플레이어 근처 조준만으로 화면이 계속 밀리지 않는다.
- **거리 반응**: 중앙 구간을 벗어나면 제곱근 곡선으로 가중치가 증가하고 최대 약 1/3에 도달한다. 모든 거리에 같은 고정 비율을 적용하지 않는다.
- **외곽 제한**: 계산에 사용할 조준 벡터를 각 축 `±1.5h`로 먼저 제한하므로 최종 offset은 각 축 최대 약 `±0.5h`다. 사각형 축별 제한이지 반경 제한이 아니다.
- **미세 떨림 억제**: 최종 offset 길이가 1/64보다 작으면 0으로 처리한다.

### 3.3 마우스 분기에는 추가 SmoothDamp가 없다

`GetCoreOffset`의 마우스 분기는 위 offset을 바로 반환한다. 아래에 보이는 `Vector2.SmoothDamp(..., 20f, m_deltaTime)`는 **마우스 분기에서 이미 반환한 뒤의 컨트롤러 전용 계산**이다.

LateUpdate는 플레이어 중심과 offset을 더하고 위치를 직접 기록한다. 마우스 입력일 때 컨트롤러용 RoomLock 상태와 전이도 Off로 만든다. 화면 흔들림·수동 카메라 복귀 같은 별도 연출을 제외한 일반 마우스 추적에 위치 관성을 하나 더 넣지 않는다.

원작도 현재 카메라로 `ScreenToWorldPoint`를 계산한다. 따라서 화면에 마우스를 그대로 두고 카메라만 옮기면 다음 프레임의 월드 마우스 좌표도 변한다. 이 피드백 자체를 원작에 없는 결함으로 설명해서는 안 된다. 원작은 비율·가중치·상한을 적용하고 새 중심을 바로 반영하므로 작은 변화가 프레임 단위로 수렴한다.

### 3.4 줌과 맵 경계의 구별

원작 기본 macro 해상도 480×270과 기본 zoom scale 1에서 `Pixelator.CheckSize()`가 만드는 직교 크기는 `270/32 = 8.4375`다. 이것은 코드로 도출한 기본 경로이며 실제 원작 prefab 직렬화값이나 특정 실행 장면의 관측값이 아니다. 해상도·배율·줌 상태가 달라지면 바뀐다.

마우스 분기는 컨트롤러의 `GetBoundedCameraPositionInRect`를 통과하지 않는다. 우리 게임에 그 상태를 그대로 복사하면 맵 위아래의 빈 공간이 드러날 수 있으므로 기존 세로 맵 제한은 유지한다. Draw의 `h=6.5` 줌도 현재 게임의 표시 사양으로 유지하며 원작 줌 8.4375를 통째로 이식하지 않는다.

## 4. 기존 방식이 답답했던 이유

수정 전 `GameCamera.LateUpdate`는 `Lerp(Player, mouseWorld, 0.35)`로 목표를 만들고 전체 카메라를 `SmoothDamp(..., 0.3초, unscaledDeltaTime)`로 따라가게 했다. Draw 진입 중 줌은 6.5를 향해 초당 8씩 바뀌었다. 일반 추적 시간 0.5초와 별개의 Draw 설정이었다.

| 기존 코드에서 확인한 차이 | 움직임에 미치는 영향과 판단 |
| --- | --- |
| 플레이어와 커서 사이에 고정 0.35 비율 | 작은 조준에서도 화면 중심이 이동하고 원작의 중앙 안정 구간이 없음 |
| 가로 aspect 보정 없음 | 같은 화면 끝을 조준해도 가로 월드 거리가 길어 옆으로 더 크게 이동 |
| 커서 거리의 축별 상한 없음 | 화면 밖/멀리 전달된 좌표에도 이동량이 계속 커짐 |
| 목표 중심에 SmoothDamp 0.3초 추가 | 마우스를 옮긴 프레임에 목표에 도착하지 않고, 멈추거나 반대로 옮겨도 이전 추적 속도를 이어 사용 |
| 카메라 이동/줌 뒤 다음 Update에서 커서 재투영 | 고정 화면 커서의 월드 목표도 다시 변하므로, 위치 감쇠가 쫓는 목표가 계속 갱신됨 |

위 표의 계산 구조는 코드 사실이다. 그로 인해 사용자가 답답함을 느꼈다는 연결은 사용자 피드백과 부합하는 **원인 해석**이며 실제 입력 지연 시간·프레임 저하를 계측한 결과는 아니다. 0.3초를 정확한 지연이나 도달 시간으로 보고하지 않는다. SmoothDamp의 smoothTime은 감쇠 계산의 기준값이다.

### 고정 화면 커서의 정적 비교

플레이어 정지, 맵 경계 없음, Draw 줌 6.5에 도착, 16:9, 화면 끝에 커서를 고정하고 반복 투영·중심 계산이 수렴했다는 조건의 **산술 모델**이다. 실제 게임 캡처나 성능 측정이 아니다.

| 커서 위치 | 이전 고정 0.35의 중심 이동량 | 원작 수식의 중심 이동량 |
| --- | ---: | ---: |
| 오른쪽 끝의 세로 중앙 | 약 6.2222 world | 약 2.6666 world |
| 위쪽 끝의 가로 중앙 | 3.5 world | 약 3.2500 world |

이전 방식은 `C = 0.35(C+screenWorldOffset)`이므로 최종적으로 `C=(0.35/0.65)screenWorldOffset`이다. 원작 수평은 aspect 보정 때문에 `C = (C/a+h)/3`에 가까우며, 수직은 `(C+h)/3`과 축별 상한에 의해 약 `h/2`로 수렴한다. 새 방식은 옆으로 과하게 밀리는 정도를 줄이면서 조준 쪽 공간은 계속 보여 준다.

## 5. Project_Chrono 적용 범위

1. 기존 `SetDrawAim(bool, Vector2)` 진입·갱신과 `Restore()` 종료 API를 유지한다. Draw 상태 전환의 책임을 다른 시스템으로 옮기지 않는다.
2. Draw 중에는 현재 Player 중심과 원작 수식의 offset을 합산해 LateUpdate에서 직접 위치를 반영한다. Draw용 SmoothDamp를 제거한다.
3. 고정 가중치 `DrawAimWeight`와 사용하지 않게 된 `DrawFollowDuration`은 코드·프리팹·설정 생성부에서 제거한다. 남겨 두어 조정이 적용되는 것처럼 보이지 않도록 한다.
4. 일반 이동의 `FollowDuration=0.5`, DrawZoom=6.5의 점진 줌, unscaled 처리, 기존 세로 맵 제한, Draw 종료의 대상/줌 복구는 유지한다.
5. Draw 전용 입력이 정지 중에도 현재 커서를 투영해 `SkillController.UpdateDrawAim`으로 넘기는 연결을 유지한다. 점 선택/Line 기하와 Slash 판정 규칙은 그대로 따른다.
6. Slash·생성/사망의 별도 초점과 줌은 기존 API를 유지한다. 종료/취소 뒤 카메라 속도 상태가 다음 모드로 남지 않도록 확인한다.

원작의 패드 RoomLock, 협동 플레이 평균, 추가 focus object, 픽셀 양자화 옵션·플랫폼별 offset, 화면 흔들림 시스템은 이번 Draw 수정에 필요하지 않아 이식하지 않는다. 원작 데이터 구조 전체를 추가하지 않는다.

## 6. 확인 범위와 남은 판단

- 원작 코드의 마우스/컨트롤러 분리, 수식, 중심 생성과 최종 적용 경로는 실제 파일에서 확인했다.
- 이 문서 작성 시점에는 새 코드의 Unity 컴파일·Play 결과를 완료로 기록하지 않는다. 해당 결과와 미확인 범위는 **GAME_SPEC 2.1.4**에 기록한다.
- 실제 체감은 새 움직임에서 화면 중앙·외곽·방향 반전·Draw 취소·확정·재진입을 함께 보고 판단해야 한다. 원작과 같은 수식을 사용했다는 사실만으로 사용자 체감이 최종 승인됐다고 처리하지 않는다.
