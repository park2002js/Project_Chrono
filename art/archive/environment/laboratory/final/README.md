# 실험실·복도·문 최종 원본

사용자 확정 형태 corridor-r15와 C 절충 팔레트 `palette-r02-c`를 사용한다. 밝은 벽, 바닥 `#B0ADA1`, 낮춘 바닥 무늬 대비를 적용했으며 검흔의 모양·색은 변경하지 않았다.

| 위치 | 역할 |
| --- | --- |
| [aseprite](aseprite/) | 연결 맵·문 없는 복도·입구문·출구문·환경 타일셋5개 |
| [unity](unity/) | PNG3개와 배치·프레임/태그 메타데이터 JSON3개 |
| [palette.json](palette.json) | 역할별 현재 색상 |
| [preview](preview/) | 현재 맵·문 개방·검흔 대비 결과 |

기본은16px 셀·16PPU·Point·무압축·Mipmap 없음이다. 방 바닥400×288px(25×18셀), 통로80px(5셀), 벽16px이며 크기를 바꾸면 정수 셀을 추가·제거하고 문 앵커와 충돌/카메라 경계도 함께 조정한다.

문 피벗은 아래 중앙(0.5,0)이다. Animator는 Open/Close Trigger를 받고 입구 closed→opening→open, 출구 closed→activation→opening→open 흐름을 사용한다. 출구 활성 표시는100ms씩3회 점멸 후300ms 점등한다. 프레임 시간·태그·배치 좌표는 unity의 메타데이터를 따른다.

색상 편집 시 현재 Aseprite에서 PNG를 재출력하여 이 폴더와 `project/Assets/Art/Environment/Laboratory/Textures/`를 갱신한다. 기존 meta·GUID·슬라이스·프리팹 연결을 보존하며 Atlas의4px padding/Bilinear와 공유 재질을 일괄 변경하지 않는다.

실제 Map·문·Collider·카메라 연결은 현재 게임에 반영돼 있다. 이 팔레트의 파일 일치·정적 화면 확인과 전체 움직임의 최종 가독성 평가는 구분하며, 현재 상태는 [GAME_SPEC](../../../../../docs/GAME_SPEC.md)을 따른다.
