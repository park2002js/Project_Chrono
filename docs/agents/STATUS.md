# Project_Chrono 현재 상태 및 맥락

## 1. 현재 상황

### 마지막 갱신

- 날짜: 2026-11-16 (Asia/Seoul)
- 후속 완료: 평가용 제출 폴더의 `README.md`에도 실제 Scripts4폴더·C#53개 전체 트리와 파일별 역할, 씬·프리팹 연결 및 코드 읽는 시작점을 추가했다. 기존 본문은 보존하고 README 내부 링크23개·실제 파일 목록 일치를 확인했다
- 추가 완료: 루트 [README](../../README.md)의 게임 코드 구조에 실제 Scripts4폴더·C#53개와 폴더별 코드 목적을 표시했다. 코드·폴더 배치는 변경하지 않았다
- 마지막 과제: 평가용 제출 폴더의 핵심 문서 선별·Notion 기획 Markdown·README 작성
- 결과: `D:/00_Works/02_GameDev/Unity/temp/2026_Capstone_Project_Chrono`에 README·핵심 사양·결과·Notion 원문·외부 패키지 목록·README 사례 비교를 반영했다. 신규35개·기존3개 갱신이며 게임 프로젝트·실행 파일·기존 아트는 유지했다
- 문서 구성: `개발 문서/00_결과`, `03_기획 및 설계`, `04_외부 도구 및 참고` 및 루트/개발 문서 README. Notion4페이지·참고 이미지14개, GitHub13링크 추출·README5개 비교, 패키지 직접53·전이13개 기록
- 확인 자료: [제출 문서 확인](../results/Submission_Documents_20261116.json)에 실제 배치38파일 해시·Markdown34개 내부 링크615개·문서521파일 목록 일치 결과를 남겼다. 개발·빌드 확인은 기존 [검증 요약](../results/Verification_Summary.json)을 따른다
- 원본 보존: 이번 요청은 선별 복사이며 원본 저장소·Notion은 유지했다. 앞선 불필요 자료 삭제의 범위와 복구 한계는 [정리 결과](../results/Cleanup_Result.json) 및 기존 정리 결정 기록을 따른다

### 현재 결과

| 영역 | 최종 정리 기준 |
| --- | --- |
| 현재 사양·구조 | [GAME_SPEC](../GAME_SPEC.md), Q1~Q29 답변 및 이후 사용자 변경 반영 |
| 런타임 | F-S01·F-P01~P11, 보스 공통·경비5패턴·입장 R03·임시 데모 엔딩의 구현·격리 검사 기록. 제출 정리 후 C#53/Editor0 |
| 아트·음향 | [아트 카탈로그](../../art/archive/README.md)의 현행 원본/출력/연결·승인 상태. 군용은 정적 배치이며 전투 미구현 |
| Windows 빌드 | 2026-11-16 14:44:03~14:46:09 KST 성공. 최종 시각·출력·확인 범위를 [검증 요약](../results/Verification_Summary.json)에 남기고 원시 로그는 삭제. 전체 실행 파일 시연은 별도 |
| 보고서·포스터 | [제출물 안내](../report/README.md). 최종 보고서는21쪽 DOCX·32211853_결과보고서.pdf, 포스터는PPTX·PDF |
| 평가용 문서·README | 제출 폴더에 핵심 문서·Notion 컨셉 원문·실행/폴더/플러그인 안내를 배치. [선정·반영 기록](../archive/01_Choice/Evaluator_Documents_20261116.md) |
| 결정·분석 근거 | [기록 보관소](../archive/README.md). 과거 버전의 검사와 현재 결과를 구분 |

당시 빌드 출력은 `D:/00_Works/02_GameDev/Unity/temp/2026_Capstone_Project_Chrono/program/Project_Chrono.exe`다. 직전 대화의 `project/Builds/Windows/Project_Chrono.exe`는 제안 경로이며 실제 생성 경로로 기록하지 않는다. 최종 문서 정리에서 새 빌드를 실행하지 않았다.

### 미완·보류 상태로 놓인 것들

- 전체 실행 파일의 연속 플레이, 모든 보스 패턴·사망/재도전·Title 복귀·엔딩을 한 실행에서 확인한 최종 회귀는 미확인이다. 기능별 격리 검사 수를 합산해 대체하지 않는다
- 설정 화면·키 재설정/저장, 전체 안내 튜토리얼·대사, 나머지 보스 전투, BGM·전체 오디오 구성은 후속 범위다. 임시 데모 엔딩은 사용자 요청으로 정식 스탭롤을 대체했다
- 실제 연결한 FX의 최종 미술·청감 판단과 군용의 추가 전투 구현은 [아트 카탈로그](../../art/archive/README.md)를 따른다. 미확정 조작 안내 시안은 이번 파일 정리에서 제외했다
- Profiler A/B 비교·FPS 성능 측정·사용자 평가는 미실행이다. 최적화 구조 설명과 실측 우위를 구분한다
- 현재 맵 기준의 전체 회귀는 미확인이다. 기능별 검사와6개 스모크의 성공 범위만 최종 요약에 남겼으며, 오래된 실패 로그·검사 사본은 삭제했다

### 다음 행동

평가자는 제출 폴더의 `README.md` → `개발 문서/README.md` → 결과보고서·개발 결과 순서로 읽을 수 있다. 이번 문서 배치 요청은 완료했으며 새 빌드·게임 검사·GitHub 업로드는 수행하지 않았다. Notion 사본은 자동 동기화되지 않는다. 이후 수정 요청 시 [선정·반영 기록](../archive/01_Choice/Evaluator_Documents_20261116.md)의 대상 경로에서 이어가고, 게임 개발 재개는 위 남은 항목 중 요청한 범위만 수행한다.

### 세부 설명

Evaluator documentation is deployed to the sibling temp/2026_Capstone_Project_Chrono folder: 35 new files and 3 updates. Preserve source docs, Notion, project/program and existing art. The root README describes the one-boss demo, controls, actual folders and dependencies. Four Notion planning pages retain their text; 14 images use prior local exports because fresh download DNS resolution failed. Five READMEs were read from 13 school-listed GitHub links. All 615 local links and 521 document hashes passed. No new game/build validation or publication was performed. See Submission_Documents_20261116.json and the evaluator-document choice record.


## 2. 맥락

### 구현 목표
- 사용자 요청에 따른 현재 목표(2026-11-15)
  - 현재 제출 범위와 플레이 완료 조건은 [GAME_SPEC의 제출 범위](../GAME_SPEC.md)를 따름.
  - **세부적인 시스템 조정을 제외하면 Boss 콘텐츠 추가와 FX·VFX 등 연출 추가에 집중할 수 있는 환경을 완성함.** 구체 기반 완료 조건은 [GAME_SPEC 상단 프로젝트 완성 목표](../GAME_SPEC.md)에 기록하며, 현재 목표를 달성한 상태는 아님.
  - 최적화 고민은 Unity Profiler 비교 결과로 선택 근거를 보강함. 후보·방법·미실행 상태는 GAME_SPEC 3.1.1에서 관리함.

### '프로젝트 관리' 관련 세부 요구
| 구분 | 세부 요구 사항 |
| --- | --- |
| 최종 문서 정리 | 2026-11-16 사용자 요청: project를 제외한 자료를 결과로 종합한 뒤, 의미 없는 구버전·미확정/일회성 자료·지난 오류·로그를 실제 삭제. 앞선 과정 자료 보존 방식을 이 후속 요청으로 대체하고 확정 결과·편집 원본·결정 이유·검증 한계만 유지 |
| 평가용 문서 배치 | 2026-11-16 후속 요청: 지정 제출 폴더에 주요 문서와 Notion 컨셉 기획 Markdown을 선별하고, 학교 졸업작품 GitHub README의 장단점을 조사해 첫인상·폴더 역할·사용 외부 플러그인을 설명하는 README 작성. [반영 기록](../archive/01_Choice/Evaluator_Documents_20261116.md) |
| 비상 운영 기준 | 2026-11-14 요청 시점에 제출까지 약 40시간. 정책 원본은 [AGENTS.md](../../AGENTS.md)의 '현재 최우선 방침' 및 '계획 문서 통합과 일괄 질문'. |
| 전환 이유·기록 | 반쪽짜리 계획 수준에서 확정 내용의 구현과 이후 결과 보고서 작성을 함께 가능하게 해야 함. [비상 운영 전환 결정](../archive/01_Choice/Emergency_Operation.md) 참조. |
| Notion 통합 방식 | 원문을 최대한 보존한 클래스 정의 → 케이스별 실제 구현과 동시 흐름 → 고민/결정. 현재 사양은 [GAME_SPEC](../GAME_SPEC.md), 질문은 [QUESTIONS](../QUESTIONS.md), 사용자 선택 이유는 [정리 방식 결정](../archive/01_Choice/Notion_Organization.md). |
| 코드별 시퀀스 동시 기록 | 기능별 `F-## SequenceDiagram` 토글·번호Case·설명·Mermaid 저장 유지.2026-11-15 최신 요청으로 미리보기 모드 설정·화면 확인·이를 위한 마우스 조작을 절차와 완료 체크에서 제외함. 실제 형식·범위는 AGENTS 3·4와 DESIGN 4.1을 따름. |
| 문서·개발 착수 | 문서 완료 후 2026-11-15 사용자가 F-S01 Title 준비·슬롯 로드·MainWorld 초기화를 직접 요청하여 코드 단계에 착수함. 기존 프로젝트를 사용하고 요청한 Case의 구현·연결·검증을 진행함. |
| 현재 사양 원본 | 제출 범위·구현 순서·도구·저장 규칙의 현재 원본은 [GAME_SPEC](../GAME_SPEC.md). 위 '구현 목표'는 2026-11-15 사용자의 명시적 목표 기록 요청 범위에서 갱신함. |

### '코드 작성 및 구조' 관련 세부 요구
| 구분 | 세부 요구 사항 |
| --- | --- |
| 프레임률과 재생 시간 | 2026-11-16 사용자 요구: 제작 기준보다 높은 FPS 환경에서도 공격/VFX 속도가 달라지지 않도록 한다. 현재 공격 제작 설명은60fps 타임라인, 실행은 경과 시간 기준으로 처리한다. 적용/검증 기준과 미구현 범위는 GAME_SPEC3.3.8을 따름. |
| 수행·완료 기준 | [AGENTS.md](../../AGENTS.md)의 'Unity 코드 작업'을 따름. 기존 `.cs`만 수정·컴파일만 확인 규칙은 비상 운영의 단계별 범위로 대체됨. |
| 레퍼런스 적용 | 이후 클래스·기능을 먼저 특정하고 [REFERENCE_CODE_MAP.md](../REFERENCE_CODE_MAP.md)의 Sephiria·필요한 Dungreed 실제 코드를 읽고 구조/호출/초기화를 파악한 뒤 현재 규모에 맞춰 적용함. Instance·MonoBehaviour는 준비 전 요청·중복/교체/씬 해제를 검사함. AGENTS '레퍼런스 코드 활용'·DESIGN 2.4를 따름. |
| 고정 참조 초기화·재사용 | 2026-11-15 후속 요청: 유지 범위·생성 우선순위·참조 시점을 정하고 Awake/Start 또는 명시적 완료 순서로 한 번 확보해 재사용. 반복 호출부의 같은 조회·생성을 검사하고 실제 교체 경계에서만 참조 갱신. 상태값·일회 생성/해제 조회는 구분하며 원본은 AGENTS·DESIGN 2.4. |
| 클래스 정의와 흐름 | class/struct/enum마다 역할·수명·공개 API/변수·연관 class를 정리함. 수명/관계는 원문과 확정 답변을 기준으로 작성. 흐름은 `+`, `→`, `if :`로 표현하며 실제 구현과 함께 작성. |
| 최적화 비교와 근거 | 기능 동등성을 유지하는 대안을 Unity Profiler로 비교해 더 나은 성능이 확인된 안을 선택하고 프로젝트 결과의 논리를 강화함. 후보·동일 조건·채택 기준은 GAME_SPEC 3.1.1, 실제 결과는 측정 후 기록함. |
| 명명·공개 범위 | Q4 사용자 확정: 새 기능에 필요한 이름은 AI가 정의하고 사용자가 이후 변경할 수 있음. Try보다 Apply/Handle/Get/Set을 선호하며 public/static을 간단히 사용하고 불필요한 readonly/sealed는 붙이지 않음. 확정한 기존 역할 이름은 유지함. |
| namespace 미사용 | 2026-11-15 사용자 요청: 기존 프로젝트 코드의 namespace를 모두 제거하고 앞으로도 선언하지 않음. 원본 규칙은 AGENTS의 Unity 코드 작업 및 DESIGN 2.1, 이유·이력은 [선택 기록](../archive/01_Choice/No_Namespace_20261115.md)을 따름. |
| 주석 형식과 목적 설명 | 2026-11-15 최신 요청: public·private 변수 옆 역할 설명, 함수의 목적별 구역 상단 요약, 구독/해제·코루틴 실행·불명확한 yield 대기 옆 목적 주석. 단일 문장은 `대기`, `복귀`처럼 끝내고 마침표 생략, 두 문장 이상을 구분할 때만 `~함.` 사용. yield break/null 제외. 원본은 AGENTS·DESIGN 2.2, 경위는 [선택 기록](../archive/01_Choice/Public_Variable_Comments_20261115.md). |
| UI 네이밍 | 2026-11-15 사용자 요청: 특정 화면·요소는 `UI_` 접두사, 공통 UI 시스템·기반·헬퍼는 밑줄 없이 `UI` 접두사로 구분함. 이후 코드에도 AGENTS 및 DESIGN 2.1의 규칙을 적용함. |
| 부팅·Title·저장 책임 | 2026-11-15 사용자 요청: GameManager는 UI_Title을 모르며 공통 저장 로드와 씬 전환만 주도함. Title은 자체 준비·입력을 담당하며 외부 저장은 `SaveManager.Current.Set` 후 `Save()` 요청으로 끝남. 현재 수명·API는 GAME_SPEC U03·S01·S05, 경위는 [선택 기록](../archive/01_Choice/Title_Save_UI_Responsibilities_20261115.md)을 따름. |
| SaveData 단순화 | 2026-11-15 사용자 요청: 호출자가 key와 타입을 알고 있다고 가정하고 세피리아의 저장 구조를 적용함. 게임별 key·타입 검증 계층을 추가하지 않음. 현재 구현·파일 처리 규칙은 GAME_SPEC S05·S06, 선택 이유는 [선택 기록](../archive/01_Choice/SaveData_Simplification_20261115.md)을 따름. |
| SaveData 저장 위치·키 | 2026-11-15 후속 요청: 문서/Project_Chrono와 ProjectChrono 기준 키를 사용함. 실제 관리자 레퍼런스는 Sephiria SaveManager이며 현재 경로 제공·폴더 생성 책임과 파일 계약은 GAME_SPEC S06·2.4, 선택 경위는 위 SaveData 선택 기록을 따름. |


### '아트 및 디자인' 관련 세부 요구
| 구분 | 세부 요구 사항 |
| --- | --- |
| 그림 겹침과 가림 | 2026-11-16 사용자 설명: 각 원본 그림 전체를 유지하고 나중에 올린 앞그림이 겹친 부분만 가리게 한다. 겹치지 않은 부분은 모두 보이며,가림을 표현하려고 원본을 반쪽으로 자르지 않는다. 특정 프레임의 전체 숨김은 사용자 지정 예외로 따로 처리한다. [선택17](../archive/01_Choice/Player_Attack_Sephiria_20261115.md) 참조. |
| 아트 보관·Git 제외 | 완성 결과물의 편집 원본과 필요한 출력물은 `/art/archive`에 대상별로 분류해 저장하고, 다른 하위 폴더의 작업 자료는 로컬에서 유지함. 원본 방침은 [AGENTS.md](../../AGENTS.md), 경위는 [선택 기록](../archive/01_Choice/Art_Archive_Git_20261115.md). |
| Title 로고 최종 원본 | 사용자 직접 수정한 최종 Aseprite를 기준으로 후속 수정하며 옛 스크립트로 재생성하지 않음. 로고 최종 선택·정리는 완료했고 현재 원본·검사·남은 연결 작업은 [GAME_SPEC 3.3](../GAME_SPEC.md)을 따름. |
| 단계 연결 | [AGENTS.md](../../AGENTS.md)의 '단계별 수행 범위'와 '코드 단계 완료 기준'을 따름. 실제 자산 의존 조정만 Sprite 단계에 남기고 나머지 확정 기능은 코드 단계에서 완료함. |
| Player 기본 디자인·Idle·Move | 정적 default 선택 이후 최신 사용자 개별 Idle 왼쪽 원본을 보존하고 오른쪽에 반영함.12장 레퍼런스의 발 구르기를 줄인6단계 Move를 별도 제작함. 사양·검사는 [GAME_SPEC 3.3](../GAME_SPEC.md), 선택 경위는 [요청 기록](../archive/01_Choice/Player_Idle_Concept_Brief_20261115.md). 사용자 미술 판단·다른 애니메이션·Unity 적용은 별도임. |

