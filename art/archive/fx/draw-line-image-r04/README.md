# Draw 선 최종 이미지

[draw-line-profile.png](draw-line-profile.png)는 흰 중심과 청록·파랑 오라, 둥근 양끝을 가진 현재 선 이미지다. 실제 게임의 Line 표현과 만료 입자의 팔레트 입력에 연결했다.

게임이 길이에 맞춰 선을 표시하며 원본 이미지를 임의 확대 저장하지 않는다. 논리 Line.Width는0.25, Slash 시전의 표시 오라 전체 판정 폭은0.75이며 현재 규칙은 [GAME_SPEC](../../../../docs/GAME_SPEC.md)의 F-P04~P07을 따른다.

선·편집·수명·Slash의 관련23검사를 확인했다. PNG를 수정할 때 기존 alpha·끝마감·GUID와 입자 팔레트 연결을 함께 확인한다.
