# Project_Chrono 개발 결과 종합

정리 기준일: 2026-11-16 (Asia/Seoul)

이 문서는 기획 확정, 구현, 아트 제작, 문제 해결과 검증을 종합한 최종 결과다. 현재 사양과 실제 API·Sequence Diagram의 원본은 [GAME_SPEC](../GAME_SPEC.md), 선택 이유는 [결정 기록](../archive/README.md), 편집 가능한 확정 아트는 [art/archive](../../art/archive/)에 둔다. 후속 사용자 요청으로 중간 판본·일회성 검사·복구 사본·미확정 시안·지난 오류 로그를 실제 삭제하고, 확인된 최종 결과는 [검증 요약](Verification_Summary.json)에 남겼다. 이번 정리에서 게임 프로젝트를 수정하거나 기능 검사를 다시 실행하지 않았다.

## 1. 최종 결과와 읽는 순서

| 확인할 내용 | 결과 위치 | 현재 의미 |
| --- | --- | --- |
| 개발 결과와 문제 해결 | 이 문서 | 중간 시행착오를 현재 결과·확인 한계와 연결 |
| 확정 규칙·클래스·기능별 흐름 | [GAME_SPEC](../GAME_SPEC.md) | 사양과 구현 대응의 원본. 각 시점의 검증 이력도 포함 |
| 최종 졸업작품 보고서 | [DOCX](../report/Project_Chrono_Report.docx) · [PDF](../report/32211853_결과보고서.pdf) | 7장·21쪽, 표 11개·실제 화면 1장·코드 7개. 기존 PDF 주석 반영과 렌더 검증 완료 |
| 포스터 | [PPTX](../report/포스터.pptx) · [PDF](<../report/32211853 박지성_포스터.pdf>) | 현재 보관된 편집본과 출력본. 보고서의 21쪽 검증 결과를 포스터 검사로 확대하지 않음 |
| 현재 아트 | [아트 보관소](../../art/archive/) | 확정 편집 원본·게임용 출력·최종 결과 설명 자료 |
| 구조 검토 도면 | [Character 중심 도면](../diagrams/Character_First_Level.drawio) | 사용자 최종 요청의 상속·소유 관계 도면 |
| Windows 배포 빌드 | [최종 확인 요약](Verification_Summary.json) | 14:44:03~14:46:09 KST `Succeeded`. 원시 로그는 삭제하며 전체 실행 파일 시연 완료와는 구분 |
| 질문·답변 | [QUESTIONS](../QUESTIONS.md) | Q1~Q29 답변과 반영 상태. 기존 답변을 다시 미정으로 돌리지 않음 |
| 레퍼런스 적용 근거 | [REFERENCE_CODE_MAP](../REFERENCE_CODE_MAP.md) | 실제 참고 코드와 채택·생략한 책임의 대응 |

확인한 빌드 출력은 `D:/00_Works/02_GameDev/Unity/temp/2026_Capstone_Project_Chrono/program/Project_Chrono.exe`다. 배포 시 실행 파일과 같은 출력 폴더의 데이터·런타임 파일을 함께 사용한다. 이번 문서 정리에서 배포 폴더를 복사하거나 재빌드하지 않았다.

## 2. 완성한 플레이 흐름

현재 결과는 서버 없는 싱글 플레이의 경비 로봇 데모다. 하나의 고정 무기, 이동·대시·3타 일반공격과 Draw & Slash를 연결했으며, 전체 원문에 있던 6보스·38패턴 가운데 경비 로봇 1명·5패턴이 현재 제작 범위다. 나머지 보스의 시안·정적 배치를 구현된 전투 콘텐츠로 세지 않는다.

| 영역·기능 | 구현·연결 결과 | 확인 결과와 경계 | 상세 원본 |
| --- | --- | --- | --- |
| 시작·Title·저장 슬롯 F-S01/F-U01 | 공통 슬롯 로드, Title 자체 준비, 3슬롯 선택·완료 슬롯의 새 게임 확인, MainWorld 생성과 실패 복구 | 초기화·저장·재진입 검사 및 최종 슬롯 전용 검사. 설정 버튼은 비활성 | GAME_SPEC 2.4.1·2.4.2·3.3.14·3.3.15 |
| 이동·대시 F-P01/02 | WASD·커서, 충돌 보정, 대각 정규화, 이동 우선 대시와 정지 커서 방향 | Update의 같은 프레임 이동·Rendering 순서 검증과 사용자의 끊김 해소 확인 | GAME_SPEC 2.1.1·2.1.2 |
| 일반공격 F-P03 | 방향 고정·3콤보·공격속도, 전진·검/검기 정렬, 회전 사각 판정·중복 방지·취소 | 공격·대시 회귀와 실제 모션 연결. 최종 공격 전진 기본 거리는 입력 유무와 무관하게 0.5 | GAME_SPEC 2.1.3 |
| Draw·Line F-P04~06 | 별도 Action Map, 두 점으로 고정 길이 직선 지정·초기화·확정·취소, Line 수명·점멸·만료 VFX | 입력 누출 방지·0/1/2점·상태 복구·수명 경계 확인. Line 이미지 FX는 R09 팔레트 연동까지 반영 | GAME_SPEC 2.1.4~2.1.6·3.3.13 |
| Slash F-P07 | Line의 논리 소비와 시전 표시 분리, 단계별 줌·발도·섬광·납도·지연 공간참, 마지막 1회 판정 | 표시 오라와 판정 폭 일치·취소 복구·보상 1회 확인. 반사 금색탄은 Line 접선 법선에 대한 고정 반사 방향 사용 | GAME_SPEC 2.1.7·2.2.6 |
| 피격·사망·부활 F-P08/09 | 피해 확정 사건과 피격 보호/표시 분리, 같은 Player의 사망·자동 부활과 초기화 | 치명 피해 1회·보호 만료·빨강 표시 원복·정지·비활성·재진입 검사 | GAME_SPEC 2.1.8·2.1.9 |
| 수정·고정 효과 F-P10/11 | 수정 후보 우선 F, 회복 완료 저장·문/체크포인트, Fever·Slow·Groggy의 적용·제거·갱신 | 공통 F의 Slash 누출 방지, 저장 경계와 효과 수명 확인. 오라의 최종 미술 판단은 별도 | GAME_SPEC 2.1.10·2.1.11 |
| 보스 공통 F-B01~06 | 저장 상태별 생성, 첫/재조우, 조건·무작위·우선 패턴, 그로기·취소, 처치·사체·문 복구, 투사체/부위 | 당시 공통 기반 96묶음 통과. 제출 전 시험 보스·검사 코드는 제거됐으므로 당시 검사 재실행 메뉴로 안내하지 않음 | GAME_SPEC 2.2.1~2.2.6·3.7 |
| 경비 5패턴 F-B07 | 금색탄 단발, 한 손 추적 낙하, 중앙 양손/좌우 12탄, 눈 레이저, 5총구 순회 부채꼴 | 패턴별 실행·취소·충돌 검증 및 현재 5후보 등록 확인. 아래 표에서 최종 규칙과 이전 이력을 연결 | GAME_SPEC 2.2.7~2.2.11 |
| 최초 입장 F-S05 | 새 게임 최초 1회 화면 밖에서 실제 Player 보행, 문 비가시 고정 화면, 중앙 인계 후 일반 추적·입력/HUD 복구 | R03의 전용 7검사·문 전체 비가시·저장/중단·벽 경계 확인. 안내·대사 전체 튜토리얼은 별도 | GAME_SPEC 2.4.3 |
| 데모 엔딩 F-S06 | 경비 OnDie 뒤 암전·중앙 안내·Title 복귀, 기존 처치 저장 유지 | 전용 4묶음과 중단·정지·재연결·완료 슬롯 보존 확인. 정식 스탭롤의 임시 대체 | GAME_SPEC 2.4·[엔딩 선택](../archive/01_Choice/Temp_Demo_Ending_20261116.md) |

## 3. 시행착오를 종합한 문제 해결 결과

각 행의 결과는 해당 수정 뒤 실제 확인한 범위다. 검사 횟수는 서로 다른 시점·스냅샷의 묶음이며, 아래 숫자를 합산해 최종 전체 회귀 검사 수로 사용하지 않는다.

| 주제 | 중간 문제·판단 | 최종 처리와 확인한 결과 | 근거 |
| --- | --- | --- | --- |
| 1. 기획 충돌과 범위 | 원문 메모·과거 계획·현재 구현의 범위가 섞임 | Q1~Q29 답변을 통합하고 경비 1명·5패턴의 단일 사양으로 연결. 나머지 콘텐츠는 후속으로 보존 | [질문 답변](../QUESTIONS.md) · [결정](../archive/01_Choice/Notion_Answers_20261115.md) |
| 2. 부팅·UI 책임 | GameManager가 Title 준비와 UI를 직접 아는 구조 | GameManager는 공통 로드·전환, UI_Title은 자기 준비·입력을 담당. 슬롯 로드/Intro 병렬 준비·Current 참조 유지 검사 | [책임 결정](../archive/01_Choice/Title_Save_UI_Responsibilities_20261115.md) |
| 3. SaveData 범위 | 저장 데이터가 게임별 키·스키마까지 검증 | key/type을 아는 호출자가 Dictionary 값을 사용하고 SaveManager가 기본값·파일 복구를 담당. 임의 키·목록 복사·순차 기록 확인 | [저장 결정](../archive/01_Choice/SaveData_Simplification_20261115.md) |
| 4. Instance와 초기화 | 먼저 조회하거나 매 프레임 고정 참조를 다시 찾을 위험 | Awake 자기 준비·Start 외부 참조 또는 명시적 생성 완료 순서를 구분하고 교체 경계에서 갱신. 재진입·해제 검사와 함께 기록 | [참조 수명 결정](../archive/01_Choice/Shared_Instances_Reference_Workflow_20261115.md) |
| 5. 이동의 드득거림 | 위치는 FixedUpdate, 표시·카메라는 렌더 프레임에서 소비 | Character.Update에서 Body.Move 즉시 적용→Rendering.Update(3)→Camera.LateUpdate로 연결. 자동 19검사와 사용자 직접 끊김 해소 확인 | [실행 순서 분석 12·13절](../archive/03_Analysis/Update_Order_Comparison_20261116.md) |
| 6. 검·검기 가림 | 원작의 그룹 내부 상대 정렬값을 전역 정렬에 적용 | 몸체 정렬 기준의 앞뒤 순서로 수정. 실제 아래 방향 검 가시성과 공격/대시 회귀 확인 | [대시·정렬 분석](../archive/03_Analysis/Player_Dash_And_Weapon_Sorting_20261116.md) |
| 7. 일반공격 전진 | 무입력·입력의 서로 다른 전진 거리와 임시 감소 튜닝 | 사용자 최종 선택으로 모두 기본 거리 0.5 사용. 각 타 시작 방향 고정·공격 18/대시 8검사 확인 | [공격 선택 이력](../archive/01_Choice/Player_Attack_Sephiria_20261115.md) · GAME_SPEC 2.1.3 |
| 8. Player 애니메이션 연결 | 몸체·무기·공격 상태의 책임과 프레임이 분산 | 기본/발도 AnimationSet과 별도 무기 모션을 연결. 최종 아트 16묶음 통과. 이 결과를 최신 전체 회귀 성공으로 확대하지 않음 | [애니메이션 분석](../archive/03_Analysis/Sephiria_Player_Animation_Architecture_20261116.md) · GAME_SPEC 3.3.11 |
| 9. 타일맵 밝은 줄 | 처음에는 부하·화면 동기화를 의심했으나 VSync에서도 발생 | 캡처의 타일 간격과 원본 이웃 픽셀을 대조하고 Atlas 패킹·4px 패딩 적용 후 사용자 해결 확인. 실제 Atlas 필터는 Bilinear | [Texture Bleeding 해결](../archive/03_Analysis/Tilemap_Texture_Bleeding_20261116.md) |
| 10. Draw 시야 | 기존 커서 추적이 의도한 조준 범위를 충분히 보여주지 못함 | ETG의 제한 조준 오프셋을 현재 화면비·맵 경계에 맞게 변형. 전용 Draw 진입/종료·커서 반전·경계 검사 | [Draw 카메라 분석](../archive/03_Analysis/Draw_Camera_Gungeon_20261116.md) |
| 11. 일반 카메라 반응 | SmoothDamp 내부값과 실제 접근 시간이 혼동됨 | 일반/컷씬의 90% 접근 약 0.25초를 목표로 내부값 0.1283 적용. 전용 10항목과 unscaled 실제 접근 관측 확인 | [카메라 분석 7·8절](../archive/03_Analysis/Camera_Follow_Sephiria_Comparison_20261116.md) |
| 12. Draw 이미지 FX | 단색 띠·검은 고정 테두리가 사용자 의도와 달라 반려 | 후속 수정으로 원형 알갱이·1px 외곽을 보존하고 R09에서 LineVFX의 white/cyan/blue RGB에 연동. 전용 23검사와 실제 GIF 확인 | GAME_SPEC 3.3.13 · [Draw 책임 결정](../archive/01_Choice/Player_Draw_Responsibilities_20261115.md) |
| 13. Slash 경로 표시 | 시작에 Line을 소비하면 시전 동안 경로가 사라짐 | 논리 소비 시점과 시전 snapshot 표시를 분리. 발도·줌·지연 공간참 중 경로 유지와 종료/취소 정리 확인 | [Slash 연출 결정](../archive/01_Choice/Player_Slash_Presentation_20261116.md) · GAME_SPEC 2.1.7 |
| 14. Slash 시각/판정 폭 | 보이는 오라보다 실제 판정이 좁음 | Line.Width 0.25를 유지하고 표시 오라 전체 폭 0.75를 시전 시작 HitWidth로 고정. 실제 메시·30도 양쪽 경계·폭 snapshot 포함 29검사 | GAME_SPEC 2.1.7 |
| 15. 금색탄 반사 | 초기 Boss 유도 방식은 Line 각도로 예측하는 반사와 다름 | 입사 방향을 Line 접선의 법선에 반사해 위치와 무관한 고정 직진 사용. Draw/Slash 32·실제 Gold 17검사에서 각도·벽 우선·적중/보상 1회 확인 | GAME_SPEC 2.1.7·2.2.6 |
| 16. 성공 적중 위치 | 겹친 사각 Cast 접점이 기대한 표면 위치와 다름 | 피해 전 ClosestPoint 보정, 실제 성공 적중만 섬광·음향 재생. Player 피드백 전용 23검사 확인 | [적중 분석](../archive/03_Analysis/Sephiria_Melee_Hit_Feedback_20261116.md) · [음향 선택](../archive/01_Choice/Player_Feedback_Audio_20261116.md) |
| 17. Title 메뉴 상자 | GUI.skin.button 기반 스타일이 회색 배경을 남김 | 새 GUIStyle과 상태별 background 설정으로 글자 메뉴 표시. 실제 빈 배경 영역·슬롯 전환·VFX의 정지 중 재생 확인 | GAME_SPEC 3.3.14 · [Title 선택](../archive/01_Choice/Title_Logo_Concept_Brief_20261115.md) |
| 18. Title 검 가독성 | 검 외곽이 어두워 글자/칼날 위치가 불명확 | 손잡이·검집의 흰 1px 외곽과 별도 3위상 푸른 광 추가. 원본 10레이어 보존·변경 96px·최종 7검사/실제 GIF 확인 | [Title R02](../../art/archive/title/title-screen-r02/README.md) |
| 19. 저장 슬롯 가독성·복귀 | 작은 카드·뒤로 버튼·정보 밀집과 Esc 동작 부족 | 가로 3카드 높이 206·항목 사이 빈 줄·Bold·슬롯 전용 Esc→Title 적용. R03 focused 5검사와 실제 정보 4그룹 렌더 확인 | GAME_SPEC 3.3.15 · [프레임 선택](../archive/01_Choice/SaveSlot_Frame_20261116.md) |
| 20. 배경과 검흔 대비 | 초기 밝은 바닥에서 검흔 구분이 약하고, 진회색안은 너무 어두움 | 검흔 14PNG를 보존하고 밝은 벽·중간 밝기 바닥의 C안을 사용자 선택대로 반영. 원본/출력 일치 확인; 움직이는 가독성 실측은 별도 | GAME_SPEC 3.3.6 · [최종 환경](../../art/archive/environment/laboratory/final/README.md) |
| 21. 한 손 낙하 경고 | 초기 고정 경고는 추적 규칙을 잘못 해석 | 시작부터 손 지면 투영을 따라 경고·내부 채움 진행, full 때 위치 고정 후 0.2게임초 뒤 착지 1회 피해. 손 통합 16·Gold 10회귀 확인 | GAME_SPEC 2.2.8 · [경비 선택](../archive/01_Choice/Boss_Guard_Pattern_20261116.md) |
| 22. 중앙 양손 공격 | 손 모양과 실제 충전/착지·탄 발사 연결 필요 | 중앙 큰 원·양손 내려침의 착지 callback에 좌우 세로줄 12탄과 원형 피해 연결. 실제 12발과 취소/복구 검사 | GAME_SPEC 2.2.9 |
| 23. 탄환 벽 처리·부채꼴 | 벽 뒤 적중 가능성과 수명 만료가 패턴 의도를 흐림 | 이동 구간에서 벽/문을 먼저 검사하고 0수명은 벽까지 유지. 5총구 두 바퀴·0.3게임초 간격 10묶음 50발, 벽/Fan·손·공통 총 44검사 | GAME_SPEC 2.2.10·2.2.6 |
| 24. 눈 레이저 피해 위치 | 공중 빔 전체가 피해 범위처럼 보임 | 유한 지면 종점의 밝은 두 원·흰 중심을 표시하고 그 원만 피해. 0.4초 과거 지면 추적 유지, 전용 12/손 16 총 28검사에서 빔 중간 무피해 확인 | GAME_SPEC 2.2.11 · [R02 결과](../../art/archive/boss/guard-laser-impact-r02/README.md) |
| 25. 최초 입장 연출 | 문이 보이거나 Player가 처음부터 화면 안에 있고 중앙 인계가 부정확 | R03 남쪽 복도·독립 도착점으로 문 전체를 화면 밖에 두고 실제 보행 후 정확 중앙에서 입력 인계. 7검사와 실제 GIF 확인 | GAME_SPEC 2.4.3 · [입장 R03](../../art/archive/map/player-entrance-r03/README.md) |
| 26. Fever 오라 | 초기 금색 입자가 몸에 가려지고 상승 표현이 부족 | 상태 사건에 재사용 입자를 연결하고 폭·높이·밀도 조정. 최종 11검사와 실제 정지·이동 GIF 확인. 기술 검사와 최종 미술 판단을 구분 | GAME_SPEC 2.1.11 · [오라 결과](../../art/archive/fx/player-fever-r01/README.md) |
| 27. 종료 흐름 | 정식 스탭롤을 구현할 시간 대신 짧은 데모 마감이 필요 | 사용자 요청으로 `TempDemoEnding`이 처치 뒤 암전·안내·Title 복귀 수행. 정지·중단·재연결·완료 저장 보존 4묶음 | [엔딩 선택](../archive/01_Choice/Temp_Demo_Ending_20261116.md) |
| 28. 제출 소스 정리 | 검사·Setup·더미·계측 코드가 실제 실행 소스에 혼재 | Editor 47·시험 런타임 4·시험 콘텐츠 제거, 실제 경비 참조/GUID 보존. 남은 런타임 53·Editor 0, 참조 누락 0·격리 스모크 6개 통과 | [소스 정리 결과](../archive/01_Choice/Submission_Unity_Cleanup_20261116.md) · GAME_SPEC 3.7 |
| 29. Player 빌드 실패 | Editor 전용 GUIStyleState.scaledBackgrounds를 런타임 UI에서 호출 | 두 대입 제거로 Player API 불일치 해결. 14:46 Windows 빌드 `Succeeded`와 출력 경로 확인 | [최종 확인 요약](Verification_Summary.json) |
| 30. 보고서 최종화 | 42쪽 초안의 과도한 분량과 내부 작업자료 의존, 후속 PDF 주석 | 7장 구조로 축약 후 필요한 물리/Draw/입력/저장 설명을 보강해 21쪽 완성. 문구·본문 일치·글꼴 포함·변경 페이지 시각 검토 확인 | [보고서 결정](../archive/01_Choice/Graduation_Report_20261116.md) · [검증 JSON](../report/evidence/ReportVerification_20261116.json) |

## 4. 아트·화면·음향 결과

아트의 `final` 또는 가장 큰 R번호만으로 승인 여부를 판단하지 않는다. 현재 채택 상태는 GAME_SPEC의 해당 항목과 사용자 선택 기록으로 확인한다. 아래는 남긴 편집 원본·게임용 출력의 시작점이며, 대체된 비교안과 미확정 시안은 삭제했다.

| 대상 | 결과·편집 시작점 | 연결·확인 상태 |
| --- | --- | --- |
| Player 기본 모션 | [player/final](../../art/archive/player/final/) | 사용자 확정 native 8개·PNG 8개, 활성 33프레임을 정리하고 게임에 연결. 좌우 대칭과 Death/Spawn 예외는 원본 계약 유지 |
| 발도 모션 | [player/iaido-r02](../../art/archive/player/iaido-r02/) | 사용자 결정에 따라 R02를 현재 발도/납도 원본으로 사용 |
| 검·3콤보·검기 | [최종 콤보 R14](../../art/archive/weapon/player-combo-preview-r14/README.md) · [검기 편집 원본](../../art/archive/weapon/greatsword-vfx-edit/README.md) | 실제 54검 포즈·검기 구성과 3타 후속 수정 반영 |
| 실험실·문·복도 | [laboratory/final](../../art/archive/environment/laboratory/final/) | 맵·문 프레임과 C 팔레트 원본/Unity 출력 동기화. 세부 import 관찰과 플레이 확인의 차이는 3.3.6에 명시 |
| 수정 | [amethyst/final](../../art/archive/environment/amethyst/final/README.md) | 사용자 수정 원본 보존, 비활성/활성 부유 출력과 Animator 준비. 실제 상호작용 연결 검증은 F-P10에서 별도 확인 |
| 최초 입장 | [입장 R03](../../art/archive/map/player-entrance-r03/README.md) · [복도 R03](../../art/archive/environment/entrance-corridor-r03/) | 현재 입장 연출·기하와 최종 실제 GIF |
| 경비 로봇·위험 표시 | [boss](../../art/archive/boss/) · [fx](../../art/archive/fx/) | 경비 정면·손 분리·5패턴·탄환/경고·지면 착탄 표시를 연결. 최종 크기·피해·충전 시간 일부는 초기 튜닝값 |
| 군용 보스 | [military-8dir-r04](../../art/archive/boss/military-8dir-r04/README.md) · [정적 배치](../../art/archive/boss/military-map-placement-r01/) | 승인된 시각 결과와 Map 배치. AI·공격·충돌·진행 보스 등록 완료로 세지 않음 |
| Title | [로고 원본](../../art/archive/title/logo_17_natural_cut_r06/) · [Title R02](../../art/archive/title/title-screen-r02/README.md) | 확정 로고, 1px 검 외곽, 별도 십자/푸른광과 실제 재생 GIF |
| HUD·슬롯 | [ui/final](../../art/archive/ui/final/) · [공용 프레임](../../art/archive/ui/window-frame-r01/README.md) | HUD/슬롯 연결 확인. 미확정 조작 안내 시안은 삭제했으며 게임 안내 기능을 추가 구현한 것은 아님 |
| Player 음향·적중 섬광 | [audio](../../art/archive/audio/) · [선택 기록](../archive/01_Choice/Player_Feedback_Audio_20261116.md) | PCM 6개와 Unity 내장 AudioSource, 콤보 A/B·실제 걷기·성공 적중 섬광 연결. BGM·전체 음량·보스 전체 음향은 후속 |

## 5. 검증 결과의 해석

| 근거 종류 | 이 자료가 확인하는 것 | 확인하지 않은 것 |
| --- | --- | --- |
| 기능별 격리 Unity 검사 | 해당 시점 소스·프리팹에서 입력/상태/피해/저장/복구가 기대한 분기를 수행 | 이후 동시 변경까지 포함한 최종 전체 회귀 |
| 해시·GUID·파일 검사 | 원본 보존, 검증본/반영본 일치, 참조 연결·프레임/픽셀/시간 일치 | 실제 게임의 최종 미술 만족도·모든 환경 동작 |
| Unity 실제 캡처·GIF | 지정 장면에서 보인 배치·타이밍·가독성 | 캡처하지 않은 전체 플레이·최종 밸런스 |
| 사용자 직접 확인 | 이동 끊김·타일 줄 등 보고된 재현 조건에서의 해결 | FPS 향상·CPU/GPU/GC 감소·정량 사용자 평가 |
| Windows 빌드 최종 요약 | 확인된 Player 빌드 성공·시각·출력 경로 | 실행 파일 전체 시연과 다른 PC에서의 배포 검사 |
| 보고서 렌더 검증 | 21쪽 DOCX/PDF 본문 일치·글꼴 포함·페이지 시각 검토 | 게임 기능 추가, 성능 비교 또는 포스터의 동일 검증 |

Windows 빌드 성공과 제출 소스6스모크의 결과는 [Verification_Summary](Verification_Summary.json)에 남긴다. 원시 로그·중간 검사 JSON·시험 프로젝트는 삭제했다. 빌드 성공을 각 기능의 원본 수동 Play·최신 전체 회귀까지 통과한 것으로 확대하지 않는다.

## 6. 완료로 바꾸지 않은 항목

| 항목 | 정리 시점의 상태 | 결과 자료의 처리 |
| --- | --- | --- |
| 최종 Windows 실행 파일의 처음부터 끝까지 시연·다른 PC 검사 | 전체 완료 근거 없음 | 빌드 성공과 분리해 확인 필요로 유지 |
| 최종 소스 전체 보스 패턴 회귀 | 패턴별 검사와 정리 후 스모크는 있음. 모든 후속 변경을 합친 전체 회귀 근거는 별도 없음 | 당시 스냅샷·전용 검사 범위를 보존 |
| 기존 Startup 검사의 현재 맵 대응 | 옛 고정좌표 전제가 현재 맵과 불일치해 일부 실패, 전체 성공으로 기록하지 않음 | 최종 아트 16검사 등 성공 근거와 실패 이력을 구분 |
| Profiler 대안 비교 | PF01~PF05와 조건부 PF06~PF07 계획, 실제 성능 A/B 측정 미실행 | 최적화 고려·반복 조회/배열 재사용은 설명하되 속도·메모리 개선 수치를 만들지 않음 |
| 전체 안내·대사 튜토리얼 | 최초 입장 연출 구현, 전체 안내/dialogue 후속 | 입장 완료와 분리 |
| 정식 스탭롤 | 사용자 요청의 데모 문구·Title 복귀로 임시 대체 | 임시 엔딩 결과로 명시 |
| 설정·키 재지정 UI | 현재 설정 버튼 비활성, 향후 계획 | Input System 채택과 설정 화면 구현을 구분 |
| BGM·전체 음량·대시/Fever/Boss 전체 음향 | 현재 Player 일부 효과음 범위 외 미완 | 현재 PCM/적중/걷기 연결만 완료로 집계 |
| 실제 연결한 FX·음향의 미술·청감 확정 | 기술 구현과 사용자 최종 판단을 구분 | 현재 사용하는 결과와 남은 판단만 표시 |
| 다른 5보스·33패턴·8방향 Player 등 확장 | 원문·후속 아이디어, 현재 제출 범위 밖 | 실제 군용 형상·정적 배치와 미구현 전투를 구분. 미확정 시안은 삭제 |
| Notion | 기능 작업 때 저장 성공 기록이 있음 | 이번 정리는 로컬 문서 작업. 원격 최신 일치나 자동 동기화를 주장하지 않음 |

과거 보류표의 황금탄 Boss 유도안은 후속 Line 반사 결정으로 대체됐다. 반사각 자체를 여전히 미정으로 읽지 않는다. 직접 이벤트와 중계자, 처치 Boss 생성 전략, 범용 FSM, Map 추가 분리, 일부 컷씬 skip 등 미측정·미선택 대안은 [보류 기록](../archive/02_Hold/Notion_Comparisons_20261115.md)과 GAME_SPEC의 현재 규칙을 함께 읽는다.

## 7. 최종 파일 정리와 후속 사용

사용자 후속 요청으로 반려·미확정 시안, 대체된 이전 판본, 일회성 검사·전송·촬영 자료, 지난 오류 로그와 중복 검증 목록을 삭제했다. 의미 있는 문제 해결 결론과 선택 이유는 이 문서·결정 기록·분석에 남기며, 확정 편집 원본과 게임용 출력은 아트 보관소에서 찾는다.

시험/복구용 Temp와 아트 작업 사본, 이전 STATUS/PLANS, 보고서 생성 중간물·일회성 참고자료도 삭제했다. 별도 백업은 만들지 않았다. Git에 기록된 파일은 Git으로 복원할 수 있으며, 추적되지 않은 작업·캐시 자료에는 별도 복구 사본이 없다. 삭제 및 최종 결과 보존 확인은 [Cleanup_Result](Cleanup_Result.json)를 따른다.

향후 개발을 재개할 때에는 [STATUS](../agents/STATUS.md)의 다음 행동과 [PLANS](../agents/PLANS.md)의 미완 범위를 확인하고, 해당 기능의 GAME_SPEC·기존 결과를 기준으로 필요한 검사만 수행한다. 문서 정리 완료는 미완 기능·미실행 평가가 모두 완료됐다는 뜻이 아니다.
