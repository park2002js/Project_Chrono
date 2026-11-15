# 던그리드 HUD 코드·에셋·위치 비율 분석

확인일: 2026-11-16 (Asia/Seoul). 사용자 첨부의 글자 가이드에 맞춰 기존 맵1920×1080 프리뷰에 배치하기 위한 정적 분석이다. 원작 실행이나 우리 Unity 구현 검증을 의미하지 않는다.

아래 제작 위치·크기는 당시 R02 분석 이력이다. 이후 사용자가 직접 수정한 원본과 최종 출력은 [UI 최종 전달](../../../art/archive/ui/final/README.md)을 따른다. R01~R03 중간 아트 자료는2026-11-16 사용자의 결과만 남기기 요청으로 삭제했으며, 이 문서는 원작 코드 근거를 보존한다.

## 1. 분석 대상과 결과

로컬 `docs/reference/dungreed/README.md`가 명시하는 Steam BuildID12998126·2026-11-06 추출본을 사용했다. REA `inspect_managed_artifact`가 Assembly-CSharp.dll의 Unity Mono/CIL·완전한 메타데이터를 확인했다. SHA256은 `8d99feb6c9e7dcb8858a22aa49d64e5c600590028ac5ebbda57ee2a0c3134933`, MVID는 `fadc8a5f-9baa-47c7-aab3-aa34cb28ac2c`이다. 당시 Evidence ID는 `ev_6cd32cd9ef546257615e1903e662fa27576cfb0a4509e988342daedab4850fdc`이며 중간 아트 폴더의 별도 요약 파일은 최종 정리 대상이다. 메타데이터 확인과 아래 추출 C# 본문 분석은 구분한다.

첨부 HP 아래 청록 막대는 5개의 충전 구획과 폭 비율이 원작 **DashBar**와 일치한다. 별도의 ShieldBar52×9도 있지만, 첨부는 좌끝2+반복9×5+우끝2=49×8 구조다. 이를 우리 그로기 게이지 외형으로 사용하며 원작에 그로기 기능이 있다고 주장하지 않는다.

## 2. 실제 코드·연결

코드 루트는 `docs/reference/dungreed/code/ingame code/Assembly-CSharp/`, 씬은 `docs/reference/dungreed/project/ExportedProject/Assets/Scenes/MainWorld.unity`이다.

| 흐름 | 실제 근거 | 적용/차이 |
| --- | --- | --- |
| Player 연결 | UIManager.cs738~781, SetPlayer→playerHPBar.Initialize→dashBar.ReBuild, Shield 활성 조건 뒤 hpGroup 레이아웃 갱신 | Player HP·Dash 외형과 조립 규칙 참고. 원작 무기 교체/레벨/Shield 시스템은 추가하지 않음 |
| HP 채움 | UI_CreatureHPBar.cs37·53·61·84, Start에서 최초 폭 보관→ceil(HP)/ceil(maxHP)에 비례한 sizeDelta.x | Player/Boss 공통. 원작 숫자는 valueText가 있을 때만 표시. 사용자 선택에 따라 숫자·정보 칸 제거 |
| Boss 시작/종료 | UI_BossWarningPanel.cs199→UIManager.SetBoss(cs785), BossBasic.cs289~296→SetBoss(null) | 원작 UI 활성·타깃 연결과 종료 경로 확인. 우리 기존 Boss HUD 독립 표시 정책 유지 |
| 대시 칸 조립 | UI_ControllerDashBar.cs10·36~58, 왼쪽2/중간9×N/오른쪽2, fill=(2+9k,-2), width=4+9N |5칸49×8의 픽셀 외형을 그로기로 재사용. 값은 우리0~100이며 대시 횟수를 복제하지 않음 |
| 대시 칸 상태 | 같은 파일69~94, 가능 칸 fill1, 충전 중 timer/interval·alpha.5, 뒤 칸 숨김 | 원작 동작 근거. 프리뷰는 그로기60%·3칸 표시로 동작 구현과 구분 |
| 스킬 값/표시 | Player.cs1389~1413→UI_SkillCooldown.Activate, UI_SkillCooldown.cs9~34→GetRemainCoolDownRate | 원작은 현재 무기의 Q/E. 우리 고정 Draw/Slash와 이름/역할 구분 |
| 스킬 쿨타임 수명 | ActiveSkill.cs69~72·117~120·173~183, timer 초기화·누적·사용 종료 | 프리뷰는 고정 예시. 원작 Image Radial360과 우리 아래→위 수평선은 다름 |

씬 UIManager는180216행 playerHPBar10083,180220행 dashBar10212,180225행 bossHPBar9020에 연결된다. Player HP8382의 LifeBar tint는 흰색이며 원본10픽셀은 모두 `#EA4747`; Boss도 같은 색을 WhiteSprite에 tint한다. 첨부 화면은 더 밝은 약`#F95747`이지만 캡처/버전/런타임 차이의 원인은 미확인이다. 이번 제작은 실제 에셋 색을 사용했다.

## 3. Sprite와 픽셀

Atlas: `Assets/Texture2D/sactx-0-2048x2048-Uncompressed-Atlas_UI-fb7fd2eb.png`,2048×2048,Point 필터. GUID `b6c4b817c03b8d84e84c97fa8896b149`, SHA256 `d5593c40aaeef9e98fc4763fa9cb6a87a95d4c77db39a5346ef16d09e1be22aa`.

Sprite는 `Assets/Sprite/이름.asset`. 아래는 PNG 좌상단 기준이며 Unity y를 `2048-y-height`로 변환했다. `m_Rect`와`m_RD.textureRect`도 대조했다.

| Sprite | PNG x,y,w,h | 용도 |
| --- | --- | --- |
| PlayerLifeBack |1053,372,74,16| Player 배경 |
| PlayerLifeBase 1 |1730,1340,74,16| 실제 Player 앞 프레임 |
| LifeBar |945,103,1,10| HP 채움 |
| BossLifeBack |1050,302,125,16| Boss 배경 |
| BossLifeBase |679,271,125,16| Boss 앞 프레임 |
| DashBaseLeftEnd |1597,1550,2,8| 그로기 외형 왼끝 |
| DashBase |849,1151,9,8| 반복5칸 |
| DashBaseRightEnd |1344,668,2,8| 오른끝 |
| DashCount |1044,1256,9,4| 청록 채움 |
| IconGray |1639,1013,19,19| 스킬 프레임2개 |

원작 HP의 내부 값 영역은 Player(22,3,49,10), Boss(22,3,100,10)이다. 사용자 답변으로 왼쪽 정보 칸을 없애므로, 프레임 양끝4px와 상하 반복 레일을 원본 그대로 조립하여 전체 크기는 유지하고 내부 값을(3,3,width−6,10)으로 넓혔다. 원작 보스 초상·레벨·HP 숫자는 넣지 않았다. 원본 크롭과 수정 프레임은 별도 보관한다.

오른쪽 큰 흰 프레임 `EquippedWeaponBase`34×24는 무기 교체 표시다. 실제 스킬 슬롯 `IconGray`19×19와 구분하여 이번 두 스킬에 후자를 사용했다.

## 4. 위치 비율과1920×1080 적용

MainWorld.unity180172행 CanvasScaler9888은 ScaleWithScreenSize·reference320×180·matchWidth0·referencePPU100이다. 따라서1920×1080에서6배가 된다. HPGroup98137/189403행은 좌상 anchor/pivot(0,1),padding3,spacing1이다. Shield가 비활성인 첨부 구도는 HP 바로 아래 Dash가 배치된다.

| 요소 | 원작 논리 배치 | 원작1080p 환산 | 이번 적용 |
| --- | --- | --- | --- |
| Player HP |(3,3),74×16| (18,18),444×96 | 동일. 화면 폭23.125%, 좌측.9375%·상단1.667% |
| Dash5→그로기 |(3,20),49×8| (18,120),294×48 | 동일. HP와6px 간격, 화면 폭15.3125% |
| Boss HP |하단중앙·아래3,125×16| (585,966),750×96 | 동일. 화면 폭39.0625%, 아래1.667% |
| 원작 Q |(238.5,155),19×19| (1431,930),114×114 | Draw를(1662,930)으로 이동 |
| 원작 E |(259.5,155),19×19| (1557,930),114×114 | Slash를(1788,930)으로 이동 |

Player Rect6352=96562행, Boss Rect5834=86197행, Q Rect5968=88863행, E 부모5551=80551행·자식6473=99051행. E는 상위 레이아웃 활성 항목에 따라 위치가 달라질 수 있어 표는 저장된 정적 배치 환산이다. 두 스킬은 원작 크기·12px 간격을 유지하고, 별도 무기 표시 없이 첨부의 우하단 가이드 영역 끝으로 옮겼다. 오른쪽 여백18px, 프레임 하단 여백36px이다.

## 5. 제작·확인과 남은 범위

당시 R02 PNG·재현 소스는 이후 최종 결과 정리 대상이다. 기존 맵의 중앙320×180 영역·Player 원본을 사용하며 body 중심은(960,540)이었다. 원본Atlas 해시·10크롭 치수·7출력Sprite 치수·1920×1080을 확인했고 양쪽 HP 프레임 내부가 투명하여 구분선이 없는지 자동 검사했다. 독립 시각 검토에서5요소·정보 칸 제거·겹침/잘림 없음이 확인됐다. 현재 출력은 상단의 최종 전달 링크를 따른다.

Player75%·Boss65%·그로기60%·스킬 쿨타임은 정적 예시다. 스킬 내부 Draw/Slash 표식은 R01 재사용이다. 실제 코드/씬/수치 연결, 모션, Unity 컴파일·플레이 검증은 이번 작업에서 하지 않았다. 원작의 서버·무기 교체·레벨·대시 횟수나 Radial 쿨타임을 우리 게임에 도입하지 않았다.
