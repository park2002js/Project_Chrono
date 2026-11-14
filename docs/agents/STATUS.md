# Project_Chrono 현재 상태 및 맥락

이 내용은 현재의 진행 상황 및 개발자 세부 요구 사항들을 기록하여, 다른 에이전트나 새로운 세션에서도 이전과 동일한 작업 환경 및 진행도를 갖추는 용도이다.

진행 현황과 관련된 것이란 "현재 진행 중인 과제", "미완·보류 상태에 놓인 과제"들을 의미한다.
"미완·보류 상태"란, 사용자가 과거에 한 번이라도 진행한 이후 완료를 하지 않고 다른 과제를 시작했을 때 놓이는 상태이다.
과제를 기제할 때에는 '세부 과제' 단위로 기록한다. 세부 과제란 앞으로 남은 모든 과제를 말하는 것이 아닌, 다음 단계의 과제를 이루기 위해서 해야 하는 세부 과제들을 의미한다.

과제가 완료되거나, 현재 진행 중인 상태를 보류하고 다른 과제를 진행하게 될 경우, `/docs/agents/PLANS.md`을 현재 상태에 맞게 갱신한다.

마지막 상태가 과제 완료 상태라면 다음 과제가 무엇인지 `/docs/agents/PLANS.md`를 참고한다.


## 1. 현재 상황
### 마지막 갱신
- 마지막 갱신 날짜
  - 2026-11-15 (Asia/Seoul)
- 마지막으로 진행한 과제
  - 저장소 준비 : `/docs/reference/` 전체 Git 제외 적용 후 추적 파일·추가 대상 파일 0개를 확인함. 로컬 파일은 보존하며 현재 문서·아트·Unity 환경 파일을 요청한 커밋에 포함함. [선택 기록](../archive/01_Choice/Reference_Git_Ignore_20261115.md) 참조. 이번 작업에서 Unity 실행·컴파일은 수행하지 않음.
  - Player 새 정면 Idle 시안 V2 : 현재 직접 요청에 따라 [새6종 비교표](../../art/player/idle_concepts_v2_20261115/player-idle-comparison.png)·개별/통합 Aseprite 원본·투명 PNG를 제작하고 파일·규격 검사 및 시각 검토를 마침. 상세 규격·검사·미선택 상태는 GAME_SPEC 3.3을 따름. 기존 시안은 보존했으며 이번 결과를 최종 채택으로 기록하지 않음.
  - Player 시안 재작성 : 이전 왼앞10종은 사용자에게 각도·형태 붕괴로 반려됨. [새 수정안10종](../../art/player/idle_leftfront_redraw_02/player-leftfront-comparison.png)을 다시 그리고 Aseprite 원본/PNG 파일·규격 검사를 완료함. 각도와 미술 품질의 사용자 승인은 아직 없음. 상세 경위·결과는 GAME_SPEC 3.3을 따름. 나머지3방향·애니메이션·Unity 적용은 미실행.
  - 문서 후속 정리 : QUESTIONS의 Q1~Q29를 4개 카테고리 표로 압축하고 GAME_SPEC 3.1.1에 Unity Profiler 비교 후보·동일 조건·측정/채택 기준을 별도 정리함. 콘텐츠·연출 중심의 완성 목표를 상단에 기록하고 Notion에도 같은 범위를 반영함. 비교·기반 구현 자체는 미실행임.
  - Player 후속 R01 : 사용자가 선택한 04머리+06의상 방향을 [새 Aseprite 원본](../../art/player/idle_revision_01/player-front-idle-r01.aseprite)에 반영하고 검수 완료. 실제 규격·레이어·검사 결과는 GAME_SPEC 3.3을 따름. 수정 방향은 확정, 수정 이미지의 최종 승인과 애니메이션·Unity 적용은 아직 없음.
  - Player 정면 Idle 시안 : 사용자 직접 요청에 따라 [6종 비교표](../../art/player/idle_concepts_20x20_20261115/player-idle-comparison.png)와 개별/통합 Aseprite 원본·투명 PNG 제작 및 검수 완료. 규격·검사 근거는 [GAME_SPEC 3.3](../GAME_SPEC.md), 이유는 [요청 기록](../archive/01_Choice/Player_Idle_Concept_Brief_20261115.md)에 기록함. 최종 디자인 선택·애니메이션·게임 적용은 미완료. 이번 아트 결과는 로컬 기록만 갱신했으며 Notion에 게시하지 않음.
  - 계획 문서 통합 : Q1~Q29 답변 반영·사양 방향 정리·Notion 게시 완료.
  - [QUESTIONS.md](../QUESTIONS.md)에 모든 답변을 기록함. 현재 추가 답변 대기는 없으며 이미 답한 항목을 다시 묻지 않음.
  - 현재 사양 원본인 [GAME_SPEC.md](../GAME_SPEC.md), [선택 기록](../archive/01_Choice/Notion_Answers_20261115.md), [후속 비교·보류 기록](../archive/02_Hold/Notion_Comparisons_20261115.md)을 같은 작업에서 갱신하고 질문·원문 대조 검수를 마침. 저장 계약의 최신 답변은 Q16·Q26·Q29에 연결함.
  - Unity 6.3 LTS는 Q23의 선택 조건 충족을 확인함. 도구 사용 범위는 GAME_SPEC 3.4와 Q21~Q23을 따르며 설치·프로젝트 생성은 미착수임.
  - [Notion 정리 페이지](https://app.notion.com/p/41857a6d6d24809a8d15f1a5aa34d48a)에 최신 통합 사양과 Q1~Q29 답변을 반영했고 모든 갱신 요청이 경고 없이 성공함. 로컬 링크·39개 정의 필드·29개 답변 상태를 검사함. 자동 동기화는 없음.
  - 완료 결과 : 문서 통합·현재 사양 방향 정리를 완료함. 프로젝트·패키지·실제 코드·기능/성능 검증은 미착수이며 게임 완성을 뜻하지 않음.

### 미완·보류 상태로 놓인 것들
- 후속 비교·연출 정의
  - 사용자가 남긴 검토 항목은 [후속 비교·보류 기록](../archive/02_Hold/Notion_Comparisons_20261115.md)을 따름. 미실행 비교를 성능 우위나 구현 완료로 기록하지 않음.

### 다음 행동
저장소 준비 확인(2026-11-15): 현재 `/project`와 Unity 설정 파일이 존재함. 아래의 프로젝트 부재·생성 미착수 기록은 이전 문서 작업 시점의 상태이며, 이후 개발 착수 시 기존 프로젝트를 먼저 확인하고 재생성하지 않음. 이번 요청은 Git 제외 설정과 커밋이며 게임 기능 구현·검증 결과를 갱신하지 않음.
이번 Player 시안 후속 요청은 `/art/player/idle_concepts_v2_20261115/`의01~06을 기준으로 반영한다. 정면 얼굴에 옆 귀/뒤통수가 보이는 현재 요청의 새 후보이며 최종 선택은 아직 없음. 기존4방향 정의와 `/art/player/idle_leftfront_redraw_02/`의 왼앞 후보10종은 별도 이력으로 보존한다. `/art/player/idle_leftfront_concepts_10/`은 사용자 반려 결과이므로 승인된 기준으로 사용하지 않는다. 픽셀 크기·알파·원본/PNG 일치 검사를 사용자 미술 승인으로 간주하지 않는다. 문서·코드 작업은 아래 기존 흐름을 따른다.
1. AGENTS → STATUS → PLANS를 읽고 GAME_SPEC의 확정 사양과 QUESTIONS Q1~Q29의 답변을 확인한다. 원문 재정리나 기존 질문의 재선택부터 시작하지 않는다.
2. 문서 작업은 완료됨. 신규 변경 요청이 있으면 GAME_SPEC 해당 정의·QUESTIONS 반영 이력·필요한 기록과 Notion을 함께 갱신한다.
3. 이번 후속 요청은 질문 압축·Profiler 비교 계획·완성 목표 기록이며 Unity 개발 시작 명령은 아니다. `/project` 부재는 의도된 미착수 상태이므로 이 문서 갱신만으로 프로젝트·패키지·코드를 생성하지 않는다.
4. 사용자가 개발 착수를 요청하면 GAME_SPEC의 개발 순서대로 Unity 6.3 LTS 프로젝트와 확정 도구를 준비한다. 정확한 설치 패치는 생성 시 공식 배포 목록으로 확인하며 기존 Q21~Q23 선택을 다시 묻지 않는다.
5. 각 Case의 실제 코드·호출/데이터 연결과 함께 `+`, `→`, `if :` 상세 흐름을 작성하고 컴파일·동작을 검증한다. 연출 상세가 미정이어도 비아트 로직 확인을 아트 의존으로 미루지 않는다.
6. 기능이 동작해 기준선이 확보되면 GAME_SPEC 3.1.1의 Profiler 비교를 수행하고 조건·수치·캡처·채택 이유를 기록한다. 비교 계획만으로 성능 개선이나 콘텐츠 추가 기반 완성을 보고하지 않는다.

### 세부 설명
Current art deliverable: six new front-idle alternatives in art/player/idle_concepts_v2_20261115, based on the direct request for frontal facial features plus visible side ear/rear hair. Native Aseprite/PNG round-trip and geometry checks passed. Independent visual review led to a two-pixel face margin correction in03, then re-export/recheck. User selection remains pending. These six static designs do not replace the prior four-direction game specification or authorize animation/Unity integration. Local documentation only.
The user rejected idle_leftfront_concepts_10 for incorrect angle and broken shapes; earlier visual sign-off was wrong. Ten candidates have been redrawn as complete connected silhouettes in art/player/idle_leftfront_redraw_02. Aseprite source/layer/native/PNG equality, 20px actual height, binary alpha, distinctness and connectivity passed technical checks. These checks do not establish artistic acceptance. New candidates are not user-approved. The four-direction requirement remains left-front/right-front/left-back/right-back; the other three directions, animation and Unity integration are unmade. Preserve rejected files as history only. This update is local.
Latest documentation update: Q1–Q29 are compressed into four category tables; GAME_SPEC 3.1.1 holds five core and two conditional Profiler comparison plans. The user explicitly requested recording a completion goal that leaves further work focused on Boss content and FX/VFX except detailed system tuning; the implementation-goal section was updated within that request. Notion received these edits successfully. No profiling or foundation implementation has run. Preserve unrelated local art work and its unpublished status.
Latest art: idle_revision_01, requested combination of concept 04 hair and 06 outfit. Actual height20px, head13px, symmetric outfit, separate left/right cloak layers. Aseprite native/PNG round-trip and geometry checks passed. Direction is user-selected; final appearance approval, animation and Unity integration remain outstanding. Preserve the original six concepts.
Six player front-idle design alternatives are complete under art/player/idle_concepts_20x20_20261115. Aseprite authored/exported every image; native and PNG pixels were checked after reopening. The master holds six alternatives, not an animation. GAME_SPEC 3.3 records the canonical brief and results. No final variant, other-direction animation, or Unity integration is complete. This art addition was recorded locally and has not been published to Notion; prior document-publication results below remain unchanged.
Emergency mode remains anchored to the original 2026-11-14 request; do not restart the 40-hour window. Q1–Q29 are answered, including the replacement save/backup/quit contract in Q29. GAME_SPEC is the canonical specification; answer integration and Notion publication are complete. All content-update calls succeeded without warnings; local links, definition fields, and 29 answer states were checked. Do not ask the same questions again. Unity 6.3 LTS meets the user's conditional choice. UniTask is limited to saves and title UI initialization; other waiting flows use coroutines. FMOD handles audio, and the camera remains custom. Documentation completion does not authorize starting development in this turn. No Unity project, package installation, code, compilation, play, build, or performance validation has begun. Detailed case sequences must accompany later code and verification. Preserve original source notes and the user-owned implementation-goal text. Reference evidence and untested comparisons are linked from GAME_SPEC; Notion is a manually updated review copy, not a background sync.


## 2. 맥락

### 구현 목표
- 사용자 요청에 따른 현재 목표(2026-11-15)
  - 현재 제출 범위와 플레이 완료 조건은 [GAME_SPEC의 제출 범위](../GAME_SPEC.md)를 따름.
  - **세부적인 시스템 조정을 제외하면 Boss 콘텐츠 추가와 FX·VFX 등 연출 추가에 집중할 수 있는 환경을 완성함.** 구체 기반 완료 조건은 [GAME_SPEC 상단 프로젝트 완성 목표](../GAME_SPEC.md)에 기록하며, 현재 목표를 달성한 상태는 아님.
  - 최적화 고민은 Unity Profiler 비교 결과로 선택 근거를 보강함. 후보·방법·미실행 상태는 GAME_SPEC 3.1.1에서 관리함.

### '프로젝트 관리' 관련 세부 요구
| 구분 | 세부 요구 사항 |
| --- | --- |
| 비상 운영 기준 | 2026-11-14 요청 시점에 제출까지 약 40시간. 정책 원본은 [AGENTS.md](../../AGENTS.md)의 '현재 최우선 방침' 및 '계획 문서 통합과 일괄 질문'. |
| 전환 이유·기록 | 반쪽짜리 계획 수준에서 확정 내용의 구현과 이후 결과 보고서 작성을 함께 가능하게 해야 함. [비상 운영 전환 결정](../archive/01_Choice/Emergency_Operation.md) 참조. |
| Notion 통합 방식 | 원문을 최대한 보존한 클래스 정의 → 케이스별 실제 구현과 동시 흐름 → 고민/결정. 현재 사양은 [GAME_SPEC](../GAME_SPEC.md), 질문은 [QUESTIONS](../QUESTIONS.md), 사용자 선택 이유는 [정리 방식 결정](../archive/01_Choice/Notion_Organization.md). |
| 문서·개발 착수 | 사용자 후속 확정: 문서가 완전히 끝나고 Sprite·FX·animation 연출을 제외한 로직 방향이 확실해진 뒤 개발 단계로 이동. 2026-11-15 별도 요청으로 Player 시안을 제작했으나 Unity 개발 시작 명령은 아직 없음. 프로젝트 부재는 의도된 미착수 상태. |
| 현재 사양 원본 | 제출 범위·구현 순서·도구·저장 규칙의 현재 원본은 [GAME_SPEC](../GAME_SPEC.md). 위 '구현 목표'는 2026-11-15 사용자의 명시적 목표 기록 요청 범위에서 갱신함. |

### '코드 작성 및 구조' 관련 세부 요구
| 구분 | 세부 요구 사항 |
| --- | --- |
| 수행·완료 기준 | [AGENTS.md](../../AGENTS.md)의 'Unity 코드 작업'을 따름. 기존 `.cs`만 수정·컴파일만 확인 규칙은 비상 운영의 단계별 범위로 대체됨. |
| 레퍼런스 적용 | AGENTS의 '레퍼런스 코드 활용'을 따름. 현재 `/docs/reference/`의 Dungreed·Sephiria 추출 코드가 존재하며, 읽은 파일·책임 분리 제안·실행 미검증 범위는 GAME_SPEC 3.5에 기록함. |
| 클래스 정의와 흐름 | class/struct/enum마다 역할·수명·공개 API/변수·연관 class를 정리함. 수명/관계는 원문과 확정 답변을 기준으로 작성. 흐름은 `+`, `→`, `if :`로 표현하며 실제 구현과 함께 작성. |
| 최적화 비교와 근거 | 기능 동등성을 유지하는 대안을 Unity Profiler로 비교해 더 나은 성능이 확인된 안을 선택하고 프로젝트 결과의 논리를 강화함. 후보·동일 조건·채택 기준은 GAME_SPEC 3.1.1, 실제 결과는 측정 후 기록함. |
| 명명·공개 범위 | Q4 사용자 확정: 새 기능에 필요한 이름은 AI가 정의하고 사용자가 이후 변경할 수 있음. Try보다 Apply/Handle/Get/Set을 선호하며 public/static을 간단히 사용하고 불필요한 readonly/sealed는 붙이지 않음. 확정한 기존 역할 이름은 유지함. |


### '아트 및 디자인' 관련 세부 요구
| 구분 | 세부 요구 사항 |
| --- | --- |
| 단계 연결 | [AGENTS.md](../../AGENTS.md)의 '단계별 수행 범위'와 '코드 단계 완료 기준'을 따름. 실제 자산 의존 조정만 Sprite 단계에 남기고 나머지 확정 기능은 코드 단계에서 완료함. |
| Player 시안 | Aseprite로 정면 Idle 5종 이상 제작 요청에 따라 6종 제작 완료. 규격·이유 원본은 [GAME_SPEC 3.3](../GAME_SPEC.md) 및 [요청 기록](../archive/01_Choice/Player_Idle_Concept_Brief_20261115.md). 시안 제작과 최종 디자인 확정을 구분함. |

