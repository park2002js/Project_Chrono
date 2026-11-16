# 문서·자료 파일 색인

2026-11-16 최종 정리. [개발 결과 종합](Development_Results.md)과 [GAME_SPEC](../GAME_SPEC.md)을 먼저 읽고, 아래에서 현재 결과·편집 원본·결정 이유·유효한 분석을 찾는다. 구버전·미확정 시안·일회성 검사·지난 오류 로그는 사용자 후속 요청으로 삭제했다.

## 결과·제출·구조

| 자료 | 위치 |
| --- | --- |
| 문서 시작점 | [문서 안내](../README.md) |
| 개발 결과·문제 해결30사례 | [종합 결과](Development_Results.md) |
| 최종 보고서·포스터 | [제출물 안내](../report/README.md) |
| 현재 아트·음향 편집 원본과 출력 | [아트 카탈로그](../../art/archive/README.md) |
| 개발자 기획 원문 | [기획 자료 안내](../notes/README.md) |
| Character 상속·소유 관계 | [최종 관계도](../diagrams/Character_First_Level.drawio) |
| 현재 상태·계획 | [STATUS](../agents/STATUS.md) · [PLANS](../agents/PLANS.md) |
| 레퍼런스 코드 대응 | [REFERENCE_CODE_MAP](../REFERENCE_CODE_MAP.md) |
| 질문·확정 답변 | [QUESTIONS](../QUESTIONS.md) |
| 빌드·소스 정리의 최종 확인 사실 | [Verification_Summary](Verification_Summary.json) |
| 이번 삭제·최종 결과 보존 확인 | [Cleanup_Result](Cleanup_Result.json) |

## 선택 결정

| 문서 | 주제 |
| --- | --- |
| [Art Archive Git 20261115](<../archive/01_Choice/Art_Archive_Git_20261115.md>) | 아트 완성 결과물 보관과 작업 자료 Git 제외 |
| [Art First Integration 20261116](<../archive/01_Choice/Art_First_Integration_20261116.md>) | 완성 아트를 현재 게임에 1차 반영 |
| [Boss Common Combat 20261116](<../archive/01_Choice/Boss_Common_Combat_20261116.md>) | F-B01~F-B06 공통 Boss 기반과 더미의 통합 검증 |
| [Boss Front Concept Brief 20261116](<../archive/01_Choice/Boss_Front_Concept_Brief_20261116.md>) | 보스 6종의 탑다운 정면 픽셀 디자인 |
| [Boss Guard Pattern 20261116](<../archive/01_Choice/Boss_Guard_Pattern_20261116.md>) | F-B07-01~02 경비 첫 두 패턴과 출구 배치 |
| [Boss Hazard Visuals 20261116](<../archive/01_Choice/Boss_Hazard_Visuals_20261116.md>) | F-B06 픽셀 탄환과 원형 선 경고 표시 |
| [Camera Follow Response 20261116](<../archive/01_Choice/Camera_Follow_Response_20261116.md>) | 일반 추적과 컷씬 타깃 전환의 카메라 접근 시간 |
| [Controls Guide 20261116](<../archive/01_Choice/Controls_Guide_20261116.md>) | 키보드·마우스 조작 안내 이미지 |
| [Documentation Finalization 20261116](<../archive/01_Choice/Documentation_Finalization_20261116.md>) | project 외부의 개발 문서·아트 안내·중간 기록을 최종 결과 중심으로 정리 |
| [Emergency Operation](<../archive/01_Choice/Emergency_Operation.md>) | 제출 전 비상 운영 체제로 전환 |
| [Graduation Report 20261116](<../archive/01_Choice/Graduation_Report_20261116.md>) | 최종 졸업작품 보고서의 범위와 양식 |
| [HUD Reference Brief 20261115](<../archive/01_Choice/HUD_Reference_Brief_20261115.md>) | 플레이어·보스 체력바 레퍼런스와 중앙 화면 프리뷰 |
| [InputActions Sephiria 20261115](<../archive/01_Choice/InputActions_Sephiria_20261115.md>) | F-P01 입력 콜백과 전달 판단의 책임을 세피리아 구조에 맞게 분리 |
| [Laboratory Arena Preview 20261115](<../archive/01_Choice/Laboratory_Arena_Preview_20261115.md>) | 연구소 실험실을 주 무대로 사용할 때 기존 Player와 비교해 공간의 크기·비율을 판단하기 위한 임시 배치 제작. |
| [No Namespace 20261115](<../archive/01_Choice/No_Namespace_20261115.md>) | 프로젝트 코드의 namespace 제거 및 이후 작성 규칙 |
| [Notion Answers 20261115](<../archive/01_Choice/Notion_Answers_20261115.md>) | 2026-11-15 Q1~Q29의 제작 방향과 후속 문서 압축·성능 비교·완성 목표 요청의 선택 이유를 기록함. |
| [Notion Organization](<../archive/01_Choice/Notion_Organization.md>) | 2026-11-14 사용자가 제공한 Notion 기획을 클래스 정의·케이스 구현 흐름·고민/결정으로 통합함. |
| [Player Attack Sephiria 20261115](<../archive/01_Choice/Player_Attack_Sephiria_20261115.md>) | F-P03 단일 무기의 공격 요청·허용·실행 책임 분리 |
| [Player Dash Sephiria 20261115](<../archive/01_Choice/Player_Dash_Sephiria_20261115.md>) | F-P02 대시를 기존 Character 이동 요청·Body 적용 구조에 연결 |
| [Player Draw Responsibilities 20261115](<../archive/01_Choice/Player_Draw_Responsibilities_20261115.md>) | F-P04 Draw의 전용 입력·편집·표현·복구 책임 분리 |
| [Player Entrance Cutscene 20261116](<../archive/01_Choice/Player_Entrance_Cutscene_20261116.md>) | 새 게임 최초 입장의 복도 확장과 실제 Player 보행 연출 |
| [Player Feedback Audio 20261116](<../archive/01_Choice/Player_Feedback_Audio_20261116.md>) | F-P03·F-U07의 공격·적중·타일 발소리와 적중 섬광 |
| [Player Fever Aura 20261116](<../archive/01_Choice/Player_Fever_Aura_20261116.md>) | F-P11 Player 각성 상태의 황금 오라 표현 |
| [Player Hit Protection Events 20261115](<../archive/01_Choice/Player_Hit_Protection_Events_20261115.md>) | F-P08 피격·보호 사건과 빨강/원본색 표시의 책임 분리 |
| [Player Idle Concept Brief 20261115](<../archive/01_Choice/Player_Idle_Concept_Brief_20261115.md>) | Player 기본 디자인 선택과 Idle·Move·Dash 제작 조건의 변경 이력 |
| [Player Lifecycle Checkpoint Buffs 20261115](<../archive/01_Choice/Player_Lifecycle_Checkpoint_Buffs_20261115.md>) | ConsumeLine 명명과 F-P09~F-P11의 연속 구현 |
| [Player Slash Presentation 20261116](<../archive/01_Choice/Player_Slash_Presentation_20261116.md>) | F-P07 Slash의 발도·카메라·음향·공간참 연출 순서와 조정 방식 |
| [Public Variable Comments 20261115](<../archive/01_Choice/Public_Variable_Comments_20261115.md>) | public 변수 옆에 역할·목적을 설명하는 한 줄 주석 작성 |
| [Reference Git Ignore 20261115](<../archive/01_Choice/Reference_Git_Ignore_20261115.md>) | 로컬 레퍼런스 파일의 Git 제외 |
| [SaveData Simplification 20261115](<../archive/01_Choice/SaveData_Simplification_20261115.md>) | SaveData를 호출자가 key·타입을 아는 단순한 저장 구조로 변경 |
| [SaveSlot Frame 20261116](<../archive/01_Choice/SaveSlot_Frame_20261116.md>) | SaveSlot 화면에 사용할 슬롯 한 개와 이후 설정창에도 사용할 프레임의 제작 요청 |
| [Shared Instances Reference Workflow 20261115](<../archive/01_Choice/Shared_Instances_Reference_Workflow_20261115.md>) | 공유 입력·UI·카메라의 Instance 접근과 원작 코드 기반 구현 절차 |
| [Submission Unity Cleanup 20261116](<../archive/01_Choice/Submission_Unity_Cleanup_20261116.md>) | 제출 전 Unity 프로젝트의 비실행·디버깅 코드 제거 |
| [Sword Size First 20261115](<../archive/01_Choice/Sword_Size_First_20261115.md>) | 플레이어 검과 FX의 제작 순서 |
| [Temp Demo Ending 20261116](<../archive/01_Choice/Temp_Demo_Ending_20261116.md>) | 목적 |
| [Title Logo Concept Brief 20261115](<../archive/01_Choice/Title_Logo_Concept_Brief_20261115.md>) | Draw & Slash 로고 시안 제작·17번 수정·사용자 최종 원본 확정 및 작업물 정리 |
| [Title Save UI Responsibilities 20261115](<../archive/01_Choice/Title_Save_UI_Responsibilities_20261115.md>) | 게임 부팅과 Title 준비의 책임 분리, Current 저장 접근 및 UI 이름 규칙 |

## 보류·후속 비교

| 문서 | 주제 |
| --- | --- |
| [Notion Comparisons 20261115](<../archive/02_Hold/Notion_Comparisons_20261115.md>) | 2026-11-15 답변에서 우선 제작안을 정했지만 이후 비교·검토하기로 한 내용을 보존함. |

## 원작 분석·문제 해결

| 문서 | 주제 |
| --- | --- |
| [Camera Follow Sephiria Comparison 20261116](<../archive/03_Analysis/Camera_Follow_Sephiria_Comparison_20261116.md>) | 카메라 추적 UX — Sephiria 비교 및 주사율 후보 분석 |
| [Draw Camera Gungeon 20261116](<../archive/03_Analysis/Draw_Camera_Gungeon_20261116.md>) | Draw 카메라와 Enter the Gungeon 마우스 추적 비교 |
| [Dungreed HUD 20261116](<../archive/03_Analysis/Dungreed_HUD_20261116.md>) | 던그리드 HUD 코드·에셋·위치 비율 분석 |
| [Player Dash And Weapon Sorting 20261116](<../archive/03_Analysis/Player_Dash_And_Weapon_Sorting_20261116.md>) | Player 대시와 무기 정렬 — 원작 값·이식 차이 |
| [Sephiria Attack Integration 20261116](<../archive/03_Analysis/Sephiria_Attack_Integration_20261116.md>) | 대검 공격 이식 — 원작 계약과 R14의 연결 |
| [Sephiria GreatSword Attacks 20261115](<../archive/03_Analysis/Sephiria_GreatSword_Attacks_20261115.md>) | 세피리아 대검 1·2타 — 공격 모션과 검기 프레임 분석 |
| [Sephiria Melee Hit Feedback 20261116](<../archive/03_Analysis/Sephiria_Melee_Hit_Feedback_20261116.md>) | Sephiria 대검 적중 피드백과 프로젝트 음향 연결 분석 |
| [Sephiria Player Animation Architecture 20261116](<../archive/03_Analysis/Sephiria_Player_Animation_Architecture_20261116.md>) | Sephiria Player 애니메이션 구조 분석 |
| [Tilemap Texture Bleeding 20261116](<../archive/03_Analysis/Tilemap_Texture_Bleeding_20261116.md>) | Tilemap 밝은 가로줄 해결 — Texture Bleeding과 시각적 안정성 개선 |
| [Update Order Comparison 20261116](<../archive/03_Analysis/Update_Order_Comparison_20261116.md>) | 주요 갱신 실행 순서 — Project Chrono와 Sephiria 비교 |

## 사용 기준

각 문서는 해당 선택·분석의 이유와 결과를 설명한다. 현재 사양과 구현 상태는 GAME_SPEC과 종합 결과를 따른다. 한때 제작했던 시안이나 기술 검사를 사용자 미술 승인·최신 전체 회귀·성능 우위로 확대하지 않는다.

일회성 전송 조각·검사 프로젝트·이전 STATUS/PLANS·보고서 참고 사례·원시 오류 로그·중복 파일 목록은 남기지 않았다. 이번 정리는 별도 백업 없이 실제 삭제하며, Git 기록이 없는 중간 작업 자료에는 별도 복구 사본이 없다.


