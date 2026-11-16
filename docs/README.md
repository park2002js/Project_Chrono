# 문서 안내

2026-11-16 정리본. 개발 결과를 먼저 읽고 필요한 사양·확정 원본·결정 이유를 찾도록 구성했다. 일회성 자료·이전 판본·지난 원시 로그는 사용자 요청으로 삭제했다.

## 결과와 제출물

| 문서 | 역할 |
| --- | --- |
| [개발 결과 종합](results/Development_Results.md) | 완성한 기능과 아트, 문제의 원인·수정·최종 확인, 남은 한계 |
| [제출물 안내](report/README.md) | 최종 보고서·포스터·편집 원본과 검증 자료 |
| [평가용 문서 배치](archive/01_Choice/Evaluator_Documents_20261116.md) | 별도 제출 폴더의 README·Notion 기획·핵심 문서 선정과 배치 결과 |
| [전체 파일 색인](results/File_Index.md) | 결정·분석 문서와 보관 자료를 주제별로 찾아보기 |
| [아트 카탈로그](../art/archive/README.md) | 현재 사용할 원본·출력과 게임 연결·승인 상태 |
| [구조도 안내](diagrams/README.md) | Character 관계 도면과 전체 구조 도면의 표현 범위 |

## 원본과 근거

| 위치 | 읽는 기준 |
| --- | --- |
| [GAME_SPEC](GAME_SPEC.md) | 현재 사양과 실제 클래스·Case 흐름의 단일 기준. 하위 항목의 개정 이력은 당시 범위 |
| [질문·답변](QUESTIONS.md) | Q1~Q29의 확정 답변과 반영 위치. 이후 변경은 GAME_SPEC·결정 기록을 함께 확인 |
| [레퍼런스 코드 대응표](REFERENCE_CODE_MAP.md) | 채택한 구조와 원작 코드 경로·차이 |
| [기획 원문](notes/README.md) | 개발자 메모·Notion 원문. 이후 확정 사양과 구분 |
| [결정·보류·분석](archive/README.md) | 선택 이유, 미실행 비교, 분석의 실제 적용 결과와 이력 |
| [최종 확인 요약](results/Verification_Summary.json) | Windows 빌드·제출 소스 정리·보고서의 최종 확인 결과와 한계 |
| [삭제·보존 확인](results/Cleanup_Result.json) | 실제 삭제 규모, project·확정 원본 보존과 문서 링크 확인 |

## 현재 상태와 작성 기준

[STATUS](agents/STATUS.md)는 현재 결과와 남은 확인, [PLANS](agents/PLANS.md)는 단계별 최종 상태, [DESIGN](agents/DESIGN.md)은 작성 기준을 담당한다. 상세 사양은 이 문서들에 반복하지 않는다.

결정·분석 문서에는 현재 결과를 설명하는 근거만 남긴다. 서로 다른 버전의 검사 수를 더해 전체 회귀 성공으로 만들지 않으며, 미완·미검증 상태도 유지한다. 이번 작업은 로컬 자료 정리다.
