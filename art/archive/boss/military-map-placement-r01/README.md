# 군용 로봇 R04 — Map 두 번째 영역 배치

사용자의 “일단 map의 두번째 영역에 배치” 요청으로 R04를 실제 Map 자식 프리팹에 연결했다. 배치 위치와 정면 대기 자세는 이번 제작값이며, 군용 보스 전투 구현을 완료한 의미는 아니다.

- [실제 Play 카메라](placement-play.png): 격리된 Title→슬롯0→MainWorld 생성 후 검사용 Player를(9.5,66)에 옮겨 기본 줌5.625로 촬영
- [방 전체](overview-editor.png): 위치 확인을 위해 줌11.25로 촬영한 편집 모드 개요
- [정적 Player 비교](placement-editor.png): 편집 모드·기본 줌5.625

## 실제 저장 위치와 규격

- [Map.prefab](../../../../project/Assets/Prefabs/Startup/Map.prefab)의 최상위 `MilitaryRobot` 자식
- [MilitaryRobot.prefab](../../../../project/Assets/Prefabs/Startup/MilitaryRobot.prefab): Transform과 SpriteRenderer만 포함
- [군용 PNG 폴더](../../../../project/Assets/Art/Enemy/Military): R04의 S/SE/E/NE/N/NW/W/SW8개와 import meta
- 위쪽 두 번째 방의 보행 영역 x2~27/y55~73 안, 지면 위치(14.5,64,0), 회전0·배율1
- 기본 정면 S,144×144px·16PPU·지면 피벗(72,24)·Point·압축/mipmap 없음
- 기존 LaboratoryUnlit 공용 재질·표시 순서20, 원본 픽셀과 플레이어의 픽셀 배율 유지

현재 정면 고정의 시각 배치다. 자동8방향 전환·이동/공격·충돌체·피격·조우/처치 등록은 추가하지 않았다. 기존 StageManager의 경비 한 명 생성과 Map.BossPositions, 첫 방·문·체크포인트 연결은 그대로다. 런타임 C#·씬·게임 설정은 변경하지 않았다.

## 확인 결과

같은 Unity6000.3.21f1의 전용 사본에서 현재 관련 소스 컴파일,8방향 import, prefab/Map 재로드와 실제 카메라 렌더를 확인했다. 최종 정적 검사와 Play 검사 모두 종료0이다. [정적 결과](static-result.json)·[Play 결과](play-result.json)·[출판 기록](publication.json)·[독립 파일 검사](independent-review.json)·[최종 검증](verification.json)을 남겼다. 군용19자산과 Map82개 객체 블록이 검증본과 일치한다. 런타임53개 중52개는 같고 병행 수정된 UI_Title.cs1개는 달라, 최신 전체 프로젝트 검증으로 확대하지 않는다.

독립 검사에서8개 PNG가 R04와 바이트 일치하고 알파0/255임을 확인했다. 신규 군용19개 자산과 Map 자식만 추가했으며, Map의 새2블록·루트 자식1줄을 제거하면 저장한 변경 전 Map과 바이트가 정확히 같다. Map GUID와 기존 연결을 보존했고 R04 원본20파일 해시도 유지됐다. 병행 경비 작업에서 바뀐 GuardRobot.prefab은 이번 군용 변경에 포함하지 않는다.

초기 샌드박스의 라이선스 연결 실패는 허용된 별도 Unity 실행으로 해소했다. 병행 경비 작업의 cs/meta 복사 시점 불일치와 미출판 프리팹 때문에 최초 컴파일/Play가 실패했으나, 해당 최신 파일을 검증 사본에 동기화한 뒤 최종 통과했다. 마지막 로그의 UnityEditor.Search 내부 예외1개는 관찰한 Editor 인덱스 문제로 구분하며 런타임 검사 성공에 영향이 없었다.

검증용 저장 폴더만 사용했고 사용자의 저장 파일과 원본 Editor 세션은 조작하지 않았다. 원본 Editor에서의 직접 Play와 제출 빌드는 실행하지 않았다. 게임 생성 흐름을 바꾸지 않아 새 Sequence Diagram이나 Notion 항목은 추가하지 않았다.
