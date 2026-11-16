# Project_Chrono 최종 보고서·포스터

2026-11-16 문서 정리 기준이다. 제출용 파일과 편집 원본을 이 폴더에 함께 보관하며, 개발 과정의 상세 결과는 [개발 결과 종합](../results/Development_Results.md)과 [현재 사양](../GAME_SPEC.md)에서 확인한다.

## 최종 산출물

| 용도 | 파일 | 확인 상태 |
| --- | --- | --- |
| 결과보고서 제출본 | [32211853_결과보고서.pdf](32211853_결과보고서.pdf) | 최종 검증 기록의 PDF와 SHA256 일치 |
| 결과보고서 편집 원본 | [Project_Chrono_Report.docx](Project_Chrono_Report.docx) | 최종 검증 기록의 DOCX와 SHA256 일치 |
| 포스터 제출본 | [32211853 박지성_포스터.pdf](<32211853 박지성_포스터.pdf>) | 현존 제출 파일 보존 |
| 포스터 편집 원본 | [포스터.pptx](포스터.pptx) | 현존 편집 파일 보존 |

보고서 현행본은 21쪽이다. [보고서 검증 기록](evidence/ReportVerification_20261116.json)에 표 11개·본문 이미지 1개·UniTask 코드 4개·물리 판정 코드 2개·Draw 코드 1개, DOCX 렌더와 PDF 본문 일치, 전체 페이지 검토, 글꼴 포함과 대체 문자 0개가 기록돼 있다. 이번 정리에서는 원본 문서를 다시 편집하거나 렌더하지 않았으며, 기존 기록과 파일 해시를 대조했다.

검증 JSON의 `Project_Chrono_Report.pdf`는 검증 당시 파일명이다. 현재 제출명 `32211853_결과보고서.pdf`의 크기 983,137 bytes와 SHA256 `d9d1b9e7d2370e5ff8bb6703b45ef0797bd9568b9e85905bdaedea1f61860322`가 해당 기록과 일치한다. DOCX의 SHA256은 `ce5aed7af50397429e938f068f62b2483292cd87a30a95b058a02cff7fe0c33a`다. 포스터의 페이지·시각 검토까지 같은 보고서 검증에 포함된 것으로 해석하지 않는다.

## 보조 자료

| 위치 | 내용과 사용 범위 |
| --- | --- |
| [figures](figures/) | Title·저장 슬롯·입장 인계·경비 레이저의 설명용 화면 4장. 모두가 현행 보고서 본문에 삽입된 것은 아님 |
| [ReportVerification_20261116.json](evidence/ReportVerification_20261116.json) | 최종 보고서의 문구·페이지·본문 일치·글꼴·파일 해시 확인 결과 |
| [최종 확인 요약](../results/Verification_Summary.json) | Windows 빌드 성공 시각·출력 위치와 확인 한계. 원시 오류 로그는 최종 정리에서 삭제 |
| [보고서 작성 결정 기록](../archive/01_Choice/Graduation_Report_20261116.md) | 42쪽 초안에서 축약하고 사용자 주석·기술 설명을 반영한 선택과 이유 |

빌드 로그의 최종 실행 파일 출력 위치는 `D:/00_Works/02_GameDev/Unity/temp/2026_Capstone_Project_Chrono/program/Project_Chrono.exe`다. 이 경로는 당시 로그가 증명하는 위치이며, 이번 문서 정리에서 실행 파일을 이동하거나 게임 프로젝트를 변경하지 않았다.
