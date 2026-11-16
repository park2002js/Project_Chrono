# Draw 만료 입자 최종 표현

[실제 결과 GIF](draw-grains-preview.gif) · [입자 Shader](DrawEvaporationParticle.shader) · [팔레트 좌표](palette-samples.json)

같은 R04 Line 이미지의 흰 중심·청록·파랑 RGB를 샘플해 원형 입자의 색으로 사용한다. 텍스처의 긴 선 모양은 표시하지 않으며 원형 coverage와 입자 수명이 alpha를 제어한다. 화면1px 외곽·고정 입자 크기·이동·소멸 흐름을 유지한다.

팔레트 UV는(0.5,0.5)·(0.5,0.578125)·(0.5,0.70703125)다. 원본 선 이미지를 바꾸면 Line과 입자 재질이 같은 Texture를 참조하는지 함께 확인한다.

Draw·편집·수명·Slash·정리의23검사와 실제 입자 표시를 확인했다. GIF의 촬영용 수명·경고 값은 게임 기본값과 구분하며 상세는 [GAME_SPEC](../../../../docs/GAME_SPEC.md)을 따른다.
