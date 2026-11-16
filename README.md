# Project_Chrono

DKU 2026 캡스톤 디자인을 위한 싱글 플레이 보스 전투 게임이다. 고정 무기와 Draw & Slash를 중심으로 경비 로봇의 다섯 패턴을 상대하는 데모를 제작했다.

## 최종 자료

| 읽을 내용 | 위치 |
| --- | --- |
| 개발 결과·문제 해결·검증과 한계 | [개발 결과 종합](docs/results/Development_Results.md) |
| 제출 보고서·포스터 및 편집 원본 | [제출물 안내](docs/report/README.md) |
| 전체 문서·자료 찾아보기 | [문서 안내](docs/README.md) · [파일별 색인](docs/results/File_Index.md) |
| 현재 게임 사양·기능별 구현과 흐름 | [GAME_SPEC](docs/GAME_SPEC.md) |
| 완성 아트·음향 원본과 출력 | [아트 카탈로그](art/archive/README.md) |
| 결정 이유·원작 분석·제작 과정 근거 | [기록 보관소](docs/archive/README.md) |

Windows64 Player 빌드는 2026-11-16 14:46 KST 성공했다. 당시 출력은 `D:/00_Works/02_GameDev/Unity/temp/2026_Capstone_Project_Chrono/program/Project_Chrono.exe`이며 [최종 확인 요약](docs/results/Verification_Summary.json)에 결과를 남겼다. 실행 파일의 전체 플레이 시연·성능 비교·사용자 평가는 이 빌드 성공과 구분한다.

## 폴더 역할

| 폴더 | 보관 내용 |
| --- | --- |
| `project/` | 실제 Unity 프로젝트 |
| `docs/results/` | 중간 기록을 종합한 결과, 파일 색인, 정리 검증 목록 |
| `docs/report/` | 제출 PDF·DOCX·포스터와 해당 검증 근거 |
| `docs/archive/` | 의미 있는 결정 이유·기술 분석과 후속 검토 사항 |
| `docs/notes/` | 개발자가 작성한 초기 기획·Notion 내보내기 원문 |
| `docs/reference/` | 재사용할 게임 레퍼런스 코드·원본, Git 제외 |
| `docs/agents/` | 현재 인수인계·계획·작성 기준 |
| `art/archive/` | 완성 아트의 편집 원본·게임용 출력·결과 확인 자료 |

### 게임 코드 구조

`project/Assets/Scripts`에는 런타임 C# 파일53개가 있으며, 각 폴더 안에 추가 하위 폴더는 없다.

```text
project/
└─ Assets/
   └─ Scripts/
      ├─ Player/                 # 16개 — 캐릭터 기반과 플레이어 동작
      │  # 입력·이동·대시·일반공격·Draw/Slash
      │  # 캐릭터 수치·피격·효과·애니메이션·음향·각성 표현
      │
      ├─ Enemy/                  # 14개 — 보스와 적 공격
      │  # 보스 생성·조우·패턴 선택/실행·부위 관리
      │  # 경비의 손·레이저 공격, 탄환·반사·공격 예고
      │
      ├─ Systems/                # 18개 — 게임 진행과 공통 기능
      │  # 게임·스테이지 초기화, 저장·불러오기
      │  # 맵·문·체크포인트·카메라·컷씬·입장·데모 엔딩
      │  # 피해 전달·공간 판정, Draw/Slash 공통 시각 효과
      │
      └─ UI/                     # 5개 — 화면과 전투 정보 표시
         # Title·저장 슬롯, 로딩, Draw 안내, 사망 화면
         # 플레이어·보스 체력과 스킬 상태를 표시하는 HUD
```

`Player`에는 보스도 함께 사용하는 `Character`·`CharacterBuff`가 있으며, `Systems`에는 `CollisionBox`·`Hitbox` 같은 전투 판정 코드도 포함된다.

2026-11-16 최종 정리에서 `project`를 제외하고 구버전·미확정 시안·임시 시험본·캐시·지난 로그를 삭제했다. 확정 편집 원본·현재 출력·결정 이유·최종 확인 요약을 남겼으며, 미완·미검증 범위는 결과 문서에서 구분한다. [삭제·보존 확인](docs/results/Cleanup_Result.json). AI 에이전트는 [AGENTS.md](AGENTS.md)를 먼저 따른다.
