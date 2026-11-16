# Project_Chrono 확정 아트·음향

게임에 사용한 자산과 그 편집 원본, 최종 결과를 설명하는 자료만 정리했다. 구버전·반려 시안·미확정 시안·일회성 검사와 과거 오류 로그는 정리 대상이다. 현재 규칙과 실제 확인 범위는 [GAME_SPEC](../../docs/GAME_SPEC.md), 개발 결과는 [종합 결과](../../docs/results/Development_Results.md)를 따른다.

| 대상 | 현재 원본·결과 |
| --- | --- |
| Player 기본 모션 | [Aseprite](player/final/aseprite/) · [게임용 PNG](player/final/unity/) |
| 발도·납도 | [R02 원본](player/iaido-r02/aseprite/iaido-front.aseprite) · [시트](player/iaido-r02/unity/iaido-front.png) |
| 고정 검·검기 | [검 원본](weapon/weapon.aseprite) · [검기 편집·출력](weapon/greatsword-vfx-edit/README.md) · [3타 최종 배치](weapon/player-combo-preview-r14/README.md) |
| 실험실·복도·문 | [최종 환경](environment/laboratory/final/README.md) |
| 자수정 | [최종 원본·출력](environment/amethyst/final/README.md) |
| 최초 입장 | [배치 규칙](environment/entrance-corridor-r03/README.md) · [실제 영상](map/player-entrance-r03/README.md) |
| Title | [로고 원본](title/logo_17_natural_cut_r06/) · [테두리·틈새 광](title/title-screen-r02/README.md) |
| HUD·조준점·키보드 | [UI 패키지](ui/final/README.md) |
| 저장 슬롯 | [공용 프레임](ui/window-frame-r01/README.md) · [최종 화면](ui/window-frame-r01/info-r03/README.md) |
| Draw 선·입자 | [선 이미지](fx/draw-line-image-r04/README.md) · [만료 입자](fx/draw-line-grains-r09/README.md) |
| 탄환·적중·각성 | [탄환](fx/boss-hazards-r06/README.md) · [적중 섬광](fx/player-hit-r01/README.md) · [각성 오라](fx/player-fever-r01/README.md) |
| 음향 | [공격·적중·발소리](audio/player-feedback-r01/README.md) · [Slash 합성음](audio/slash-r01/README.md) |
| 경비 몸체·손 | [몸체 원본](boss/guard-r05/README.md) · [분리 원본](boss/guard-hand-r01/README.md) |
| 경비 패턴 | [손·양손](boss/guard-patterns-r02/README.md) · [부채꼴·벽 충돌](boss/guard-wall-fan-r01/README.md) · [지면 레이저](boss/guard-laser-impact-r02/README.md) |
| 군용 형상·배치 | [3D 원본](boss/military-3d-r05/README.md) · [8방향 픽셀](boss/military-8dir-r04/README.md) · [정적 Map 배치](boss/military-map-placement-r01/README.md) |

발도는 사용자 결정에 따라 R02를 사용한다. 폴더의 가장 큰 버전 번호를 자동으로 선택하지 않는다. Aseprite·SVG·Shader·Material·음원 생성 원본과 실제 PNG/WAV·배치 메타데이터를 편집 기준으로 사용한다.

최종 GIF·전체 프레임표는 결과 설명용이며 별도 표기된 느린 재생 속도를 게임 시간으로 적용하지 않는다. 현재 게임에 연결한 각성·Slash 표현의 최종 미술·청감 판단과 군용의 추가 전투 구현은 완료 범위에 포함하지 않는다.
