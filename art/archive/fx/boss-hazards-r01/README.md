# 보스 공통 탄환·원형 경고 R01

2026-11-16 사용자 요청: 탄환은 픽셀 이미지, 원형 경고 장판은 반투명 빨간 선으로 표현한다. 첨부 레이저는 표현 분석용이며 이번 제작 대상은 탄환과 원형 경고다.

- `normal-bullet.aseprite`: 8×8px·1프레임·3레이어 편집 원본. 짙은 빨강 윤곽, 빨강 몸체, 흰 중심이며 실제 불투명 영역은 가운데 6×6px다. 색·크기는 제작 초기안이다.
- `normal-bullet.png`: 원본과 같은 합성 PNG. Unity 출력은 `project/Assets/Art/Enemy/Projectiles/normal-bullet.png`, 16PPU·Point·압축 없음·mipmap 없음으로 연결한다.
- `bullet-pixel-preview.png`: 밝은/어두운 바탕에서 16배 최근접 확대한 비교 출력이다.
- `bullet-validation.json`: 이진 alpha·대칭·3레이어 원본 재열기·PNG/프로젝트 출력 일치를 확인한 결과다.
- `create-bullet.lua`: 최초 도형 제작 근거다. 이후 사용자 편집 원본을 덮어쓰는 재생성 용도로 사용하지 않는다.
- `export.lua`: `input`의 현재 Aseprite를 열어 `output` PNG로 출력한다. 이후 원본 편집은 이 내보내기 경로를 사용한다.

원형 경고는 래스터 이미지 없이 `CircleWarningVfx`와 `LineRenderer`로 실제 Collider 반경을 표시한다. 내부 채움은 없으며96개 점의 닫힌 선이다. 예고 RGBA(1,0.12,0.16,0.45), 활성 alpha0.8·선폭0.04·반경1·예고0.8초는 조정 가능한 초기값이다. 피해·수명은 기존 `AreaProjectile`이 관리한다.

실사용 프리팹은 `project/Assets/Prefabs/Enemy/NormalBullet.prefab`과 `WarningArea.prefab`이다. 기존 Dummy의 Normal·Area 프리팹 참조/GUID도 보존해 같은 표시를 연결한다. `Tools/Chrono/Prepare Boss Hazard Art`로 연결하고 `Tools/Chrono/Check Boss Hazard Art`로 저장을 격리해 검사할 수 있다.

`preview-white.png`와 `preview-laboratory.png`는 Unity 카메라로 직접 렌더한1024×768 화면이다. 왼쪽은 반경1의 예고색, 오른쪽은 반경2의 활성색 비교이며 두 원의 선폭은 같다. 탄환 원본1px은 화면6px다. 실험실 비교는 현재 전투방 바닥 타일을 사용한다.

동일 Unity6000.3.21f1의 격리 사본에서2026-11-16 08:29:46 KST 기준 컴파일·어셈블리 재로드·11묶음 Play Mode 검사·배치 종료0을 확인했다. 원형 경고 중 무피해·활성 후1회 피해·재진입 중복 없음·정지/수명·Slash·그로기 유지·Box Laser 회귀·Title 복귀를 확인했다. 실사용/기존 시험 프리팹과 코드 등11개 반영 대상의 원본/검증 사본 해시가 같다. `unity-validation.json`·`integration-validation.json`을 따른다.

기존 시험 Area의 예고는0.5초를 유지하며 실사용 WarningArea의0.8초와 구분한다. 첫 검사의 Color float 완전일치 오탐은 실제 LineRenderer 색 정밀도에 맞춘 Color32 비교로 수정했다. Editor 시작의 SearchDatabase 인덱스 예외는 관찰됐지만 이후 컴파일·실행 검사는 통과했다. 원본 Editor의 직접 Play/import와 제출 빌드는 확인하지 않았다. 경비5패턴 구현 완료와 구분하며 전체 결과·남은 범위는 GAME_SPEC B05.1·2.2.6을 따른다.
