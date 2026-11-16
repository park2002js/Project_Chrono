# 보스 탄환 확대·반투명 채움 R02

2026-11-16 사용자 수정: 탄환 주위를 각 방향2px 확장하고 Player와 같은 배율로 비교한다. 장판 내부는 반투명 빨강으로 채우며 테두리는 더 진하게 구분할 수 있다. 이전 R01은 제작·검사 이력으로 보존한다.

- `normal-bullet.aseprite`·`.png`:12×12px 셀·실제 불투명10×10px·1프레임·4레이어. 이전8×8px 셀/불투명6×6px에서 각 방향2px 확장했다. 기존 빨강·흰 중심은 유지하고 빨간 몸체를 넓혀 바깥 윤곽을 이동했다.
- `expand-bullet.lua`: R01 편집 원본을 읽어 이번 확장을 수행한 제작 근거다. 이후 사용자 편집본의 덮어쓰기 재생성 용도로 사용하지 않는다. PNG 출력은 R01의 `export.lua`에 R02 원본/출력 경로를 넘겨 현재 합성을 내보낸다.
- `bullet-validation.json`: 확대 치수·중심/빨강 픽셀 보존·대칭·이진 alpha·4레이어 원본 재열기/PNG/프로젝트 일치 결과다.
- `preview-white.png`·`preview-laboratory.png`: 실제 Unity 카메라1024×768 캡처. 위쪽 Player와 탄환은 모두16PPU·원본1px당 화면6px다. 발밑 기준선을 맞췄으며 탄환의 불투명 높이는 Player20px의 절반인10px다. 아래 왼쪽 원은 반경1의 예고색, 오른쪽은 반경2의 활성색이다.

원형 내부는 공유 단위 원판 `CircleFill.asset`으로 채우며 기존96점 외곽선과 같은 반경을 사용한다. 예고는 내부alpha0.2025/테두리0.45, 활성은 내부0.36/테두리0.8이며 조정 가능한 초기값이다. 내부 뒤로 바닥이 비치고 테두리는 더 진하게 표시한다. 피해·전조·수명은 기존 AreaProjectile이 담당한다. 탄환 판정 반경/CircleCast 반경은 확대 크기에 맞춰0.24로 조정했다.

실사용은 `project/Assets/Prefabs/Enemy/NormalBullet.prefab`·`WarningArea.prefab`이다. 기존 시험 Normal·Area의 참조/GUID를 유지한다. PNG도 기존 GUID를 유지하며 같은 경로에 반영했다. `Tools/Chrono/Prepare Boss Hazard Art`와 `Tools/Chrono/Check Boss Hazard Art`로 준비·검사할 수 있다.

2026-11-16 08:46:26 KST에 같은 Unity6000.3.21f1의 격리 사본에서 컴파일/어셈블리 재로드·11묶음 Play Mode 성공·배치 종료0을 확인했다. 실제 채움·더 진한 테두리·같은 배율 비교·전조 무피해·활성1회 피해·정지/수명·Slash·그로기 유지·기존 Box Laser와 Title 정리를 확인했다. 소유13대상은 검증 사본과 해시가 같다. `unity-validation.json`·`integration-validation.json`을 따른다.

동시 작업의 Editor1개·런타임3개가 검증 뒤 달라져 현재 프로젝트 전체의 최종 검증으로 확대하지 않는다. 시작 로그의 Editor SearchDatabase 인덱스 예외는 관찰했으며 컴파일/기능 검사는 통과했다. 원본 Editor 직접 Play/import·제출 빌드는 미실행이다. 실제 경비5패턴·레이저 제작 완료와도 구분한다. 현재 전체 사양·기록은 GAME_SPEC B05.1·2.2.6을 따른다.
