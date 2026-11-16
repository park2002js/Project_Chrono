# 구조도

| 편집 원본 | 결과와 표현 범위 |
| --- | --- |
| [Character 중심 관계도](Character_First_Level.drawio) | 최종 R05. Character 상속·직접/하위 소유를 21클래스·20의미관계로 표현. 같은 부모의 자식은 하나의 가로선과 부모 쪽 세로선·기호로 연결. 외부 서비스 점선·다중도는 사용자 요청으로 제외 |

Character 도면은 사용자 확정 R05의 편집 가능한 draw.io XML이다. 작성 시점의 관계를 설명하며, 이후 삭제된 DummyBoss 시험 타입까지 현재 런타임에 존재한다는 의미는 아니다. Editor·시험 목록이 섞인 전체 C# 구버전 도면은 삭제했다. 현재 런타임 소스와 제거 범위는 [GAME_SPEC 3.7](../GAME_SPEC.md) 및 [정리 결정 기록](../archive/01_Choice/Submission_Unity_Cleanup_20261116.md)을 따른다.

실제 호출 순서는 [GAME_SPEC](../GAME_SPEC.md)의 기능별 독립 Sequence Diagram을 기준으로 한다. 제출 보고서는 사용자 요청에 따라 Class/Sequence Diagram을 수록하지 않았다.
