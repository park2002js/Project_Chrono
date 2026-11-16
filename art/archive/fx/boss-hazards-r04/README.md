# 보스 탄환 넓은 윤곽·예고 도형 R04

2026-11-16 사용자 수정: 탄환의 실제 도트를16×16px로 만들고, 상하좌우 바깥 변 중앙의 평평한 부분을 각각8px로 넓혀 마름모화를 막는다. 장판 예고는 원·사각형·부채꼴·끝이 열린 직선 띠를 준비하며 기본 Scale(1,1,1)에서 원과 사각형을 변형해 사용한다.

## 탄환

`normal-bullet.aseprite`·`gold-bullet.aseprite`는 각각18×18px 셀(1px 투명 여백)·실제16×16px·1프레임·3레이어다. 외곽 각 변의 중앙8px는 평평하고 모서리만 깎았다. 두 탄환은 형태·alpha가 같고 R03의 세 가지 색만 유지했다.216개 불투명 픽셀·8px 평면·대칭·이진 alpha·원본 재열기/PNG/프로젝트 동일을 `bullet-validation.json`에서 확인한다.

`build-bullets.lua`는 이번 윤곽 제작 근거다. 이후 사용자 편집본을 덮어쓰는 재생성 용도로 사용하지 않는다. 현재 원본의 PNG 출력은 R01의 `export.lua`에 해당 입력/출력 경로를 넘긴다. Unity의 기존 두 PNG 경로·GUID와16PPU/Point를 유지한다. 판정/CircleCast 반경0.39는 확대 크기에 맞춘 초기값이다.

## 예고 프리팹 사용

아래4개는 `project/Assets/Prefabs/Enemy/Warnings/`에 있으며 모두 기본 Scale(1,1,1)이다. 반투명 빨간 내부와 더 진한 경계를 사용하며 `AreaWarningVfx.WarningColor`·`FillOpacityMultiplier`·`OutlineWidth`에서 표현을 조절한다.

| 프리팹 | 기본 도형 | 사용 예시 |
| --- | --- | --- |
| `CircleWarning.prefab` | 지름1의 원 | Scale(3,1,1)로 가로3·세로1 타원 |
| `RectangleWarning.prefab` | 가로1·세로1 정사각형 | Scale(3,1,1)로 가로3·세로1 직사각형 |
| `SectorWarning.prefab` | 원점에서 위로 펼치는 반경0.5·기본90° 부채꼴 | `SectorAngle`로0.1~360° 조절, Scale(4,4,1)로 반경2.360°는 방사 경계 없이 원으로 표시 |
| `StraightWarning.prefab` | 가로(X)1·폭(Y)1의 열린 띠 | Scale(6,0.5,1)로 길이6·폭0.5. 긴 평행선2개만 표시하며 짧은 양끝 경계선은 없음 |

회전은 Transform의 Z로 적용한다. 이 프리팹은 피해·Collider 없는 순수 예고 표시이며 패턴에서 생성/숨김/제거를 소유한다. 기존 `WarningArea.prefab`의 원형 피해 판정·Radius 계약은 유지한다. 새 표시용 Circle의 기본 지름1과 기존 피해 Area의 Radius1(지름2)을 구분한다.

런타임 API는 `Configure(WarningShape shape, Color color, float sectorAngle=90f)`·`SetColor(Color color)`다. 원·사각형은 공유 Mesh를 사용하고, 부채꼴은 자기 수명에 한 번 만든 Mesh를 각도 변경에 재사용한 뒤 제거 시 정리한다. Inspector 변경은 예약 후 반영하며 평소 반복 검색·생성은 없다. `Tools/Chrono/Prepare Boss Hazard Art`로 연결하고 `Tools/Chrono/Check Boss Hazard Art`로 검사한다.

## 실제 화면·검사

`preview-white.png`·`preview-laboratory.png`는 실제 Unity 카메라1024×768 캡처다. 위쪽 Player·일반탄·황금탄은16PPU·원본1px당 화면6px·같은 바닥 기준선이다. 중간 줄은 원/타원/정사각형, 아래 줄은 직사각형/120° 부채꼴/양끝 열린 직선 띠다.

2026-11-16 09:12:19 KST에 동일 Unity6000.3.21f1의 격리 사본에서 컴파일·어셈블리 재로드·15묶음 Play Mode 성공·배치 종료0을 확인했다. 기존13묶음에4종 순수 표시 계약과 Scale 변형·각도90/120/360·Mesh 재사용·열린 선·반투명색 검사2묶음을 추가했다. 현재 C#84개/런타임49개·이번26대상은 검증 사본과 모두 같다. 기존 원형 Area 관련6대상은 R02/R03과 바이트가 같다. `unity-validation.json`·`integration-validation.json`을 따른다.

Editor 시작의 SearchDatabase 인덱스 예외는 관찰했지만 컴파일·기능 검사는 통과했다. 원본 Editor 직접 Play/import·제출 빌드와 새로운 도형의 보스 공격/피해 판정 연결은 미실행이다. 전체 사양·시퀀스·레퍼런스 근거는 GAME_SPEC B05.1·2.2.6을 따른다.
