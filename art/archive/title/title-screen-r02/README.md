# Title 로고·흰 테두리·틈새 광

원본200×80 로고의 손잡이·검집에 흰색1px 테두리를 추가하고, 검흔 선을 따라 푸른 광3위상을 별도 이미지로 연결했다.

| 파일 | 역할 |
| --- | --- |
| [현재 Aseprite](title-logo-17-natural-cut.aseprite) · [PNG](title-logo-17-natural-cut.png) | 현재 로고 편집본·출력 |
| [사용자 수정 입력](source-user-original.aseprite) · [입력 PNG](source-user-original.png) | 사용자 수정 원본 보존 |
| [광0](title-fissure-glow-0.png) · [광1](title-fissure-glow-1.png) · [광2](title-fissure-glow-2.png) | 원본 로고에 굽지 않는200×80 RGBA 위상 이미지 |
| [실제 재생 GIF](title-fissure-preview.gif) | 현재 Title 표현 |

흰 테두리는 새 레이어96px이며 기존10레이어·글자·검흔·원래 하이라이트를 보존했다. 푸른 광은 원본 선의 수직거리 약±3px 이내에 두고 양끝을 투명하게 마감한다. UI_Title이 인접 위상을 실제 시간으로 교차 표시하며 기존 십자 빛과 함께 재생한다.

게임 로고·별도 광을 연결하고7개 화면 검사를 확인했다. 광의 강도·박자에 대한 사용자 최종 미술 판단은 별도다.
