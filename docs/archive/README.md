# `/docs/archive` 디렉토리 활용 방법
## 행동 방침
이 문서는 `/docs/archive` 하위에 존재하는 디렉토리 및 문서가 어떤 과제의 결과로 작성된 것인지 그 경위와 목적을 기록하는 문서이다.

아래의 '디렉토리 구분 표'와 각 디렉토리별 설명 문단들을 보고 자신이 어느 디렉토리에 결과를 작성해야 하는지, 디렉토리를 추가해야 하는지 판단한다.

만약 자신이 추가하려는 내용에 해당되는 디렉토리가 이미 존재하면 그 디렉토리를 사용한다.

### 디렉토리 구분
아래의 표는 현 README.md가 위치한 디렉토리인 `/docs/archive` 를 기준으로 위치가 서술되어 있다.
아래에 표에 명시한 디렉토리로는 구분할 수 없는 사항일 경우 표에 새로히 디렉토리 및 담당 내용을 추가하고, 그와 관련하여 이 문서에 문단을 추가한다.
| 위치 | 담당 내용 |
| --- | --- |
| `./01_Choice` | 코드 작성 및 구조, 그 외 선택 사항에 대해 왜 그런 결정을 하였는가를 기록 |
| `./02_Hold` | 보류 상태에 놓인 비교·검증·(임시처리 된)결정해야 하는 사안을 기록 |
| `./03_Analysis` | 요청된 원작·에셋 분석의 근거, 수치와 제작 참고 자료를 기록 |

### 이 문서의 작성 요령
이 디렉토리의 바로 하위에 존재하는 디렉토리에 대한 설명은 "##"로 구분하여 작성하며, 그 하위 디렉토리에 저장된 각 파일에 대한 설명은 목록화인 `-`를 사용해서 구분해 작성한다.

아래는 작성 예시이다.
```markdown
## 1. 예시 디렉토리
*이 부분에는 예시 디렉토리가 어떤 목적으로 작성된 것인지 기록한다*.
- '예시.md' : 예시.md에 대한 설명을 작성한다. 
- .... 
```
---

## 1. 01_Choice
- [Temp_Demo_Ending_20261116.md](01_Choice/Temp_Demo_Ending_20261116.md) : 경비 OnDie 구독·암전·중앙 데모 종료 안내·Title 복귀 임시 요청, 기존 스탭롤 대체와 초기 타이밍 구분
- [Controls_Guide_20261116.md](01_Choice/Controls_Guide_20261116.md) : 검은 배경용 자판/마우스 안내·일반/특수 슬래시 구분과 F 상호작용 설명 제외 요청, 이미지 제작/게임 코드 변경 구분
- [Player_Entrance_Cutscene_20261116.md](01_Choice/Player_Entrance_Cutscene_20261116.md) : 남쪽 입구 복도 약3배·실제 Player의 컷씬 보행/조작 인계와 새 게임 최초1회·계속/부활 체크포인트 유지 선택
- [Player_Fever_Aura_20261116.md](01_Choice/Player_Fever_Aura_20261116.md) : 각성 중 몸에서 피어오르는 황금 오라 요청과 상태 사건·재사용 입자 연결, 초기 제작값/미술 승인 구분
- [Player_Feedback_Audio_20261116.md](01_Choice/Player_Feedback_Audio_20261116.md) : Bank 추출·Unity 내장 오디오 선택과 콤보1=A/2=B/3=A→B, 실제 성공 적중 섬광 요청 및 초기 튜닝 구분
- [SaveSlot_Frame_20261116.md](01_Choice/SaveSlot_Frame_20261116.md) : 검정·파랑/어두운 보라의 SaveSlot 단일 픽셀 프레임과 여러 해상도·설정창 재사용 요청
- [Boss_Guard_Pattern_20261116.md](01_Choice/Boss_Guard_Pattern_20261116.md) : 출구를 가리는 경비 배치·Gold 단발/한 손 충전 낙하의 첫 두 패턴 요청과 단독R01/2개 등록·재도전 경계
- [Boss_Hazard_Visuals_20261116.md](01_Choice/Boss_Hazard_Visuals_20261116.md) : 픽셀 일반탄·반투명 빨강 원형 선 경고 요청과 구체 제작 초기값·레이저 분석/미구현 범위 구분
- [Player_Slash_Presentation_20261116.md](01_Choice/Player_Slash_Presentation_20261116.md) : Slash의 빠른/느린 줌·발도 유지·칼날 섬광·납도음·지연 공간참 순서와 조정 가능한 초기값, 전투 판정 시점의 답변 상태
- [Boss_Front_Concept_Brief_20261116.md](01_Choice/Boss_Front_Concept_Brief_20261116.md) : 보스 8방향 목표와 스케치·Notion 레퍼런스를 이용한 6종 탑다운 정면 Aseprite 제작 요청
- [Art_First_Integration_20261116.md](01_Choice/Art_First_Integration_20261116.md) : 완성 Title·맵/수정/문·Player/검·HUD/키보드/조준점을 게임에1차 반영하는 요청과 애니메이션·이동 조건
- [Boss_Common_Combat_20261116.md](01_Choice/Boss_Common_Combat_20261116.md) : F-B01~F-B06 공통 기반·실제 더미/임시 패턴 통합 검증 요청과 최종 콘텐츠·초기 튜닝 구분
- [HUD_Reference_Brief_20261115.md](01_Choice/HUD_Reference_Brief_20261115.md) : Notion Player HP·첨부 Boss HP 선택과 중앙1920×1080 프리뷰 요청, 제작안/확정 구분.
- [Player_Lifecycle_Checkpoint_Buffs_20261115.md](01_Choice/Player_Lifecycle_Checkpoint_Buffs_20261115.md) : ConsumeLine 명명과 F-P09~F-P11 일괄 구현 요청·기존 확정 사양 적용·초기 튜닝 구분.
- [Player_Hit_Protection_Events_20261115.md](01_Choice/Player_Hit_Protection_Events_20261115.md) : F-P08 상태 발생자의 사건 알림·구독자 책임과 피격 시 빨강/원본색 표시 요청.
- [Sword_Size_First_20261115.md](01_Choice/Sword_Size_First_20261115.md) : 상단을 바라보는 Player의 왼쪽 허리 착용 검을 크기부터 비교하고 디테일·FX를 이후 정하는 요청.
- [Player_Draw_Responsibilities_20261115.md](01_Choice/Player_Draw_Responsibilities_20261115.md) : F-P04의 실제 전용 입력 전환·역할 분담과 이후 교체하기 쉬운 임시 점·Line VFX 요청.
- [Camera_Follow_Response_20261116.md](01_Choice/Camera_Follow_Response_20261116.md) : 일반·컷씬 타깃의90% 접근 약0.25초와 즉시 이동·Draw 예외를 유지하는 요청
- [Art_Archive_Git_20261115.md](01_Choice/Art_Archive_Git_20261115.md) : 완성 아트 보관과 작업 자료 Git 제외 요청 및 이유.
- [Player_Attack_Sephiria_20261115.md](01_Choice/Player_Attack_Sephiria_20261115.md) : F-P03의 입력·허용·공격 책임, 원작/우리 검 프레임 검토·FPS·검기 편집 요청과 선택8의 시간 부족·리스크 축소를 위한 Player 애니메이션 구조 이관 이유. 분석·실제 적용·별도 아트 범위를 구분한다.
- [Player_Dash_Sephiria_20261115.md](01_Choice/Player_Dash_Sephiria_20261115.md) : F-P02의 이동 우선·정지 커서 방향, 세피리아 대시 흐름 적용과 현재 Character 요청·Body 적용 구조 유지 요청.
- [Shared_Instances_Reference_Workflow_20261115.md](01_Choice/Shared_Instances_Reference_Workflow_20261115.md) : Instance·고정 참조 재사용과 원작 분석 절차, Player Awake 자동 초기화·Map 조회·Body의 누적 이동 적용을 요청한 이유.
- [InputActions_Sephiria_20261115.md](01_Choice/InputActions_Sephiria_20261115.md) : F-P01의 Input Actions 콜백·캐시·전달 판단 책임과 세피리아 입력 차단의 주체·진입·유지·해제 처리 적용 요청.
- [Title_Save_UI_Responsibilities_20261115.md](01_Choice/Title_Save_UI_Responsibilities_20261115.md) : 부팅·Title 자체 준비의 책임 분리, Current 참조·저장 요청 계약과 UI_ 네이밍 규칙의 요청·이유 및 조건부 성능 비교.
- [Public_Variable_Comments_20261115.md](01_Choice/Public_Variable_Comments_20261115.md) : public·private 변수, 목적별 구역·구독·코루틴의 설명과 짧은 주석 어투 및 작성·검토 절차에 대한 사용자 요청.
- [Laboratory_Arena_Preview_20261115.md](01_Choice/Laboratory_Arena_Preview_20261115.md) : 연구소6배 표시·진행 방향·전체/부분 시야, ETG 분석·균일16px 셀400×288 선택, 크랙 바닥·입구 외부 이동·경계 벽 보강 및 대리석 연결 복도 요청의 경위.
- [No_Namespace_20261115.md](01_Choice/No_Namespace_20261115.md) : 프로젝트 코드의 namespace 제거와 이후 개발에서 namespace를 선언하지 않도록 한 사용자 요청.
- [Title_Logo_Concept_Brief_20261115.md](01_Choice/Title_Logo_Concept_Brief_20261115.md) : 던그리드 기준 시안20종 제작부터17번 수정, 사용자 직접 수정본 최종 확정과 Aseprite·PNG 두 파일만 남긴 정리까지의 선택·이유 이력.
- [Reference_Git_Ignore_20261115.md](01_Choice/Reference_Git_Ignore_20261115.md) : 레퍼런스 파일 전체 Git 제외와 지정 메시지 커밋 요청.
이 디렉토리에는 개발자가 코드 작성을 하거나, 전체 구조를 결정하거나, 그외 중요 사안에 관해, 주어진 여러 선택 사항·비교 사항들 중 한가지를 선택한 경우, 주어진 사안을 제목으로 하고 내용은 선택안과 그 이유를 작성한다.

이 디렉토리에 작성되는 것들은 "선택된 것"들에 대해서 적으나, '임시처리'와는 명확하게 구분한다. 

즉, 이곳에 작성된 것들은 이후 더 좋은 방안이 제기되거나 진행 상황에 맞춰서 선택안이 변경될 수 있어도 결국 당시에는 납득가능한 이유가 있어 선택된 것들을 기록하지만, 
'임시처리'된 것은 어떠한 사유로 더 나은 방안을 찾으려는 시도를 하지 못했거나 안한 것들이거나, 납득될만한 이유 없이 단순 시간 부족만을 이유로 빠르게 처리하기 위해 임의로 선택한 사안들이므로 '02_Hold'에 분리해서 기록한다.
- [Emergency_Operation.md](01_Choice/Emergency_Operation.md) : 2026-11-14 제출 전 비상 운영 전환, 문서 일괄 확인 및 단계별 완성 기준을 요청한 이유와 정책 원본 위치.
- [Notion_Organization.md](01_Choice/Notion_Organization.md) : 클래스 정의 우선·케이스 흐름 동시 작성, 코드별 Notion 기록·기능별 토글과 이후 미리보기 설정·확인을 체크에서 제외한 요청.
- [Notion_Answers_20261115.md](01_Choice/Notion_Answers_20261115.md) : 2026-11-15 Q1~Q29 선택 이유·대안, 후속 질문 압축·Profiler 비교·콘텐츠/연출 중심 완성 목표의 경위. 현사양 원본·구현 결과와 구별함.
- [Player_Idle_Concept_Brief_20261115.md](01_Choice/Player_Idle_Concept_Brief_20261115.md) : Player 시안 조건의 변경·반려 이력과 사용자 수정본 default 선택, 이전 시안 삭제 및 Idle4방향 제작 요청·이유. 새 파생 방향의 미술 승인과 구분함.

- [SaveData_Simplification_20261115.md](01_Choice/SaveData_Simplification_20261115.md) : key·타입을 알고 있다는 전제의 SaveData 단순화와 세피리아 구조, 문서/Project_Chrono 경로·ProjectChrono 기준 키, Notion Class 갱신 요청·이유. 현재 구현 원본은 GAME_SPEC S05·S06·2.4임.

## 2. 02_Hold
이 디렉토리에는 아직 검증·비교하지 못했거나 당장 결정하지 못해 임시처리로 둔 과제·사안들을 기록함으로써, 잊지 않도록 하는 것을 목적으로 둔다.

- 'AI_활용_기록.md' : 이 문서는 이후 AI를 얼마나 잘 활용했는가를 기록하기 위해 어떻게 측정할지를 그 방안에 대해 고민하는 것이다.
- [Notion_Comparisons_20261115.md](02_Hold/Notion_Comparisons_20261115.md) : 직접 OnDie·처치 Boss 배치·4/8방향·반사 방식·FSM·Map 책임 분리·컷씬 skip의 후속 검토와 미검증 범위. Profiler 성능 비교 계획은 GAME_SPEC 3.1.1로 연결함.

## 3. 03_Analysis

- [Sephiria_Melee_Hit_Feedback_20261116.md](03_Analysis/Sephiria_Melee_Hit_Feedback_20261116.md) : 원작 성공 타격의 실제 접점·수직±10도·반경0.2·7장20Hz 섬광과 Bank 음원 연결, 우리 단일 적중음·절대시간 재생의 차이

- [Tilemap_Texture_Bleeding_20261116.md](03_Analysis/Tilemap_Texture_Bleeding_20261116.md) : 타일 간격의 밝은 가로줄과 이웃 픽셀 대조, 맵 Atlas·4px 패딩 적용 후 사용자 해결 확인, 실제 Bilinear 설정과 UX 경험용 문장. 해결 전 캡처2장·미측정 범위를 함께 보관한다.

- [Draw_Camera_Gungeon_20261116.md](03_Analysis/Draw_Camera_Gungeon_20261116.md) : 원작 마우스 카메라의 실제 분기·화면비/거리 가중/상한·즉시 반영과 기존 Draw의 답답함 원인, 적용 범위·정적 비교·미확인 한계

- [Player_Dash_And_Weapon_Sorting_20261116.md](03_Analysis/Player_Dash_And_Weapon_Sorting_20261116.md) : 실제 Player 대시24/87·0.14/0.10·정적분2.5074와 그룹 내부±1의 전역 적용 오류·몸체 정렬 캐시 수정 근거

- [Sephiria_Attack_Integration_20261116.md](03_Analysis/Sephiria_Attack_Integration_20261116.md) : 원작대검의피벗/반사·입력무시/버퍼·콤보/AS·기본속도/공격전진과R14실게임이식,추론한3타회복정책의근거. 실제검증상태는GAME_SPEC2.1.3을따른다.
원본에서 확인한 사실·정적 계산·실제 실행 관측을 구분하고 제작용 수치와 재현 자료를 보관한다. 분석 완료는 게임에 적용하거나 미술을 확정한 사실과 구분한다.

- [Update_Order_Comparison_20261116.md](03_Analysis/Update_Order_Comparison_20261116.md) : 조사 시점44런타임 파일·원작74코드/meta와 예정 기능의 실행 순서 비교.1~11절은 이전 분석,12절은 Update0/3 적용·자동19검사,13절은 Fixed/Late 도입 경위부터 사용자 직접 플레이의 드득거림 해소 확인까지 정리한 해결 기록이다.

- [Sephiria_Player_Animation_Architecture_20261116.md](03_Analysis/Sephiria_Player_Animation_Architecture_20261116.md) : 기본 Player/PinkRabbit의 몸체 AnimationSet·별도 무기 Animator, 행동별 상태·FPS·시간·초기화 순서·실제 참조와 우리 이관 전 구조의 차이. 원작 실행 검증은 미실행이며 이관 구현·검증 상태는 GAME_SPEC3.3.11을 따른다.

- [Camera_Follow_Sephiria_Comparison_20261116.md](03_Analysis/Camera_Follow_Sephiria_Comparison_20261116.md) : Sephiria 카메라/이동 코드·실행 순서와 현재50Hz 이동·렌더 추적 비교. 화면 주사율·VSync 후보·산술 모델과6절의 코스튬/몸체 Sprite 애니메이터 실제 Update 순서를 포함하며 실행 측정·해결은 미실행이다.

- [Dungreed_HUD_20261116.md](03_Analysis/Dungreed_HUD_20261116.md) : HP·Dash5칸·스킬 UI의 코드/씬/Atlas와320×180비율 분석, 사용자 가이드 배치·HP 정보 칸 제거 및1920×1080 프리뷰. 원작 실행·Unity 연결은 미실행이다.

- [Sephiria_GreatSword_Attacks_20261115.md](03_Analysis/Sephiria_GreatSword_Attacks_20261115.md) : 기본 대검 1·2타의 실제 Animator 경로, 프레임별 위치·회전·칼끝 궤적, 검기4장과 타이머를 분석한 제작 참고. CSV·JSON·비교 그림·원본 해시를 포함하며 원작 런타임 캡처는 미실행이다.
