# 선택 주제
주제 : 프로젝트 코드의 namespace 제거 및 이후 작성 규칙
- 2026-11-15 사용자가 모든 namespace를 제거하고 앞으로의 개발에도 namespace를 반영하지 않도록 요청함.
- 기존 DESIGN 2.1은 `Chrono.` 접두사를 사용하는 namespace를 요구했으므로 이번 직접 요청으로 해당 규칙을 교체함.

# 선택 사안 1
- 선택 : 프로젝트에서 직접 작성하는 런타임·Editor C# 코드를 전역 타입으로 작성하며 이후 신규 코드에도 namespace를 선언하지 않음. 기능별 디렉토리 구분과 Unity·외부 패키지 사용에 필요한 `using`은 유지함.
- 이유 : 이유 미기록.
- 제시된 다른 선택 사안들 : 없음.
- 규칙 반영 : [AGENTS의 Unity 코드 작업](../../../AGENTS.md#3-unity-코드-작업), [DESIGN 2.1](../../agents/DESIGN.md#21-cs-code-style).
- 코드 변경·검증 결과는 [GAME_SPEC](../../GAME_SPEC.md)와 [STATUS](../../agents/STATUS.md)의 해당 작업에서 관리함. 규칙 변경 자체를 코드 검증 완료로 간주하지 않음.
