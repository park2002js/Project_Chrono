# Project_Chrono

DKU 2026 캡스톤 디자인 작품. 고정된 검으로 싸우며, 시간을 멈춰 선을 그리는 **Draw**와 그 선을 따라 베는 **Slash**를 사용하는 2D 탑다운 싱글 플레이 보스 러시다. **2026-11-27 개발을 마무리했으며, 이 저장소는 최종 결과를 보존한다.**

## 최종 결과

- 흐름: Title·저장 슬롯 → 시작 통로에서 기상 → 경비 로봇 → 어둠의 문 → 최종 보스 → 빛의 문 → 데모 종료 문구 → Title
- 플레이어: 이동·대시·3연격·Draw & Slash·각성, 체크포인트와 부활
- 보스: 경비 로봇(패턴 2~7), 최종 보스(패턴 1·2·3·5·6·7), 그로기와 카운터
- 화면·진행: HUD, 설정 창, 3슬롯 저장, 맵 전환, 배경음과 효과음
- 최종본에 없는 것: 튜토리얼 단발 패턴과 조작 안내, 대사, 키 재지정, 스탭롤

## 실행과 조작

배포 빌드는 [program/Draw_Slash.zip](program/Draw_Slash.zip)을 풀거나 [program](program/)의 `Project_Chrono.exe`를 실행한다(Windows 10/11, 키보드·마우스). 이 빌드는 2026-11-27 10:28에 만들었으며, 이후 소스에 넣은 시연용 패턴 순환은 들어 있지 않다.

| 상황 | 키보드·마우스 |
| --- | --- |
| 전투 | WASD 또는 방향키로 이동하고 마우스로 조준한다. 왼쪽 클릭 또는 Enter로 일반 공격, Space로 대시한다. |
| Draw | Shift로 진입하고 왼쪽 클릭으로 두 점을 찍은 뒤 F로 선을 확정한다. R로 점을 지우거나 Shift로 취소한다. |
| Slash·상호작용 | 일반 상태에서 F로 Slash를 사용한다. 자수정 근처에서는 F로 상호작용한다. |

### 소스에서 실행

Unity Hub에서 [project](project/)를 열고 Unity **6000.3.21f1**을 사용한다. [Title 씬](project/Assets/Scenes/Title.unity)에서 Play를 누른 뒤 **시작 → 저장 슬롯**을 선택하면 [MainWorld 씬](project/Assets/Scenes/MainWorld.unity)으로 이동한다. 시연용 설정으로 두 보스는 모든 패턴을 한 번씩 돌아가며 쓴다([BossPatternSelector.cs](project/Assets/Scripts/Enemy/BossPatternSelector.cs)의 `CycleAllPatterns`).

## 제출 자료

| 자료 | 제출본 | 편집 원본 |
| --- | --- | --- |
| 결과보고서 | [PDF](docs/report/32211853_결과보고서.pdf) | [Word](docs/report/Project_Chrono_Report.docx) |
| 포스터 | [PDF](<docs/report/32211853 박지성_포스터.pdf>) | [PowerPoint](docs/report/포스터.pptx) |

## 저장소 구성

| 위치 | 내용 |
| --- | --- |
| [게임 사양](docs/GAME_SPEC.md) | 최종 게임 동작, 수치, 담당 코드, 확인 범위 |
| [Sequence Diagram](docs/SEQUENCE_DIAGRAMS.md) | 기능별 호출 흐름 |
| [기획 원문](docs/planning/notes/) | 게임 계획서, 핵심 기획, Notion 내보내기 |
| 패턴 원문 | [경비 로봇](docs/planning/patterns/guard-robot.md) · [최종 보스](docs/planning/patterns/final-boss.md) |
| [project](project/) | Unity 6000.3.21f1 프로젝트 |
| [program](program/) | Windows 빌드와 압축본 |
| [art/archive](art/archive/) | 그림·음원 원본 |

기획 원문은 작성 당시 내용이라 최종 결과와 다를 수 있다. 현재 동작은 게임 사양이 기준이다.
