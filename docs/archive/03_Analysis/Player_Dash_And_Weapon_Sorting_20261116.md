# Player 대시와 무기 정렬 — 원작 값·이식 차이

2026-11-16. 기능 F-P02·F-P03, 담당 PlayerCharacter·AttackController. 실제 읽은 코드/프리팹의 정적 근거를 정리한다. 구현·실행 결과의 원본은 [GAME_SPEC2.1.2](../../GAME_SPEC.md#212-f-p02-대시-구현확인과-sequence-diagram)와2.1.3이다. 원작 게임을 실행해 측정한 결과가 아니다.

## 1. 대시는 클래스 초기값보다 실제 Player 프리팹을 기준으로 적용

- [PlayerAvatar.prefab](<../../reference/sephiria/assets/ExportedProject/Assets/GameObject/PlayerAvatar.prefab>)1034는 FastMove 컴포넌트 fileID114208575754630950을 CurrentDashModule로 연결한다.2155의 Script GUID와 CharacterDash_FastMove.cs.meta가 일치한다.
- 같은 프리팹2163·2175·2178은 회피0.10초·이동0.14초·시작속도24다. 클래스의20·0.33·0.16 초기값을 실제 Player 값으로 사용하지 않는다.
- [CharacterDash_FastMove.cs](<../../reference/sephiria/code/ingame code/Assembly-CSharp/CharacterDash_FastMove.cs>)21~31은 매 Update 속도를87×deltaTime만큼 먼저 줄이고 변위를 계산한 뒤 이동 타이머를 확인한다.36~49는 방향을 이동 입력 우선/없으면 조준으로 고정하며 초기속도는24×(1+DashSpeedBonusPercent/100)다. 일반 이동 SP 배율은 곱하지 않는다.
- [UnitAvatar.cs](<../../reference/sephiria/code/ingame code/Assembly-CSharp/UnitAvatar.cs>)1177~1183은 대시 변위로 일반 변위를 덮는다. 이동 후 별도 회복 잠금은 없다. 공격 전진을 포함한 기존 변위 전체를 대시가 대체하므로, 우리 구현도 대시 시간 중 공격 전진의 시간만 진행하고 그 변위를 버린다. 대시 종료 경계 뒤 남은 프레임 시간에는 남은 공격 전진을 다시 합산한다. AS2 공격 종료 직후에도 대시 구간 자체에 공격 전진이 더해지지 않게 하는 경계다. 관측 프레임이 대시 종료 뒤 시간까지 포함하면 그 구간의 정상 공격 전진은 별도로 합산한다.3233~3245와 [CharacterDash.cs](<../../reference/sephiria/code/ingame code/Assembly-CSharp/CharacterDash.cs>)93~109가 CanMove/CanDash·중복·사용횟수·DASHDISABLE을 검사한다.
- CharacterDash.cs187~200·77~80은 시작부터 회피0.10+DashInvincibleTimeBonus/1000초를 독립 관리한다. StopDash147~170은 이동 종료이며 회피 시간을 같은 값으로 강제하지 않는다.

추가 스탯0일 때 연속 곡선 v(t)=max(0,24−87t)를0~0.14초 적분하면2.5074다. 원작은 선감속/Euler 이동·마지막 프레임 초과가 있어 고정60FPS 계산 예시는2.5125다. 우리 구현은 같은 수치/곡선을 구간 정적분해 FPS별 거리 차이를 만들지 않는다. 이 계산값을 원작 실제 실행 측정값이라고 부르지 않는다.

현재 변경은 DashSpeed24·DashDeceleration87·DashDuration0.14·DodgeDuration0.10·InputBlockDuration0.14다. 이동 후 추가 잠금0이며 기존 DashCooldown0.65를 유지한다. 원작의 충전 횟수/회복·구덩이 무시·네트워크는 도입하지 않는다. 원작 WeaponControllerSimple527~539의 StopDash(true) 공격 전환도 우리 Q10의 공격↔대시 전환 금지에 맞춰 제외한다.

## 2. 검이 타일 아래로 내려간 원인과 정렬 기준

원작 PlayerAvatar.prefab1638의 BodyWrapper에는 SortingGroup이 있다. WeaponControlAnimator.SetSortingOrder_MainWeapon56→WeaponControllerSimple→WeaponSimple228~233은 무기 SortingGroup의 내부 앞뒤 값을 설정한다. 이±1을 우리 SpriteRenderer의 전역−1/1로 옮긴 것이 이식 오류였다.

우리 Player는 몸체20·검21·검기21과 SortingLayer0을 사용하고 SortingGroup이 없다. AttackController가 검/검기를 전역−1/1로 덮으면 같은 레이어의 맵 타일1~10 아래에 정렬될 수 있다. 이는 정적 순서 대조의 근거이며 특정 캡처에서 타일이 검 전체를 가렸다는 실행 증거와 구분한다. 앞선 공격 검사는 Player를 빈 좌표(40,40)로 옮겨 촬영했으므로 포즈/검기 일치가 실제 타일 위 정렬까지 확인한 결과는 아니었다.

수정은 Player.Awake의 최초 준비에서 CharacterRendering.BodySprite 레이어·순서를 한 번 확보하고, 검은 몸체순서±1·검기는몸체순서+1로 지정한다. 기본 몸체20에서 검19/21·검기21이 되어 타일 위 표시와 몸체 앞뒤 전환을 함께 유지한다. 풀의 추가 검기에도 같은 캐시를 사용한다. 후속 사용자 요청으로 공격하지 않는 Idle·이동·콤보Wait의 검은 모두 몸체 뒤19에 둔다. 실제 공격 중의 클립 앞뒤 전환과Wait의 위치/각도 곡선은 유지한다. 타일 순서 변경이나 새 SortingGroup 추가는 하지 않는다. Player 수명 중 고정된 표현 설정이므로 매 Update에 컴포넌트/Instance를 다시 조회하지 않는다.

## 3. 실제 실행과 한계

**이번 후속 변경·확인(2026-11-16):** 무입력 공격 전진을 줄이려는 사용자 요청에 `StationaryAttackDashMultiplier=0.5`를 현재 튜닝값으로 적용해 명목0.3,이동 입력이 있던 타는 명목0.6을 사용한다. 방향/무입력 여부는 예약 클릭이 아니라 실제 각 타 시작에 고정하고 타격 시 적용한다. Idle·이동·Wait2/Wait3의 검은 몸체 뒤19이며 실제 공격 중의 기존19/21 전환과 Wait의 위치/각도 곡선은 유지한다. 원작의 타격 순간 입력 조회·무입력 동일 거리에서 사용자 맞춤 동작으로 변경한 것이며0.5를 원작 수치나 사용자 확정 밸런스값으로 기록하지 않는다.

**현재 후속 검증:** `project/Logs/PlayerAttackSnapshot.log`·`PlayerAttackSnapshot-result.json`에서 컴파일 오류0·18묶음 성공·success=true·error 빈값·배치 종료0을 확인했다.1·2·3타 모두 무입력 시작 후 준비 중 키/커서가 바뀌어도 시작 커서 방향으로0.3,이동 입력으로 시작한 뒤 준비 중 해제해도 원래 방향으로0.6을 진행했다. AS2에서 다음 타가 시작돼도 이전 활성 전진 방향/속도는 다음 타격 전까지 유지하고 그때 해당 타의 snapshot으로 전환했다. 기존544개 검 모션·54개3타 FX·입력 무시/예약·콤보·독립 판정·3타 단일 피해·재활성/Title 정리도 재확인했다. 대시8묶음 재검증과 뒤 검19/실제 공격19·21/검기21·최신 바닥 캡처,관련23파일/45자산 일치·동시7파일 제외·검색 초기화 예외는2.1.2를 따른다.

**현재 대시 재검증(2026-11-16):** Unity6000.3.21f1의 `project/Logs/PlayerAttackSnapshotDash.log`와 `PlayerAttackSnapshotDash-result.json`에서 컴파일 종료·8묶음 성공·error 빈값·배치 종료0을 확인했다. Space 일회 요청·방향 고정·WASD 복원/기존 쿨타임0.65 보존,0.10초 회피/0.14초 이동,정지/차단·얇은 벽 충돌을 재확인했다. Time.captureDeltaTime이1/30·1/60·1/144초인 순수 대시 거리는 모두2.5074005127이다. 실제 렌더 FPS나 성능 측정값이 아니다. AS2 공격 직후2.50787735는 대시 종료 경계 뒤 프레임 시간의 정상 공격 전진을 포함한다. 대시 구간 자체에는 공격 전진을 더하지 않는다.

**검/검기 최신 정렬 확인:** Idle·이동·Wait의 검은 몸체20 뒤19,실제 공격은 기존19/21 전환,검기는21임을 확인했다. `project/Logs/AttackSnapshot`의 `sword-on-laboratory-floor.png`에서 수정된 뒤 검은2829픽셀,같은 구도의 `sword-old-order-on-same-floor.png`에서 이전 절대−1은0픽셀이었다. 이번 비교는 실제 바닥 위 완전 가림을 재현했다. `attack-on-laboratory-floor.png`의 검기는69610픽셀이다. 캡처 구역은 outside0/corridor-floor1이고 전체 맵 정적 순서0~10과 구분한다. 지표는 `PlayerAttackSnapshotDash-metrics.json`이다.

**최신 실행 범위·한계:** `project/Logs/PlayerAttackSnapshot.log`의 공격18묶음도 success=true·error 빈값·배치0이다. `PlayerAttackSnapshot-verification.json`은 관련23파일의 원본/격리 일치·최종 baseline 변경0과 공격45자산 일치를 기록하며 baseline은 `PlayerAttackSnapshot-source.json`이다. AreaWarningVfx·CircleWarningVfx·SkillController·SlashPresentationFx·DrawEvaporationVfx·DrawVfx·GameCamera7개 동시 런타임 변경은 미동기화 상태여서 전체 최신 원본 검증으로 확대하지 않는다. C# 컴파일 오류는0이며 기존 UnityEditor.Search.IndexationOnStartup의 ArgumentOutOfRangeException은 관찰된 미해결 검색 초기화 예외다. 기능 검사 성공·배치0과 구분한다. 원본 Editor 수동 플레이·전체 StartupFlow·제출 빌드·성능 측정은 이번 범위가 아니다.

**앞선 캡처 이력:** `project/Logs/DashIntegration`의 이전 절대+1 비교는 검4955→2847픽셀로 몸체에 겹친 손잡이/하단의 부분 가림이었다. 당시 자료를 바닥 전체 가림 재현으로 바꾸어 설명하지 않는다. 위 최신 AttackSnapshot의 절대−1 비교가 별도의 완전 가림 근거다. 이전 대시28/29/19와공격15묶음은 각 당시 확인 이력이다.
