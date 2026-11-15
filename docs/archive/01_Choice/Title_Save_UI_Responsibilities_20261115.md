# 선택 주제
주제 : 게임 부팅과 Title 준비의 책임 분리, Current 저장 접근 및 UI 이름 규칙
- 2026-11-15 사용자가 TitleUI에서 GameManager.Initialize를 호출하고 다시 TitleUI 초기화로 돌아오는 구조의 책임 관계를 수정하도록 요청함.

# 선택 사안 1
- 선택 : GameManager는 앱 부팅에서 공통 저장 데이터를 로드하도록 지시하고 세션·씬 전환을 수행함. Title UI를 참조하지 않으며 Title은 자신의 Start에서 연출과 슬롯 표시를 준비하고 전역 GameManager의 공개 시작 API를 호출함.
- 이유 : 게임 부팅과 UI의 자체 준비 책임을 분리하고, 씬에 다시 들어올 때 Title이 스스로 재설정되므로 GameManager가 Title UI를 알 필요가 없다는 사용자 판단.
- 제시된 다른 선택 사안들
  - 기존 TitleUI → GameManager.Initialize → TitleUI.InitializeAsync의 순환 호출은 이번 요청으로 교체함.

# 선택 사안 2
- 선택 : 외부 조회용 `Current`와 내부 `current`를 분리함. 최초에는 null, 슬롯 선택 시 객체 참조를 설정하며 Title 복귀 시 해제하지 않음. 슬롯 번호는 `SelectSlot`, 선택 함수는 이름 충돌을 피하도록 `SetCurrent`로 구현함.
- 이유 : MainWorld 동안 선택 데이터는 바뀌지 않고 Current의 소비자는 MainWorld에 있으므로 매 조회 시 Slots에서 다시 찾거나 Title 복귀 때 null로 만들 필요가 없다는 사용자 판단.
- 선택 : 외부는 `Current.Set` 후 `Save()`를 호출하면 책임이 끝나며 실제 기록·백업·복구의 결과는 SaveManager가 내부 처리함.
- 이유 : 저장 요청자에게 성공·실패 처리를 요구하지 않고 공통 저장 도구의 책임으로 두려는 사용자 요청.
- 제시된 다른 선택 사안들
  - 기존 CurrentSlot 기반 삼항 조회·Title 복귀 시 선택 해제·저장 상태 공개는 교체함. 기존 파일 백업·재시도 규칙은 유지함.

# 선택 사안 3
- 선택 : 특정 UI 화면·요소에는 `UI_`, UI 전반의 시스템·기반·헬퍼에는 밑줄 없이 `UI` 접두사를 적용하고 이후 개발에도 유지함.
- 이유 : UI 코드를 이름으로 명확하게 구분하기 위한 사용자 요청.
- 제시된 다른 선택 사안들
  - 없음. TitleUI와 LoadingUI는 UI_Title·UI_Loading으로 변경하고 UIController는 공통 UI 코드로 유지함. 월드 CameraController는 역할에 맞게 Systems 디렉토리로 이동함.

# 조건부 성능 비교 요청과 반영 범위
- 사용자는 이번 변경으로 Title의 UniTask 병렬 처리가 깨질 경우에 Notion에 “Title 로드와 Save 데이터의 UniTask 병렬 처리 완료 속도와 단순 단계별 처리 완료 속도를 비교”하는 항목을 기록하도록 요청함.
- 구현에서는 GameManager가 시작한 슬롯 로드의 완료 대기와 Title 연출을 `UniTask.WhenAll`로 함께 기다리는 구조를 유지함. 따라서 조건은 발생하지 않았으며 이번 작업에서 Notion 비교 항목은 추가하지 않음. 성능 차이를 측정하거나 우위를 확인한 것은 아님.
- 현재 구조·API·시퀀스 및 검증 결과 원본은 [GAME_SPEC](../../GAME_SPEC.md)의 U03·S01·S05·2.4, 네이밍 규칙은 [DESIGN 2.1](../../agents/DESIGN.md#21-cs-code-style)에 둠.
