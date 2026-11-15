# 연구자 기본형·흩날림 R02

2026-11-16 사용자 요청에 따라30×30px의 일반 남성·흰 연구 가운 기본형을 먼저 제작하고, 그 기본형에서 머리끝과 가운 자락만 움직이는 모션을 파생했다.

- [기본형 Aseprite](aseprite/researcher-base.aseprite) · [기본형 PNG](png/researcher-base.png) · [기본형12배](base-12x.png)
- [흩날림 Aseprite](aseprite/researcher-flutter.aseprite) · [8배 GIF](flutter-8x.gif)
- [PNG 시트](png/researcher-flutter-sheet.png) · [개별 프레임](png/frames/) · [프레임 비교](frames-comparison.png)
- [검사 결과](validation.json)

검은 머리·검은 뿔테, 흰 가운과 짙은 내의/바지, 피부색3×3px 사각 손을 사용했다. 모든 프레임은30×30px이며 실제 불투명 경계는 최대21×28px다.8레이어·15색으로 구성했다.

기본형은1장, 흩날림은6장이다. 첫 모션 프레임은 기본형과 픽셀이 완전히 같고 다리·셔츠·가운 몸통·얼굴/안경·정수리·손의6레이어는 전 프레임 고정이다. 머리끝과 가운 끝의2레이어만1~2px 움직인다. 프레임당180ms·총1.08초 반복은 **제작 미리보기 제안**으로 최종 게임 속도 결정이 아니다.

native 재열기/개별 PNG·180×30 시트 일치, 이진 알파, 잘림 없음과 고정 레이어 불변을 확인했다. 원본 GIF 및8배 GIF도6프레임으로 재열었다. 이전 R01은 보존했다. 다른 방향·Unity 연결은 별도이며 [GAME_SPEC3.3.12](../../../../docs/GAME_SPEC.md#3312-보스-6종-탑다운-정면-시안--front-r01)을 따른다.
