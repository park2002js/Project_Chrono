# Player 포함 세피리아 대검 1·2타 프레임

2026-11-16. 이전 [우리 검 교체판](../sephiria-motion-our-sword-r01/README.md)에 현재 Player를 추가했다. 검의 위치·회전과 검기 크기·간격·피벗은 이전124표본과 같다.

- [1타 전체30fps — 19장](attack-01-all-30fps.png)
- [2타 전체30fps — 23장](attack-02-all-30fps.png)
- [1타 전체60fps 보간 — 37장](attack-01-all-60fps.png)
- [2타 전체60fps 보간 — 45장](attack-02-all-60fps.png)

각 공격의 `30fps/`·`60fps/` 아래 `native/`는128×128, `6x/`는768×768 투명PNG다. 총248개이며 전체 비교판은3배 표시다. 각 배율은 같은 수식을 해당 해상도에서 최근접 샘플링한다. 마지막 표본은 종료 경계이며 추가 유지 프레임이 아니다.

## Player와 앞뒤 표시

현재 `art/archive/player/final/unity/idle-left-back.png`의 첫20×20셀을 사용했다. Unity 출력과 바이트가 같고 native 첫 프레임을(4,0)에 복원한 픽셀과 일치한다. 16PPU·발밑 중앙 피벗(좌상단 기준10,20)을 Player 원점에 놓고 **몸체 자세는 고정**했다. 위쪽을 조준한 검 모션을 보기 위한 현재 뒷방향 이미지이며 새로운 몸체 공격 자세를 그린 결과는 아니다.

원작 `SetSortingOrder_MainWeapon` 이벤트대로1타는F0~F10에서 검이 몸 뒤, F11부터 몸 앞이다. 2타는F0~F8에서 몸 앞, F9부터 몸 뒤다. 검기는 몸 앞에 표시했다. FX와 전경 검의 로컬 sortingOrder는 모두1이지만 실제로는 Player의 SortingGroup 계층도 영향을 주므로 전체 정렬의 동률이라고 단정하지 않는다. 합성에서는 검을 마지막에 그려 확인하기 쉽게 했으며 원작 실행의 정확한 FX 가림 순서로 취급하지 않는다.

## 좌표식의 출처

원작에 보고서의 좌표 계산식을 직접 작성한 것은 아니다. 원작은 `WeaponSimple.Initialize`에서 어깨 오프셋/검기 지점을 설정하고, `WeaponControllerSimple.Aim`에서 Shoulder를 회전시키며, Animator가 `Shoulder/Weapon`의 위치·회전 키를 보간한다. 보고서의 식은 **그 Transform 계층과 저장된 키가 합쳐지는 결과를 분석자가 수식으로 풀어 쓴 것**이다. 거리·각도·시점은 실제 원본 데이터를 사용했다.

원작의 프레임별 키 사이 보간도 Unity Animator가 수행한다. 분석 스크립트의 Hermite 계산은 원작 C#에서 발견한 별도 공격 공식이 아니라 직렬화된 접선값으로 같은 곡선을 평가한 것이다. 자세한 근거는 [원작 분석3.2·4절](../../../../docs/archive/03_Analysis/Sephiria_GreatSword_Attacks_20261115.md)을 따른다.

## 확인 범위

`build_frames.py`와 `manifest.json`에 입력 해시·124표본·원작 앞뒤 이벤트·출력 경로를 저장했다. 이전판의 모든 위치/회전/시간/검기 픽셀 해시 일치, Player final/Unity/native 픽셀 일치·16PPU/피벗,248PNG·4비교판과 잘림을 확인했다. 원작 검기 시간은 명목20Hz이며 실제 Update 양자화 차이는 원작 분석과 같다. 원작 실행 캡처·Project_Chrono 공격 코드/씬 적용은 수행하지 않았다.
