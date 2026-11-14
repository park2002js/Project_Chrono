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
  - 2026-11-14 (Asia/Seoul)
- 마지막으로 진행한 과제
  - 제출 전 비상 운영 전환 : 행동 방침 및 인수인계 문서 갱신
  - 정책 갱신 완료. 실제 기획 통합·코드 작성·Sprite 추가·디버깅은 이번 작업에서 수행하지 않음.
  - 완료 기준 : 새 에이전트가 시간 제약, 4단계 순서, 일괄 질문 절차, 구현·검증 범위를 읽고 다음 작업을 시작할 수 있음.

### 미완·보류 상태로 놓인 것들
- Unity Editor 버전 선택
  - 기존 진행 항목. 필요한 Plugin·asset 및 지원 버전 검토 후 결정하도록 순서를 조정하여 보류함. 버전 선택·설치·이전은 이번 작업에서 수행하지 않음.

### 다음 행동
1. 문서 통합 작업을 시작할 때 AGENTS.md의 비상 방침과 PLANS.md의 1단계를 읽는다.
2. `/docs/notes`, 관련 결정 기록, 현재 `/project` 코드에서 컨셉·시스템·구조·필요 아트의 출처를 확인하고 `/docs/GAME_SPEC.md` 초안을 구성한다. 이 통합 문서는 아직 작성되지 않음.
3. 충돌과 필수 누락을 `/docs/QUESTIONS.md`에 묶어 채팅에도 제시한다. 기존 '구현 목표'의 튜토리얼·첫 보스와 실제 제출 범위의 관계도 필요한 경우 함께 확인한다. 질문지는 아직 작성되지 않음.
4. 확정 기능에 필요한 Plugin·asset 후보와 Unity 호환 정보를 비교한다. 사용자 답변을 반영한 뒤 기능별 구현 작업으로 이어간다. 미확정 결정과 무관한 작업은 계속할 수 있음.

### 세부 설명
Emergency mode was requested on 2026-11-14 with about 40 hours remaining at that request; do not restart this window in a new session. No exact deadline was supplied. This task updated operating instructions only. No game design reconciliation, code/art changes, tool selection, or Unity validation was performed. AGENTS.md defines stage-scoped implementation and iterative debugging permissions. Existing content under "구현 목표" is preserved, not newly confirmed. `/docs/reference/` is absent in the current checkout; verify availability when a reference task starts and do not claim to have inspected missing code.


## 2. 맥락

### 구현 목표
- 최종 목표
  - 기본 조작을 배우는 튜토리얼과 첫 번째 보스 구간을 예상한다. 조작감과 전체적인 아트 분위기를 전달하는 것이 목표다.
  - 최대한 이후 개발을 위해 컨텐츠만 추가하면 되는 

### '프로젝트 관리' 관련 세부 요구
| 구분 | 세부 요구 사항 |
| --- | --- |
| 비상 운영 기준 | 2026-11-14 요청 시점에 제출까지 약 40시간. 정책 원본은 [AGENTS.md](../../AGENTS.md)의 '현재 최우선 방침' 및 '계획 문서 통합과 일괄 질문'. |
| 전환 이유·기록 | 반쪽짜리 계획 수준에서 확정 내용의 구현과 이후 결과 보고서 작성을 함께 가능하게 해야 함. [비상 운영 전환 결정](../archive/01_Choice/Emergency_Operation.md) 참조. |

### '코드 작성 및 구조' 관련 세부 요구
| 구분 | 세부 요구 사항 |
| --- | --- |
| 수행·완료 기준 | [AGENTS.md](../../AGENTS.md)의 'Unity 코드 작업'을 따름. 기존 `.cs`만 수정·컴파일만 확인 규칙은 비상 운영의 단계별 범위로 대체됨. |
| 레퍼런스 적용 | 같은 문서의 '레퍼런스 코드 활용'을 따름. 현재 `/docs/reference/` 부재는 참고 작업 시 확인할 환경 정보이며 이번 정책 갱신의 차단 사유는 아님. |


### '아트 및 디자인' 관련 세부 요구
| 구분 | 세부 요구 사항 |
| --- | --- |
| 단계 연결 | [AGENTS.md](../../AGENTS.md)의 '단계별 수행 범위'와 '코드 단계 완료 기준'을 따름. 실제 자산 의존 조정만 Sprite 단계에 남기고 나머지 확정 기능은 코드 단계에서 완료함. |

